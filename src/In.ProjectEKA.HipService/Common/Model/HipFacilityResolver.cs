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
            if (string.IsNullOrWhiteSpace(healthId) || string.IsNullOrWhiteSpace(hipId))
                return false;

            var current = await userAuthRepository.GetAuthDetailsByHealthId(healthId).ConfigureAwait(false);
            var existing = current != null && current.Item2 == null ? current.Item1 : null;
            if (existing != null && existing.HipId == hipId)
            {
                var name = string.IsNullOrEmpty(hipName) ? existing.HipName : hipName;
                var token = accessToken ?? existing.AccessToken;
                if (name == existing.HipName && token == existing.AccessToken)
                    return true;
                return userAuthRepository.Update(new AuthConfirm(healthId, hipId, token, name));
            }

            if (existing != null && !replaceExisting)
                return true;

            if (existing != null)
                await userAuthRepository.Delete(healthId, existing.HipId).ConfigureAwait(false);

            var added = await userAuthRepository.Add(new AuthConfirm(healthId, hipId, accessToken, hipName))
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
