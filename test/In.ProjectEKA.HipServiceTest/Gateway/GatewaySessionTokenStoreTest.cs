namespace In.ProjectEKA.HipServiceTest.Gateway
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using HipService.Gateway;
    using Xunit;

    public class GatewaySessionTokenStoreTest
    {
        [Fact]
        public async Task ReusesTokenUntilRenewalBuffer()
        {
            var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var clock = now;
            var fetches = 0;
            var store = new GatewaySessionTokenStore(TimeSpan.FromMinutes(2), () => clock);

            Task<GatewaySessionToken> Fetch()
            {
                var count = ++fetches;
                return Task.FromResult(new GatewaySessionToken("Bearer " + count, 1200));
            }

            var first = await store.GetOrCreateAsync(Fetch);
            var second = await store.GetOrCreateAsync(Fetch);
            clock = now.AddMinutes(17);
            var stillValid = await store.GetOrCreateAsync(Fetch);
            clock = now.AddMinutes(18);
            var renewed = await store.GetOrCreateAsync(Fetch);

            first.Should().Be("Bearer 1");
            second.Should().Be("Bearer 1");
            stillValid.Should().Be("Bearer 1");
            renewed.Should().Be("Bearer 2");
            fetches.Should().Be(2);
        }

        [Fact]
        public async Task ConcurrentCallersShareOneSessionRequest()
        {
            var fetches = 0;
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var store = new GatewaySessionTokenStore(TimeSpan.FromMinutes(2));

            async Task<GatewaySessionToken> Fetch()
            {
                var count = Interlocked.Increment(ref fetches);
                started.TrySetResult(true);
                await release.Task.ConfigureAwait(false);
                return new GatewaySessionToken("Bearer " + count, 1200);
            }

            var first = store.GetOrCreateAsync(Fetch);
            await started.Task.ConfigureAwait(false);
            var second = store.GetOrCreateAsync(Fetch);
            release.TrySetResult(true);
            var results = await Task.WhenAll(first, second).ConfigureAwait(false);

            results.Should().Equal("Bearer 1", "Bearer 1");
            fetches.Should().Be(1);
        }

        [Fact]
        public async Task UsesHalfTheLifetimeWhenTheBufferIsLongerThanTheToken()
        {
            var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var clock = now;
            var fetches = 0;
            var store = new GatewaySessionTokenStore(TimeSpan.FromMinutes(2), () => clock);

            Task<GatewaySessionToken> Fetch()
            {
                var count = ++fetches;
                return Task.FromResult(new GatewaySessionToken("Bearer " + count, 60));
            }

            var first = await store.GetOrCreateAsync(Fetch);
            clock = now.AddSeconds(29);
            var stillValid = await store.GetOrCreateAsync(Fetch);
            clock = now.AddSeconds(30);
            var renewed = await store.GetOrCreateAsync(Fetch);

            first.Should().Be("Bearer 1");
            stillValid.Should().Be("Bearer 1");
            renewed.Should().Be("Bearer 2");
            fetches.Should().Be(2);
        }

        [Fact]
        public async Task DoesNotCacheATokenWithoutExpiresIn()
        {
            var fetches = 0;
            var store = new GatewaySessionTokenStore(TimeSpan.FromMinutes(2));

            Task<GatewaySessionToken> Fetch()
            {
                var count = ++fetches;
                return Task.FromResult(new GatewaySessionToken("Bearer " + count, 0));
            }

            var first = await store.GetOrCreateAsync(Fetch);
            var second = await store.GetOrCreateAsync(Fetch);

            first.Should().Be("Bearer 1");
            second.Should().Be("Bearer 2");
            fetches.Should().Be(2);
        }

        [Fact]
        public async Task DoesNotCacheAFailedSessionCall()
        {
            var fetches = 0;
            var store = new GatewaySessionTokenStore(TimeSpan.FromMinutes(2));

            Task<GatewaySessionToken> Fetch()
            {
                fetches++;
                if (fetches == 1)
                    return Task.FromResult<GatewaySessionToken>(null);
                return Task.FromResult(new GatewaySessionToken("Bearer ok", 1200));
            }

            var failed = await store.GetOrCreateAsync(Fetch);
            var recovered = await store.GetOrCreateAsync(Fetch);
            var reused = await store.GetOrCreateAsync(Fetch);

            failed.Should().BeNull();
            recovered.Should().Be("Bearer ok");
            reused.Should().Be("Bearer ok");
            fetches.Should().Be(2);
        }
    }
}
