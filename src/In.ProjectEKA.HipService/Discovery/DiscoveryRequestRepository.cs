using System.Linq;

namespace In.ProjectEKA.HipService.Discovery
{
    using System.Threading.Tasks;
    using Database;
    using Microsoft.EntityFrameworkCore;
    using Model;

    public class DiscoveryRequestRepository : IDiscoveryRequestRepository
    {
        private readonly DiscoveryContext discoveryContext;

        public DiscoveryRequestRepository(DiscoveryContext discoveryContext)
        {
            this.discoveryContext = discoveryContext;
        }

        public async Task Add(DiscoveryRequest discoveryRequest)
        {
            await discoveryContext.DiscoveryRequest.AddAsync(discoveryRequest);
            await discoveryContext.SaveChangesAsync();
        }

        public async Task Delete(string requestId, string consentManagerUserId)
        {
            var requestIdKey = requestId.ToLowerInvariant();
            var consentManagerUserIdKey = consentManagerUserId.ToLowerInvariant();
            var discoveryRequest = await discoveryContext.DiscoveryRequest
                .FirstAsync(request =>
                    request.TransactionId.ToLower() == requestIdKey &&
                    request.ConsentManagerUserId.ToLower() == consentManagerUserIdKey);
            discoveryContext.Remove(discoveryRequest);
            await discoveryContext.SaveChangesAsync();
        }

        public Task<bool> RequestExistsFor(
            string requestId,
            string consentManagerUserId,
            string patientReferenceNumber)
        {
            var requestIdKey = requestId.ToLowerInvariant();
            var consentManagerUserIdKey = consentManagerUserId.ToLowerInvariant();
            var patientReferenceKey = patientReferenceNumber.ToLowerInvariant();
            return discoveryContext.DiscoveryRequest
                .AnyAsync(request => request.TransactionId.ToLower() == requestIdKey
                                     && request.ConsentManagerUserId.ToLower() == consentManagerUserIdKey
                                     && request.PatientReferenceNumber.ToLower() == patientReferenceKey);
        }

        public Task<bool> RequestExistsFor(string requestId)
        {
            var requestIdKey = requestId.ToLowerInvariant();
            return discoveryContext.DiscoveryRequest
                .AnyAsync(request => request.TransactionId.ToLower() == requestIdKey);
        }

        public async Task DeleteDiscoveryRequest(string healthId)
        {
            var healthIdKey = healthId.ToLowerInvariant();
            var discoveryRequests = discoveryContext.DiscoveryRequest
                .Where(request =>
                    request.ConsentManagerUserId.ToLower() == healthIdKey);
            foreach (var discoveryRequest in discoveryRequests)
            {
                discoveryContext.Remove(discoveryRequest);
            }

            await discoveryContext.SaveChangesAsync();
        }
    }
}