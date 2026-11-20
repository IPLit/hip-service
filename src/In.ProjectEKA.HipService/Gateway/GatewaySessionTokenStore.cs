namespace In.ProjectEKA.HipService.Gateway
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using In.ProjectEKA.HipService.Logger;

    public sealed class GatewaySessionToken
    {
        public GatewaySessionToken(string authorizationValue, int expiresInSeconds)
        {
            AuthorizationValue = authorizationValue;
            ExpiresInSeconds = expiresInSeconds;
        }

        public string AuthorizationValue { get; }

        public int ExpiresInSeconds { get; }
    }

    /// <summary>
    /// Process-wide cache of the ABDM gateway session token.
    /// One refresh runs at a time; callers that arrive during that refresh wait and then reuse the new token.
    /// </summary>
    public sealed class GatewaySessionTokenStore
    {
        private readonly TimeSpan renewalBuffer;
        private readonly Func<DateTime> clock;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private readonly object sync = new object();
        private string authorizationValue;
        private DateTime refreshAtUtc;

        public GatewaySessionTokenStore(TimeSpan renewalBuffer, Func<DateTime> utcNow = null)
        {
            this.renewalBuffer = renewalBuffer < TimeSpan.Zero ? TimeSpan.Zero : renewalBuffer;
            clock = utcNow ?? (() => DateTime.UtcNow);
        }

        public static GatewaySessionTokenStore FromConfiguration(GatewayConfiguration configuration)
        {
            var minutes = configuration?.SessionTokenRenewalBufferMinutes ?? 2;
            if (minutes < 0)
                minutes = 0;
            return new GatewaySessionTokenStore(TimeSpan.FromMinutes(minutes));
        }

        public async Task<string> GetOrCreateAsync(Func<Task<GatewaySessionToken>> fetch)
        {
            var cached = ReadValidToken();
            if (cached != null)
            {
                Log.Information("Session accessToken is valid and not expired!");
                return cached;
            }

            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                cached = ReadValidToken();
                if (cached != null)
                    return cached;

                var created = await fetch().ConfigureAwait(false);
                if (created == null || string.IsNullOrEmpty(created.AuthorizationValue))
                    return null;

                if (created.ExpiresInSeconds > 0)
                {
                    var expiresAtUtc = UtcNow().AddSeconds(created.ExpiresInSeconds);
                    lock (sync)
                    {
                        authorizationValue = created.AuthorizationValue;
                        refreshAtUtc = RefreshAt(expiresAtUtc);
                    }
                }

                return created.AuthorizationValue;
            }
            finally
            {
                gate.Release();
            }
        }

        private string ReadValidToken()
        {
            lock (sync)
            {
                if (authorizationValue == null || UtcNow() >= refreshAtUtc)
                    return null;
                return authorizationValue;
            }
        }

        private DateTime RefreshAt(DateTime expiresAtUtc)
        {
            var lifetime = expiresAtUtc - UtcNow();
            if (lifetime <= TimeSpan.Zero)
                return UtcNow();

            var buffer = renewalBuffer;
            if (buffer >= lifetime)
                buffer = TimeSpan.FromTicks(lifetime.Ticks / 2);

            return expiresAtUtc - buffer;
        }

        private DateTime UtcNow()
        {
            return clock();
        }
    }
}
