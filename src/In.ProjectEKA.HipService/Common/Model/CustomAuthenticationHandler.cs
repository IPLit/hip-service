using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using In.ProjectEKA.HipService.OpenMrs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace In.ProjectEKA.HipService.Common.Model
{
    using static Constants;

    public class CustomAuthenticationHandler : AuthenticationHandler<CustomAuthenticationOptions>
    {
        private readonly OpenMrsConfiguration _configuration;
        private readonly JwtConfiguration _jwtConfiguration;
        private readonly IHttpClientFactory _httpClientFactory;
        private const string UnauthorizedError = "Failed to authenticate. Please check your credentials.";

        public CustomAuthenticationHandler(
            IOptionsMonitor<CustomAuthenticationOptions> options, 
            ILoggerFactory logger, 
            UrlEncoder encoder, 
            ISystemClock clock, 
            OpenMrsConfiguration configuration, 
            JwtConfiguration jwtConfiguration,
            IHttpClientFactory httpClientFactory) // Inject HttpClientFactory
            : base(options, logger, encoder, clock)
        {
            _configuration = configuration;
            _jwtConfiguration = jwtConfiguration;
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Cookies.ContainsKey(REPORTING_SESSION) || Request.Cookies.ContainsKey(OPENMRS_SESSION_ID_COOKIE_NAME))
            {
                return await PerformCookieAuth();
            }

            if (Request.Headers.ContainsKey(AUTHORIZATION))
            {
                return await PerformTokenAuth();
            }

            return AuthenticateResult.Fail(UnauthorizedError);
        }

        private async Task<AuthenticateResult> PerformTokenAuth()
        {
            string authorizationHeader = Request.Headers[AUTHORIZATION];
            if (authorizationHeader.StartsWith(X_TOKEN_TYPE, StringComparison.OrdinalIgnoreCase))
            {
                string token = authorizationHeader.Substring($"{X_TOKEN_TYPE} ".Length).Trim();

                try
                {
                    var jwtToken = new JwtSecurityToken(token);
                    var key = await GetPublicKeysFromKeycloakAsync(jwtToken.Header.Kid);

                    if (key == null)
                    {
                        return AuthenticateResult.Fail(UnauthorizedError);
                    }

                    ISecurityTokenValidator tokenHandler = new JwtSecurityTokenHandler();
                    var validationParameters = new TokenValidationParameters
                    {
                        ValidateLifetime = true,
                        ValidAudience = _jwtConfiguration.Audience,
                        ValidIssuer = _jwtConfiguration.Authority,
                        IssuerSigningKey = key
                    };

                    var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
                    var identity = principal.Identity as ClaimsIdentity;
                    var claims = identity?.Claims.ToList();

                    string sid = claims?.FirstOrDefault(c => c.Type == "sid")?.Value;

                    // Store both Session ID and Raw Token for ABDM Service usage downstream
                    Request.HttpContext.Items[SESSION_ID] = sid;
                    Request.HttpContext.Items["X-Token"] = token; // Critical for ABDM V3 headers!

                    var ticket = new AuthenticationTicket(principal, Scheme.Name);
                    return AuthenticateResult.Success(ticket);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Token validation failed.");
                    return AuthenticateResult.Fail(UnauthorizedError);
                }
            }
            
            return AuthenticateResult.Fail(UnauthorizedError);
        }

        private async Task<AuthenticateResult> PerformCookieAuth()
        {
            string sessionId = Request.Cookies[REPORTING_SESSION];
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = Request.Cookies[OPENMRS_SESSION_ID_COOKIE_NAME];
            }

            var httpClient = _httpClientFactory.CreateClient();

            using var request = new HttpRequestMessage(HttpMethod.Get, _configuration.Url + WHO_AM_I);
            request.Headers.Add("Cookie", $"{OPENMRS_SESSION_ID_COOKIE_NAME}={sessionId}");

            var response = await httpClient.SendAsync(request).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location != null)
            {
                using var redirectRequest = new HttpRequestMessage(HttpMethod.Get, response.Headers.Location);
                redirectRequest.Headers.Add("Cookie", $"{OPENMRS_SESSION_ID_COOKIE_NAME}={sessionId}");
                response = await httpClient.SendAsync(redirectRequest).ConfigureAwait(false);
            }

            if (!response.IsSuccessStatusCode)
            {
                return AuthenticateResult.Fail(UnauthorizedError);
            }
            Request.HttpContext.Items[SESSION_ID] = sessionId;            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "username"),
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }

        private static byte[] Base64UrlDecode(string base64Url)
        {
            string base64 = base64Url.Replace('_', '/').Replace('-', '+');
            
            while (base64.Length % 4 != 0)
            {
                base64 += '=';
            }

            return Convert.FromBase64String(base64);
        }

        private async Task<SecurityKey> GetPublicKeysFromKeycloakAsync(string kid)
        {
            if (string.IsNullOrEmpty(kid)) return null;

            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(_jwtConfiguration.Cert);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var jwks = new JsonWebKeySet(json);

            foreach (var key in jwks.Keys)
            {
                if (key.Kid != null && key.Kid.Equals(kid, StringComparison.Ordinal))
                {
                    var certificateBytes = Base64UrlDecode(key.X5c[0]);
                    var certificate = new X509Certificate2(certificateBytes);

                    return new X509SecurityKey(certificate);
                }
            }

            return null;
        }
    }
}
