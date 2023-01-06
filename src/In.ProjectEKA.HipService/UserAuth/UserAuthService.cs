using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using In.ProjectEKA.HipLibrary.Patient.Model;
using In.ProjectEKA.HipService.Common;
using In.ProjectEKA.HipService.Common.Model;
using In.ProjectEKA.HipService.DataFlow;
using In.ProjectEKA.HipService.Gateway;
using In.ProjectEKA.HipService.UserAuth.Model;
using Microsoft.Extensions.Logging;
using Optional;
using static In.ProjectEKA.HipService.Common.Constants;
using Error = In.ProjectEKA.HipLibrary.Patient.Model.Error;


namespace In.ProjectEKA.HipService.UserAuth
{
    public class UserAuthService : IUserAuthService
    {
        private readonly IUserAuthRepository userAuthRepository;
        private readonly ILogger<UserAuthController> logger;
        private readonly IGatewayClient gatewayClient;


        public UserAuthService(IUserAuthRepository userAuthRepository, ILogger<UserAuthController> logger, IGatewayClient gatewayClient)
        {
            this.userAuthRepository = userAuthRepository;
            this.logger = logger;
            this.gatewayClient = gatewayClient;
        }
        

       
        public Tuple<GatewayFetchModesRequestRepresentation, ErrorRepresentation> FetchModeResponse(
            FetchRequest fetchRequest, BahmniConfiguration bahmniConfiguration)
        {
            var healthId = fetchRequest.healthId;
            if (!(IsValidHealthId(healthId) || IsValidHealthNumber(healthId)))
                return new Tuple<GatewayFetchModesRequestRepresentation, ErrorRepresentation>
                    (null, new ErrorRepresentation(ErrorResponse.InvalidHealthId));
            if (IsValidHealthNumber(healthId))
            {
                healthId = Regex.Replace(healthId, @"^(.{2})(.{4})(.{4})(.{4})$", "$1-$2-$3-$4");
            }

            var requester = new Requester(bahmniConfiguration.Id, HIP);
            var purpose = fetchRequest.purpose;
            var query = purpose != null
                ? new FetchQuery(healthId, purpose, requester)
                : new FetchQuery(healthId, requester);
            var timeStamp = DateTime.Now.ToUniversalTime();
            var requestId = Guid.NewGuid();
            return new Tuple<GatewayFetchModesRequestRepresentation, ErrorRepresentation>
                (new GatewayFetchModesRequestRepresentation(requestId, timeStamp, query), null);
        }
        
        public async Task<ErrorRepresentation> AuthInit(AuthInitRequest authInitRequest,string correlationId,BahmniConfiguration bahmniConfiguration,GatewayConfiguration gatewayConfiguration)
        {
            var (gatewayAuthInitRequestRepresentation, error) = AuthInitResponse(authInitRequest, bahmniConfiguration);
            if (error != null)
                return error;
            Guid requestId = gatewayAuthInitRequestRepresentation.requestId;
            var cmSuffix = gatewayConfiguration.CmSuffix;

            try
            {
                logger.Log(LogLevel.Information,
                    LogEvents.UserAuth,
                    "Request for auth-init to gateway: {@GatewayResponse}",
                    gatewayAuthInitRequestRepresentation.dump(gatewayAuthInitRequestRepresentation));
                logger.Log(LogLevel.Information, LogEvents.UserAuth, $"cmSuffix: {{cmSuffix}}," +
                                                                     $" correlationId: {{correlationId}}, " +
                                                                     $"healthId: {{healthId}}, requestId: {{requestId}}",
                    cmSuffix, correlationId, gatewayAuthInitRequestRepresentation.query.id, requestId);
                await gatewayClient.SendDataToGateway(PATH_AUTH_INIT, gatewayAuthInitRequestRepresentation, cmSuffix,
                    correlationId);
                var i = 0;
                do
                {
                    Thread.Sleep(gatewayConfiguration.TimeOut);
                    if (UserAuthMap.RequestIdToErrorMessage.ContainsKey(requestId))
                    {
                        var gatewayError = UserAuthMap.RequestIdToErrorMessage[requestId];
                        UserAuthMap.RequestIdToErrorMessage.Remove(requestId);
                        return new ErrorRepresentation(gatewayError);
                    }

                    if (UserAuthMap.RequestIdToTransactionIdMap.ContainsKey(requestId))
                    {
                        logger.LogInformation(LogEvents.UserAuth,
                            "Response about to be send for requestId: {RequestId} with transactionId: {TransactionId}",
                            requestId, UserAuthMap.RequestIdToTransactionIdMap[requestId]
                        );
                        if (UserAuthMap.HealthIdToTransactionId.ContainsKey(authInitRequest.healthId))
                        {
                            UserAuthMap.HealthIdToTransactionId[authInitRequest.healthId] = UserAuthMap.RequestIdToTransactionIdMap[requestId];
                        }
                        else
                        {
                            UserAuthMap.HealthIdToTransactionId.Add(authInitRequest.healthId,
                                UserAuthMap.RequestIdToTransactionIdMap[requestId]);
                        }
                        return null;
                    }

                    i++;
                } while (i < gatewayConfiguration.Counter);
            }
            catch (Exception exception)
            {
                logger.LogError(LogEvents.UserAuth, exception, "Error happened for requestId: {RequestId} for" +
                                                               " auth-init request", requestId);
            }
            return new ErrorRepresentation(new Error(ErrorCode.GatewayTimedOut, "Gateway timed out"));
        }

