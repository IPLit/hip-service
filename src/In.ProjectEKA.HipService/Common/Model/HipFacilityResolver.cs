using System;
using System.Threading.Tasks;
using In.ProjectEKA.HipService.UserAuth;

namespace In.ProjectEKA.HipService.Common.Model
{
    /// <summary>
    /// Resolves the HIP facility for a patient.
    /// The visit location is used first, then stored health-id details, then the configured default.
    /// </summary>
    public class HipFacilityResolver
    {
        private readonly IUserAuthRepository userAuthRepository;
        private readonly BahmniConfiguration bahmniConfiguration;

        public HipFacilityResolver(IUserAuthRepository userAuthRepository, BahmniConfiguration bahmniConfiguration)
        {
            this.userAuthRepository = userAuthRepository;
            this.bahmniConfiguration = bahmniConfiguration;
        }

        private async Task<bool> RememberHipDetails(string healthId, string hipId, string hipName, string accessToken,
            bool replaceExisting)
        {
            if (userAuthRepository == null || string.IsNullOrWhiteSpace(healthId) || string.IsNullOrWhiteSpace(hipId))
                return false;

            var current = await userAuthRepository.GetAuthDetailsByHealthId(healthId, hipId).ConfigureAwait(false);
            var existing = current != null && current.Item2 == null ? current.Item1 : null;
            var incomingToken = string.IsNullOrWhiteSpace(accessToken) ? null : accessToken;
            if (existing != null && !string.IsNullOrEmpty(existing.HipId) &&
                existing.HipId.Equals(hipId, StringComparison.OrdinalIgnoreCase))
            {
                var name = string.IsNullOrEmpty(hipName) ? existing.HipName : hipName;
                var storedToken = string.IsNullOrWhiteSpace(existing.AccessToken) ? null : existing.AccessToken;
                var token = incomingToken ?? storedToken;
                var nameUnchanged = string.Equals(name, existing.HipName, StringComparison.OrdinalIgnoreCase);
                var tokenUnchanged = string.Equals(token, storedToken, StringComparison.Ordinal);
                if (nameUnchanged && tokenUnchanged)
                    return true;
                // Keep a blank token on an existing facility row until a real link token arrives.
                // Never replace a stored token with a blank one.
                var tokenToStore = string.IsNullOrWhiteSpace(token) ? existing.AccessToken : token;
                return userAuthRepository.Update(new AuthConfirm(healthId, hipId, tokenToStore, name));
            }

            if (existing != null && !replaceExisting)
                return true;

            // No row yet. A visit-only lookup has no link token; do not insert an AuthConfirm
            // with an empty AccessToken. The token callback inserts the row once a token exists.
            if (incomingToken == null)
                return false;

            var nameNew = NameForStoredHip(hipId, hipName);
            var added = await userAuthRepository.Add(new AuthConfirm(healthId, hipId, incomingToken, nameNew))
                .ConfigureAwait(false);
            return added.HasValue;
        }

        public async Task<HfrDetails> Resolve(string healthId, string visitUuid)
        {
            if (!string.IsNullOrEmpty(visitUuid))
            {
                var fromVisit = await bahmniConfiguration.ResolveFromVisit(visitUuid).ConfigureAwait(false);
                if (fromVisit != null && !string.IsNullOrEmpty(fromVisit.HfrId) && !string.IsNullOrEmpty(fromVisit.HfrName))
                {
                    await RememberHipDetails(healthId, fromVisit.HfrId, fromVisit.HfrName, null, false)
                        .ConfigureAwait(false);
                    return fromVisit;
                }
            }
            if (!string.IsNullOrWhiteSpace(healthId) && userAuthRepository != null)
            {
                var authDetails = await userAuthRepository.GetAuthDetailsByHealthId(healthId).ConfigureAwait(false);
                var stored = authDetails != null && authDetails.Item2 == null ? authDetails.Item1 : null;
                if (!string.IsNullOrEmpty(stored?.HipId))
                    return new HfrDetails(stored.HipId, NameForStoredHip(stored.HipId, stored.HipName));
            }
            return new HfrDetails(bahmniConfiguration.GetDefaultHfrId(), bahmniConfiguration.GetDefaultFacilityName());
        }

        public string NameForStoredHip(string hipId, string hipName)
        {
            if (!string.IsNullOrEmpty(hipName))
                return hipName;
            if (string.Equals(hipId, bahmniConfiguration.GetDefaultHfrId(), StringComparison.OrdinalIgnoreCase))
                return bahmniConfiguration.GetDefaultFacilityName();
            return hipId;
        }
    }
}
