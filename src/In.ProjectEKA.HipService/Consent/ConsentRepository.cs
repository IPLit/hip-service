using System.Linq;

namespace In.ProjectEKA.HipService.Consent
{
    using System.Threading.Tasks;
    using Common.Model;
    using Database;
    using Microsoft.EntityFrameworkCore;
    using Model;

    public class ConsentRepository : IConsentRepository
    {
        private readonly ConsentContext consentContext;

        public ConsentRepository(ConsentContext consentContext)
        {
            this.consentContext = consentContext;
        }

        public async Task AddAsync(Consent consent)
        {
            await consentContext.ConsentArtefact.AddAsync(consent);
            await consentContext.SaveChangesAsync();
        }

        public async Task UpdateAsync(string consentArtefactId, ConsentStatus status)
        {
            var consentArtefactIdKey = consentArtefactId.ToLowerInvariant();
            var consentArtefact = await consentContext.ConsentArtefact
                .FirstOrDefaultAsync(c => c.ConsentArtefactId.ToLower() == consentArtefactIdKey);
            if (consentArtefact == null)
                return;
            consentArtefact.Status = status;
            await consentContext.SaveChangesAsync();
        }

        public async Task<Consent> GetFor(string consentArtefactId)
        {
            var consentArtefactIdKey = consentArtefactId.ToLowerInvariant();
            return await consentContext.ConsentArtefact
                .FirstOrDefaultAsync(x => x.ConsentArtefactId.ToLower() == consentArtefactIdKey);
        }

        public async Task DeleteConsentArtefact(string healthId)
        {
            var healthIdKey = healthId.ToLowerInvariant();
            var consents = consentContext.ConsentArtefact.AsEnumerable()
                .Where(b => b.ConsentArtefact.Patient.Id != null &&
                            b.ConsentArtefact.Patient.Id.ToLower() == healthIdKey)
                .ToList();
            foreach (var consent in consents)
            {
                consentContext.Remove(consent);
            }

            await consentContext.SaveChangesAsync();
        }
    }
}