        private Tuple<GatewayAuthInitRequestRepresentation, ErrorRepresentation> AuthInitResponse(
            AuthInitRequest authInitRequest, BahmniConfiguration bahmniConfiguration)
        {
            var healthId = authInitRequest.healthId;
            if (!(IsValidHealthId(healthId) || IsValidHealthNumber(healthId)))
                return new Tuple<GatewayAuthInitRequestRepresentation, ErrorRepresentation>
                    (null, new ErrorRepresentation(ErrorResponse.InvalidHealthId));
            if (IsValidHealthNumber(healthId))
            {
                healthId = Regex.Replace(healthId, @"^(.{2})(.{4})(.{4})(.{4})$", "$1-$2-$3-$4");
            }

            var timeStamp = DateTime.Now.ToUniversalTime();
            var requestId = Guid.NewGuid();
            var requester = new Requester(bahmniConfiguration.Id, HIP);
            var purpose = authInitRequest.purpose;
            var authInitQuery = purpose != null
                ? new AuthInitQuery(healthId, purpose, authInitRequest.authMode, requester)
                : new AuthInitQuery(healthId, authInitRequest.authMode, requester);
            return new Tuple<GatewayAuthInitRequestRepresentation, ErrorRepresentation>
                (new GatewayAuthInitRequestRepresentation(requestId, timeStamp, authInitQuery), null);
        }
        
         public async Task<Tuple<AuthConfirmResponse, ErrorRepresentation>> AuthConfirm(AuthConfirmRequest authConfirmRequest,string correlationId,GatewayConfiguration gatewayConfiguration)
        {
            var (gatewayAuthConfirmRequestRepresentation, error) = AuthConfirmResponse(authConfirmRequest);
            if (error != null)
                return new Tuple<AuthConfirmResponse, ErrorRepresentation>(null, new ErrorRepresentation(new Error(ErrorCode.BadRequest,error.Error.Message)));
            var requestId = gatewayAuthConfirmRequestRepresentation.requestId;
            var cmSuffix = gatewayConfiguration.CmSuffix;

            try
            {
                logger.Log(LogLevel.Information,
                    LogEvents.UserAuth,
                    "Request for auth-confirm to gateway: {@GatewayResponse}",
                    gatewayAuthConfirmRequestRepresentation.dump(gatewayAuthConfirmRequestRepresentation));
                logger.Log(LogLevel.Information,
                    LogEvents.UserAuth, $" : {{cmSuffix}}, correlationId: {{correlationId}}," +
                                        $" authCode: {{authCode}}, transactionId: {{transactionId}} requestId: {{requestId}}",
                    cmSuffix, correlationId, gatewayAuthConfirmRequestRepresentation.credential.authCode,
                    gatewayAuthConfirmRequestRepresentation.transactionId, requestId);
                await gatewayClient.SendDataToGateway(PATH_AUTH_CONFIRM, gatewayAuthConfirmRequestRepresentation
                    , cmSuffix, correlationId);
                var i = 0;
                do
                {
                    Thread.Sleep(gatewayConfiguration.TimeOut + 8000);
                    if (UserAuthMap.RequestIdToErrorMessage.ContainsKey(requestId))
                    {
                        var gatewayError = UserAuthMap.RequestIdToErrorMessage[requestId];
                        UserAuthMap.RequestIdToErrorMessage.Remove(requestId);
                        return new Tuple<AuthConfirmResponse, ErrorRepresentation>(null, new ErrorRepresentation(gatewayError));
                    }

                    if (UserAuthMap.RequestIdToAccessToken.ContainsKey(requestId) &&
                        UserAuthMap.RequestIdToPatientDetails.ContainsKey(requestId))
                    {
                        logger.LogInformation(LogEvents.UserAuth,
                            "Response about to be send for requestId: {RequestId} with accessToken: {AccessToken}",
                            requestId, UserAuthMap.RequestIdToAccessToken[requestId]
                        );
                        return new Tuple<AuthConfirmResponse, ErrorRepresentation>(new AuthConfirmResponse(UserAuthMap.RequestIdToPatientDetails[requestId]), null);
                    }

                    i++;
                } while (i < gatewayConfiguration.Counter);
            }
            catch (Exception exception)
            {
                logger.LogError(LogEvents.UserAuth, exception, "Error happened for requestId: {RequestId}", requestId);
            }

            return new Tuple<AuthConfirmResponse, ErrorRepresentation>(null, new ErrorRepresentation(new Error(ErrorCode.GatewayTimedOut, "Gateway timed out")));

        }

