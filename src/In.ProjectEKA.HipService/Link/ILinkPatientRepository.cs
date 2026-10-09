using In.ProjectEKA.HipLibrary.Patient.Model;

namespace In.ProjectEKA.HipService.Link
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Model;
    using Optional;

    public interface ILinkPatientRepository
    {
        Task<Tuple<LinkEnquires, Exception>> SaveRequestWith(
            string linkReferenceNumber,
            string consentManagerId,
            string consentManagerUserId,
            string patientReferenceNumber,
            IEnumerable<string> careContextReferenceNumbers
        );

        Task<Tuple<LinkEnquires, Exception>> GetPatientFor(string linkReferenceNumber);

        Task<Option<LinkedAccounts>> Save(
            string consentManagerUserId,
            string patientReferenceNumber,
            string linkReferenceNumber,
            IEnumerable<string> careContextReferenceNumbers,
            Guid patientUuid
            );

        Task<Tuple<IEnumerable<LinkedAccounts>, Exception>> GetLinkedCareContexts(string consentManagerUserId);

        Task<Tuple<List<string>, Exception>> GetLinkedCareContextsOfPatient(
            string patientReferenceNumber);
        
        Task<Tuple<string, Exception>> GetHealthID(
            string patientReferenceNumber);

        Task<Option<InitiatedLinkRequest>> Save(string requestId, string transactionId, string linkReferenceNumber);

        Task<Option<InitiatedLinkRequest>> GetByRequestId(string requestId);

        Task<Option<IEnumerable<InitiatedLinkRequest>>> Get(string linkReferenceNumber);

        bool Update(InitiatedLinkRequest linkRequest);
        Task<Tuple<Guid, Exception>> GetPatientUuid(string patientReferenceNumber);
        Task<Option<CareContextMap>> SaveCareContextMap(CareContextRepresentation careContextRepresentation);
        Task DeleteLinkedAccounts(string healthId);
        Task DeleteLinkEnquires(string healthId);
    }
}