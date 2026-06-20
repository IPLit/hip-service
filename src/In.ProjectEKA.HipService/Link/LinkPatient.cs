using In.ProjectEKA.HipService.Common.Model;
using In.ProjectEKA.HipService.OpenMrs;
using In.ProjectEKA.HipService.UserAuth;
using In.ProjectEKA.HipService.UserAuth.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace In.ProjectEKA.HipService.Link
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Transactions;
    using Common;
    using Discovery;
    using HipLibrary.Patient;
    using HipLibrary.Patient.Model;
    using Logger;
    using Microsoft.Extensions.Options;
    using Model;
    using static In.ProjectEKA.HipService.Discovery.DiscoveryReqMap;
    
    public class LinkPatient
    {
        private readonly IDiscoveryRequestRepository discoveryRequestRepository;
        private readonly ILinkPatientRepository linkPatientRepository;
        private readonly IOptions<OtpServiceConfiguration> otpService;
        private readonly IPatientRepository patientRepository;
        private readonly IPatientVerification patientVerification;
        private readonly ReferenceNumberGenerator referenceNumberGenerator;
        private readonly IOpenMrsClient openMrsClient;
        private readonly IUserAuthService userAuthService;
        private readonly BahmniConfiguration bahmniConfiguration;

        public LinkPatient(
            ILinkPatientRepository linkPatientRepository,
            IPatientRepository patientRepository,
            IPatientVerification patientVerification,
            ReferenceNumberGenerator referenceNumberGenerator,
            IDiscoveryRequestRepository discoveryRequestRepository,
            IOptions<OtpServiceConfiguration> otpService,
            IOpenMrsClient openMrsClient,
            IUserAuthService userAuthService,
            BahmniConfiguration bahmniConfiguration)
        {
            this.linkPatientRepository = linkPatientRepository;
            this.patientRepository = patientRepository;
            this.patientVerification = patientVerification;
            this.referenceNumberGenerator = referenceNumberGenerator;
            this.discoveryRequestRepository = discoveryRequestRepository;
            this.otpService = otpService;
            this.openMrsClient = openMrsClient;
            this.userAuthService = userAuthService;
            this.bahmniConfiguration = bahmniConfiguration;
        }

        public virtual async Task<ValueTuple<PatientLinkEnquiryRepresentation, ErrorRepresentation>> LinkPatients(
            PatientLinkEnquiry request)
        {
            var (patient, error) = await PatientAndCareContextValidation(request);
            if (error != null)
            {
                Log.Error(error.Error.Message);
                return (null, error);
            }

            var linkRefNumber = referenceNumberGenerator.NewGuid();
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                if (!await SaveInitiatedLinkRequest(request.RequestId, request.TransactionId, linkRefNumber)
                    .ConfigureAwait(false))
                    return (null,
                        new ErrorRepresentation(new Error(ErrorCode.DuplicateRequestId, ErrorMessage.DuplicateRequestId))
                        );

                var careContextReferenceNumbers = request.Patient.CareContexts
                    .Select(context => context.ReferenceNumber)
                    .ToArray();

                // Extract visit UUID from first care context (format: "patientId:visitUuid")
                var visitUuid = careContextReferenceNumbers.First() != null
                    ? ExtractVisitUuidFromReference(careContextReferenceNumbers.First())
                    : null;
                var hipId = bahmniConfiguration.GetHfrIdByVisitUuid(visitUuid);
                if (string.IsNullOrEmpty(hipId))
                {
                    Log.Information($"PostTo: Attempting to set HFR ID for visit UUID: {visitUuid}");
                    var hfrId = await SetHfrIdForVisitAsync(visitUuid).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(hfrId))
                    {
                        hipId = hfrId;
                        Log.Information($"PostTo: Successfully set HFR ID {hfrId} for visit UUID {visitUuid}");
                    }
                    else
                    {
                        hipId = bahmniConfiguration.GetDefaultHfrId();
                    }
                }
                var hipName = bahmniConfiguration.GetFacilityNameByVisitUuid(visitUuid);
                if (string.IsNullOrEmpty(hipName))
                {
                    hipName = bahmniConfiguration.GetDefaultFacilityName();
                }

                var (_, exception) = await linkPatientRepository.SaveRequestWith(
                    linkRefNumber,
                    request.Patient.ConsentManagerId,
                    request.Patient.ConsentManagerUserId,
                    request.Patient.ReferenceNumber,
                    careContextReferenceNumbers)
                    .ConfigureAwait(false);
                if (exception != null)
                    return (null,
                        new ErrorRepresentation(new Error(ErrorCode.ServerInternalError,
                            ErrorMessage.DatabaseStorageError)));

                var session = new Session(
                    linkRefNumber,
                    new Communication(CommunicationMode.MOBILE, patient.PhoneNumber),
                    new OtpGenerationDetail(hipName,
                        OtpAction.LINK_PATIENT_CARECONTEXT.ToString()));
                var otpGeneration = await patientVerification.SendTokenFor(session);
                if (otpGeneration != null)
                    return (null,
                        new ErrorRepresentation(new Error(ErrorCode.OtpGenerationFailed, otpGeneration.Message)));

                await discoveryRequestRepository.Delete(request.TransactionId, request.Patient.ConsentManagerUserId)
                    .ConfigureAwait(false);

                scope.Complete();
            }

            var time = new TimeSpan(0, 0, otpService.Value.OffsetInMinutes, 0);
            var expiry = DateTime.Now.Add(time).ToUniversalTime().ToString(Constants.DateTimeFormat);
            var meta = new LinkReferenceMeta(nameof(CommunicationMode.MOBILE), patient.PhoneNumber, expiry);
            var patientLinkReferenceResponse = new PatientLinkEnquiryRepresentation(
                new LinkEnquiryRepresentation(linkRefNumber, "MEDIATED", meta));
            return (patientLinkReferenceResponse, null);
        }

        private async Task<ValueTuple<HipLibrary.Patient.Model.Patient, ErrorRepresentation>> PatientAndCareContextValidation(
            PatientLinkEnquiry request)
        {
            var patient = await patientRepository.PatientWithAsync(request.Patient.ReferenceNumber);
            return patient.Map(patient =>
                    {
                        var programs = request.Patient.CareContexts
                            .Where(careContext =>
                                patient.CareContexts.Any(c => c.ReferenceNumber == careContext.ReferenceNumber))
                            .Select(context => new CareContextRepresentation(context.ReferenceNumber,
                                patient.CareContexts.First(info => info.ReferenceNumber == context.ReferenceNumber)
                                    .Display)).ToList();
                        if (programs.Count != request.Patient.CareContexts.Count())
                            return (null, new ErrorRepresentation(new Error(ErrorCode.CareContextNotFound,
                                ErrorMessage.CareContextNotFound)));

                        return (patient, (ErrorRepresentation) null);
                    })
                .ValueOr((null,
                    new ErrorRepresentation(new Error(ErrorCode.NoPatientFound, ErrorMessage.NoPatientFound))));
        }

        public virtual async Task<ValueTuple<PatientLinkConfirmationRepresentation, string, ErrorRepresentation>>
            VerifyAndLinkCareContext(
            LinkConfirmationRequest request)
        {
            var (linkEnquires, exception) =
                await linkPatientRepository.GetPatientFor(request.LinkReferenceNumber);
            var cmId = "";
            if (exception != null)
                return (null,cmId,
                    new ErrorRepresentation(new Error(ErrorCode.NoLinkRequestFound, ErrorMessage.NoLinkRequestFound)));
            cmId = linkEnquires.ConsentManagerId;

            var errorResponse = await patientVerification.Verify(request.LinkReferenceNumber, request.Token);
            if (errorResponse != null)
                return (null,cmId, new ErrorRepresentation(errorResponse.toError()));

            var patient = await patientRepository.PatientWithAsync(linkEnquires.PatientReferenceNumber);
            return await patient.Map( async patient =>
                {
                    var savedLinkRequests = await linkPatientRepository.Get(request.LinkReferenceNumber);
                    savedLinkRequests.MatchSome(linkRequests =>
                    {
                        foreach (var linkRequest in linkRequests)
                        {
                            linkRequest.Status = true;
                            linkPatientRepository.Update(linkRequest);
                        }
                    });

                    var representations = linkEnquires.CareContexts
                        .Where(careContext =>
                            patient.CareContexts.Any(info => info.ReferenceNumber == careContext.CareContextName))
                        .Select(context => new CareContextRepresentation(context.CareContextName,
                            patient.CareContexts.First(info => info.ReferenceNumber == context.CareContextName)
                                .Display));
                    var linkConfirmationRepresentations = patient.CareContexts
                        .Where(info => representations.Any(context => context.ReferenceNumber == info.ReferenceNumber))
                        .Where(cc => cc.HiTypes != null && cc.HiTypes.Any())
                        .SelectMany(cc => cc.HiTypes.Select(hiType => new { HiType = hiType, CareContext = cc }))
                        .GroupBy(x => x.HiType)
                        .Select(group => new LinkConfirmationRepresentation(linkEnquires.PatientReferenceNumber,
                            $"{patient.Name}",
                            group.Select(x => new CareContextRepresentation(x.CareContext.ReferenceNumber, x.CareContext.Display))
                                .ToList(),
                            group.Key.ToString(),
                            group.Count()))
                        .ToList();
                    var patientLinkResponse = new PatientLinkConfirmationRepresentation(linkConfirmationRepresentations);
                    var resp = await SaveLinkedAccounts(linkEnquires, patient.Uuid);
                    if (resp)
                    {
                        LinkAbhaIdentifier(patient.Uuid, linkEnquires.ConsentManagerUserId);
                        return (patientLinkResponse, cmId, (ErrorRepresentation) null);
                    } 
                    return (null,cmId,
                            new ErrorRepresentation(new Error(ErrorCode.NoPatientFound,
                                ErrorMessage.NoPatientFound)));
                }).ValueOr(
                    Task.FromResult<ValueTuple<PatientLinkConfirmationRepresentation, string, ErrorRepresentation>>(
                        (null, cmId,new ErrorRepresentation(new Error(ErrorCode.CareContextNotFound,
                            ErrorMessage.CareContextNotFound)))));
        }

        private async Task<bool> SaveLinkedAccounts(LinkEnquires linkEnquires,string patientUuid)
        {
            var linkedAccount = await linkPatientRepository.Save(
                linkEnquires.ConsentManagerUserId,
                linkEnquires.PatientReferenceNumber,
                linkEnquires.LinkReferenceNumber,
                linkEnquires.CareContexts.Select(context => context.CareContextName).ToList(),
                (patientUuid!=null?Guid.Parse(patientUuid): Guid.Empty)
                )
                .ConfigureAwait(false);
            
            return linkedAccount.HasValue;
            
        }
        
        private async void LinkAbhaIdentifier(string patientUuid, string abhaAddress)
        {
            var patient = PatientInfoMap[abhaAddress];
            var abhaNumberIdentifier =  patient?.VerifiedIdentifiers.FirstOrDefault(id => id.Type == IdentifierType.ABHA_NUMBER);
            var json = JsonConvert.SerializeObject(new PatientAbhaIdentifier(abhaNumberIdentifier?.Value, abhaAddress), new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            });
            var resp = await openMrsClient.PostAsync(
                    $"{Constants.PATH_OPENMRS_UPDATE_IDENTIFIER}/{patientUuid}",
                    json
                )
                .ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                var ndhmDemographics = new NdhmDemographics(abhaAddress, patient.Name, patient.Gender.ToString(), patient.YearOfBirth.ToString(), patient.VerifiedIdentifiers.FirstOrDefault(id => id.Type == IdentifierType.MOBILE)?.Value);
                await userAuthService.Dump(ndhmDemographics);
            }
            else
            {
                Log.Error("Errored in linking the abha identifier to the patient");
            }
        }

        public async Task<bool> SaveInitiatedLinkRequest(string requestId, string transactionId,
            string linkReferenceNumber)
        {
            var savedLinkRequest = await linkPatientRepository.Save(requestId, transactionId, linkReferenceNumber)
                .ConfigureAwait(false);
            return savedLinkRequest.HasValue;
        }
        public async Task<ErrorRepresentation> VerifyAndLinkCareContexts(String requestId)
        {
            var (linkEnquires, exception) =
                await linkPatientRepository.GetPatientFor(requestId);
            var cmId = "";
            if (exception != null)
                return new ErrorRepresentation(new Error(ErrorCode.NoLinkRequestFound, ErrorMessage.NoLinkRequestFound));
            cmId = linkEnquires.ConsentManagerId;
            var patient = await patientRepository.PatientWithAsync(linkEnquires.PatientReferenceNumber);
            return await patient.Map( async patient =>
                {
                    var savedLinkRequests = await linkPatientRepository.Get(requestId);
                    savedLinkRequests.MatchSome(linkRequests =>
                    {
                        foreach (var linkRequest in linkRequests)
                        {
                            linkRequest.Status = true;
                            linkPatientRepository.Update(linkRequest);
                        }
                    });
                    return await SaveLinkedAccounts(linkEnquires,patient.Uuid)
                        ? (ErrorRepresentation) null
                        : new ErrorRepresentation(new Error(ErrorCode.NoPatientFound,
                                ErrorMessage.NoPatientFound));
                }).ValueOr(
                Task.FromResult<ErrorRepresentation>(new ErrorRepresentation(new Error(ErrorCode.CareContextNotFound,
                        ErrorMessage.CareContextNotFound))));
        }

        /// <summary>
        /// Extracts visit UUID from care context reference number
        /// Care context reference format is typically "patientId:visitUuid"
        /// </summary>
        private string ExtractVisitUuidFromReference(string careContextReference)
        {
            if (string.IsNullOrEmpty(careContextReference))
                return null;

            var parts = careContextReference.Split(':');
            // If reference contains ":", assume format is "patientId:visitUuid" and return the second part
            if (parts.Length >= 2)
            {
                return parts[1];
            }
            // If no ":" found, the reference itself might be the visit UUID
            return careContextReference;
        }

        private async Task<string> SetHfrIdForVisitAsync(string visitUuid)
        {
            try
            {
                if (string.IsNullOrEmpty(visitUuid))
                {
                    Log.Error("SetHfrIdForVisitAsync: visitUuid is null or empty");
                    return null;
                }
                Log.Information($"SetHfrIdForVisitAsync: Retrieving HFR ID for visit UUID: {visitUuid}");
                // Get visit from OpenMRS with full representation to include location details
                var visitPath = $"ws/rest/v1/visit/{visitUuid}";
                var visitResponse = await openMrsClient.GetAsync(visitPath);
                if (visitResponse == null || !visitResponse.IsSuccessStatusCode)
                {
                    Log.Error($"SetHfrIdForVisitAsync: Failed to retrieve visit {visitUuid} from OpenMRS. Status: {visitResponse?.StatusCode}");
                    return null;
                }
                var visitContent = await visitResponse.Content.ReadAsStringAsync();
                var visitJson = JObject.Parse(visitContent);
                // Extract location UUID from visit
                var locationRef = visitJson["location"]?["uuid"]?.ToString();
                if (string.IsNullOrEmpty(locationRef))
                {
                    Log.Error($"SetHfrIdForVisitAsync: Visit {visitUuid} does not have a location");
                    return null;
                }
                Log.Information($"SetHfrIdForVisitAsync: Visit location UUID: {locationRef}");
                // Get location details with attributes to find HFR ID
                var locationPath = $"ws/rest/v1/location/{locationRef}?v=full";
                var locationResponse = await openMrsClient.GetAsync(locationPath);
                if (locationResponse == null || !locationResponse.IsSuccessStatusCode)
                {
                    Log.Error($"SetHfrIdForVisitAsync: Failed to retrieve location {locationRef} from OpenMRS. Status: {locationResponse?.StatusCode}");
                    return null;
                }
                var locationContent = await locationResponse.Content.ReadAsStringAsync();
                var locationJson = JObject.Parse(locationContent);
                // Find HFR ID and HFR Name from location attributes
                string hfrId = null;
                string facilityName = null;
                if (locationJson["attributes"] is JArray attributes)
                {
                    foreach (var attribute in attributes)
                    {
                        var attributeType = attribute["attributeType"]?["display"]?.ToString() ??
                                          attribute["attributeType"]?["name"]?.ToString();
                        if (attributeType != null)
                        {
                            // Extract HFR ID
                            if (attributeType.Equals("ABDM HFR ID", StringComparison.OrdinalIgnoreCase) ||
                                 attributeType.Contains("HFR ID", StringComparison.OrdinalIgnoreCase))
                            {
                                hfrId = attribute["value"]?.ToString();
                                if (!string.IsNullOrEmpty(hfrId))
                                {
                                    Log.Information($"SetHfrIdForVisitAsync: Found HFR ID: {hfrId} for location {locationRef}");
                                }
                            }
                            // Extract ABDM HFR Name
                            if (attributeType.Equals("ABDM HFR Name", StringComparison.OrdinalIgnoreCase) ||
                                attributeType.Contains("HFR Name", StringComparison.OrdinalIgnoreCase))
                            {
                                facilityName = attribute["value"]?.ToString();
                                if (!string.IsNullOrEmpty(facilityName))
                                {
                                    Log.Information($"SetHfrIdForVisitAsync: Found ABDM HFR Name: {facilityName} for location {locationRef}");
                                }
                            }
                        }
                    }
                }
                if (string.IsNullOrEmpty(hfrId))
                {
                    Log.Information($"SetHfrIdForVisitAsync: WARNING - HFR ID not found in location {locationRef} attributes");
                    return null;
                }
                // Store HFR ID in cache for this visit UUID
                bahmniConfiguration.SetHfrIdForVisit(visitUuid, hfrId);
                Log.Information($"SetHfrIdForVisitAsync: Successfully stored HFR ID {hfrId} for visit UUID {visitUuid}");

                // Store facility name in cache for this visit UUID
                bahmniConfiguration.SetFacilityNameForVisit(visitUuid, facilityName);
                Log.Information($"SetHfrIdForVisitAsync: Successfully stored facility name {facilityName} for visit UUID {visitUuid}");
                return hfrId;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"SetHfrIdForVisitAsync: Error processing request: {ex.Message}");
                return null;
            }
        }

    }
}