        private Tuple<GatewayAuthConfirmRequestRepresentation, ErrorRepresentation> AuthConfirmResponse(
            AuthConfirmRequest authConfirmRequest)
        {
            var healthId = authConfirmRequest.healthId;
            if (!((IsValidHealthId(healthId) || IsValidHealthNumber(healthId)) && IsPresentInMap(healthId)))
                return new Tuple<GatewayAuthConfirmRequestRepresentation, ErrorRepresentation>
                    (null, new ErrorRepresentation(new Error(ErrorCode.InvalidHealthId, "HealthId is invalid")));
            var credential = authConfirmRequest.Demographic == null
                ? new AuthConfirmCredential(GetDecodedOtp(authConfirmRequest.authCode), null)
                : new AuthConfirmCredential(null, authConfirmRequest.Demographic);

            var transactionId = UserAuthMap.HealthIdToTransactionId[healthId];
            var timeStamp = DateTime.Now.ToUniversalTime();
            var requestId = Guid.NewGuid();
            return new Tuple<GatewayAuthConfirmRequestRepresentation, ErrorRepresentation>
            (new GatewayAuthConfirmRequestRepresentation(requestId, timeStamp, transactionId, credential),
                null);
        }

        private static bool IsValidHealthId(string healthId)
        {
            string pattern = @"^[a-zA-Z]+(([a-zA-Z.0-9]+){2})[a-zA-Z0-9]+@[a-zA-Z]+$";
            return Regex.Match(healthId, pattern).Success;
        }

        private static string GetDecodedOtp(String authCode)
        {
            if (authCode == null) return null;
            var decodedOtp = Convert.FromBase64String(authCode);
            var otp = Encoding.UTF8.GetString(decodedOtp);
            return otp;
        }

        private static bool IsValidHealthNumber(string healthId)
        {
            string pattern = @"^(\d{14})$|^([0-9]{2}[-][0-9]{4}[-][0-9]{4}[-][0-9]{4})$";
            return Regex.Match(healthId, pattern).Success;
        }

        private static bool IsPresentInMap(string healthId)
        {
            return UserAuthMap.HealthIdToTransactionId.ContainsKey(healthId);
        }

        private string getHealthId(string accessToken)
        {
            var token = new JwtSecurityTokenHandler().ReadToken(accessToken) as JwtSecurityToken;
            return token?.Claims.First(c => c.Type == "patientId").Value;
        }

        public async Task<Tuple<AuthConfirm, ErrorRepresentation>> OnAuthConfirmResponse(
            OnAuthConfirmRequest onAuthConfirmRequest)
        {
            var accessToken = onAuthConfirmRequest.auth.accessToken;
            var healthId = onAuthConfirmRequest.auth.patient.id;
            if(healthId == null)
            {
                healthId = getHealthId(onAuthConfirmRequest.auth.accessToken);
            }
            var authConfirm = new AuthConfirm(healthId, accessToken);
            var savedAuthConfirm = userAuthRepository.Get(healthId).Result;
            if (savedAuthConfirm.Equals(Option.Some<AuthConfirm>(null)))
            {
                var authConfirmResponse = await userAuthRepository.Add(authConfirm).ConfigureAwait(false);
                if (!authConfirmResponse.HasValue)
                {
                    return new Tuple<AuthConfirm, ErrorRepresentation>(null,
                        new ErrorRepresentation(new Error(ErrorCode.DuplicateAuthConfirmRequest,
                            "Auth confirm request already exists")));
                }
            }
            else
            {
                userAuthRepository.Update(authConfirm);
            }

            UserAuthMap.HealthIdToTransactionId.Remove(healthId);
            var requestId = Guid.Parse(onAuthConfirmRequest.resp.RequestId);
            UserAuthMap.RequestIdToAccessToken.Add(requestId, accessToken);
            if (UserAuthMap.HealthIdToAccessToken.ContainsKey(healthId))
            {
                UserAuthMap.HealthIdToAccessToken[healthId] = accessToken;
            }
            else
            {
                UserAuthMap.HealthIdToAccessToken.Add(healthId, accessToken);
            }

            UserAuthMap.RequestIdToPatientDetails.Add(requestId, onAuthConfirmRequest.auth.patient);
            return new Tuple<AuthConfirm, ErrorRepresentation>(authConfirm, null);
        }

        public async Task Dump(NdhmDemographics ndhmDemographics)
        {
            await userAuthRepository.AddDemographics(ndhmDemographics).ConfigureAwait(false);
        }

    }
}