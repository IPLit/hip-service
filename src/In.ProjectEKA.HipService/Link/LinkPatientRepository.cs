using In.ProjectEKA.HipLibrary.Patient.Model;

namespace In.ProjectEKA.HipService.Link
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Common;
    using Database;
    using Logger;
    using Microsoft.EntityFrameworkCore;
    using Model;
    using Optional;

    public class LinkPatientRepository : ILinkPatientRepository
    {
        private readonly LinkPatientContext linkPatientContext;

        public LinkPatientRepository(LinkPatientContext linkPatientContext)
        {
            this.linkPatientContext = linkPatientContext;
        }

        public async Task<Tuple<LinkEnquires, Exception>> SaveRequestWith(
            string linkReferenceNumber,
            string consentManagerId,
            string consentManagerUserId,
            string patientReferenceNumber,
            IEnumerable<string> careContextReferenceNumbers)
        {
            var dateTimeStamp = DateTime.Now.ToUniversalTime().ToString(Constants.DateTimeFormat);
            var linkedCareContexts = careContextReferenceNumbers
                .Select(referenceNumber => new CareContext(referenceNumber)).ToList();
            var linkRequest = new LinkEnquires(
                patientReferenceNumber,
                linkReferenceNumber,
                consentManagerId,
                consentManagerUserId,
                dateTimeStamp,
                linkedCareContexts);
            try
            {
                await linkPatientContext.LinkEnquires.AddAsync(linkRequest).ConfigureAwait(false);
                await linkPatientContext.SaveChangesAsync().ConfigureAwait(false);
                return new Tuple<LinkEnquires, Exception>(linkRequest, null);
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, exception.StackTrace);
                return new Tuple<LinkEnquires, Exception>(null, exception);
            }
        }

        public async Task<Tuple<LinkEnquires, Exception>> GetPatientFor(string linkReferenceNumber)
        {
            try
            {
                var linkRequest = await linkPatientContext.LinkEnquires.Include("CareContexts")
                    .FirstAsync(request => request.LinkReferenceNumber == linkReferenceNumber)
                    .ConfigureAwait(false);
                return new Tuple<LinkEnquires, Exception>(linkRequest, null);
            }
            catch (Exception exception)
            {
                Log.Error("Can't find patient for the given reference number");
                // Log.Fatal(exception, exception.StackTrace);
                return new Tuple<LinkEnquires, Exception>(null, exception);
            }
        }

        public async Task<Option<LinkedAccounts>> Save(string consentManagerUserId,
            string patientReferenceNumber,
            string linkReferenceNumber,
            IEnumerable<string> careContextReferenceNumbers,
            Guid patientUuid
        )
        {
            var linkedAccounts = new LinkedAccounts(patientReferenceNumber,
                linkReferenceNumber,
                consentManagerUserId,
                DateTime.Now.ToUniversalTime().ToString(Constants.DateTimeFormat),
                careContextReferenceNumbers.ToList(),
                patientUuid);
            try
            {
                await linkPatientContext.LinkedAccounts.AddAsync(linkedAccounts).ConfigureAwait(false);
                await linkPatientContext.SaveChangesAsync().ConfigureAwait(false);
                return Option.Some(linkedAccounts);
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, exception.StackTrace);
                return Option.None<LinkedAccounts>();
            }
        }

        public async Task<Tuple<IEnumerable<LinkedAccounts>, Exception>> GetLinkedCareContexts(
            string consentManagerUserId)
        {
            try
            {
                var linkRequest = await linkPatientContext.LinkedAccounts
                    .Where(request => request.ConsentManagerUserId.Equals(consentManagerUserId))
                    .ToListAsync()
                    .ConfigureAwait(false);
                return new Tuple<IEnumerable<LinkedAccounts>, Exception>(linkRequest, null);
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, exception.StackTrace);
                return new Tuple<IEnumerable<LinkedAccounts>, Exception>(null, exception);
            }
        }

        public async Task<Tuple<List<string>, Exception>> GetLinkedCareContextsOfPatient(
            string patientReferenceNumber)
        {
            try
            {
                var linkRequest = await linkPatientContext.LinkedAccounts.OrderByDescending(i => i.DateTimeStamp)
                    .FirstOrDefaultAsync(request => request.PatientReferenceNumber.Trim().Equals(patientReferenceNumber.Trim()));
                   if (linkRequest != null)
                   {
                       return new Tuple<List<string>, Exception>(linkRequest.CareContexts, null);
                   }
                   else
                   {
                       return new Tuple<List<string>, Exception>(null, null);
                   }
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, exception.StackTrace);
                return new Tuple<List<string>, Exception>(null, exception);
            }
        }

        public async Task DeleteLinkedAccounts(string healthId)
        {
            var linkedAccounts = linkPatientContext.LinkedAccounts
                .Where(request =>
                    request.ConsentManagerUserId == healthId);
            foreach (var linkedAccount in linkedAccounts)
            {
                linkPatientContext.Remove(linkedAccount);
            }

            await linkPatientContext.SaveChangesAsync();
        }

        public async Task DeleteLinkEnquires(string healthId)
        {
            var linkEnquires = linkPatientContext.LinkEnquires.Include("CareContexts")
                .Where(request =>
                    request.ConsentManagerUserId == healthId);
            foreach (var linkEnquire in linkEnquires)
            {
                linkPatientContext.Remove(linkEnquire);
            }

            await linkPatientContext.SaveChangesAsync();
        }


        public async Task<Tuple<string, Exception>> GetHealthID(
            string patientReferenceNumber)
        {
            try
            {
                var linkRequest = await linkPatientContext.LinkedAccounts
                    .FirstOrDefaultAsync(request => request.PatientReferenceNumber.Trim().Equals(patientReferenceNumber.Trim()));
                return new Tuple<string, Exception>(linkRequest.ConsentManagerUserId, null);
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, exception.StackTrace);
                return new Tuple<string, Exception>(null, exception);
            }
        }

        public async Task<Option<InitiatedLinkRequest>> Save(string requestId,
            string transactionId,
            string linkReferenceNumber)
        {
            try
            {
                var initiatedLinkRequest = new InitiatedLinkRequest(requestId,
                    transactionId,
                    linkReferenceNumber,
                    false,
                    DateTime.Now.ToUniversalTime().ToString(Constants.DateTimeFormat));
                await linkPatientContext.InitiatedLinkRequest.AddAsync(initiatedLinkRequest).ConfigureAwait(false);
                await linkPatientContext.SaveChangesAsync();
                return Option.Some(initiatedLinkRequest);
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, exception.StackTrace);
                return Option.None<InitiatedLinkRequest>();
            }
        }

        public async Task<Option<CareContextMap>> SaveCareContextMap(
            CareContextRepresentation careContextRepresentation)
        {
            try
            {
                var careContextMap = await linkPatientContext.CareContextMap
                    .FirstOrDefaultAsync(c =>
                        c.CareContextName == careContextRepresentation.ReferenceNumber);
                if (careContextMap == null)
                {
                    var careContextMapNew =
                        new CareContextMap(careContextRepresentation.ReferenceNumber,
                            careContextRepresentation.Type);
                    await linkPatientContext.CareContextMap.AddAsync(careContextMapNew)
                        .ConfigureAwait(false);
                }

                await linkPatientContext.SaveChangesAsync();
                return Option.Some(careContextMap);
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, exception.StackTrace);
                return Option.None<CareContextMap>();
            }
        }

        public async Task<Option<IEnumerable<InitiatedLinkRequest>>> Get(string linkReferenceNumber)
        {
            try
            {
                var initiatedLinkRequest = await linkPatientContext.InitiatedLinkRequest
                    .Where(request => request.LinkReferenceNumber.Equals(linkReferenceNumber))
                    .ToListAsync()
                    .ConfigureAwait(false);
                return Option.Some(initiatedLinkRequest.AsEnumerable());
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, exception.StackTrace);
                return Option.None<IEnumerable<InitiatedLinkRequest>>();
            }
        }


        public async Task<Tuple<Guid, Exception>> GetPatientUuid(string consentManagerId)
        {
            try
            {
                var linkRequest = await linkPatientContext.LinkedAccounts
                    .FirstOrDefaultAsync(request => request.ConsentManagerUserId == consentManagerId);
                return new Tuple<Guid, Exception>(linkRequest.PatientUuid, null);
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, exception.StackTrace);
                return new Tuple<Guid, Exception>(Guid.Empty, exception);
            }
        }

        public bool Update(InitiatedLinkRequest linkRequest)
        {
            try
            {
                linkPatientContext.InitiatedLinkRequest.Update(linkRequest);
                linkPatientContext.SaveChanges();
                return true;
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, exception.StackTrace);
                return false;
            }
        }
    }
}