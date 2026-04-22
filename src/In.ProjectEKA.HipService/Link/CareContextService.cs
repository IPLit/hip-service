using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using In.ProjectEKA.HipLibrary.Patient.Model;
using In.ProjectEKA.HipService.Common;
using In.ProjectEKA.HipService.Common.Model;
using In.ProjectEKA.HipService.Gateway;
using In.ProjectEKA.HipService.Link.Model;
using In.ProjectEKA.HipService.Logger;
using In.ProjectEKA.HipService.OpenMrs;
using In.ProjectEKA.HipService.UserAuth;
using In.ProjectEKA.HipService.UserAuth.Model;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Optional.Unsafe;
using HiType = In.ProjectEKA.HipLibrary.Patient.Model.HiType;

namespace In.ProjectEKA.HipService.Link
{
    using static Constants;

    public class CareContextService : ICareContextService
    {
        private readonly HttpClient httpClient;
        private readonly IUserAuthRepository userAuthRepository;
        private readonly IUserAuthService  userAuthService;
        private readonly BahmniConfiguration bahmniConfiguration;
        private readonly ILinkPatientRepository linkPatientRepository;
        private readonly LinkPatient linkPatient;
        private readonly IOptions<HipConfiguration> hipConfiguration;
        private readonly IGatewayClient gatewayClient;
        private readonly GatewayConfiguration gatewayConfiguration;
        private readonly IOpenMrsClient openMrsClient;
        public CareContextService(HttpClient httpClient, IUserAuthRepository userAuthRepository,
            BahmniConfiguration bahmniConfiguration, ILinkPatientRepository linkPatientRepository, LinkPatient linkPatient, IOptions<HipConfiguration> hipConfiguration, IGatewayClient gatewayClient, GatewayConfiguration gatewayConfiguration,
            IUserAuthService userAuthService, IOpenMrsClient openMrsClient)
        {
            this.httpClient = httpClient;
            this.userAuthRepository = userAuthRepository;
            this.bahmniConfiguration = bahmniConfiguration;
            this.linkPatientRepository = linkPatientRepository;
            this.linkPatient = linkPatient;
            this.hipConfiguration = hipConfiguration;
            this.gatewayClient = gatewayClient;
            this.gatewayConfiguration = gatewayConfiguration;
            this.userAuthService = userAuthService;
            this.openMrsClient = openMrsClient;
        }

        public async Task<Tuple<GatewayAddContextsRequestRepresentation, ErrorRepresentation>> AddContextsResponse(
            NewContextRequest addContextsRequest, string cmSuffix, Guid requestId)
        {
            var careContexts = addContextsRequest.CareContexts;
            var abhaAddress = addContextsRequest.HealthId;
            
            if (!await linkPatient.SaveInitiatedLinkRequest(requestId.ToString(), null, requestId.ToString())
                .ConfigureAwait(false))
                return new Tuple<GatewayAddContextsRequestRepresentation, ErrorRepresentation>
                    (null, new ErrorRepresentation(new Error(ErrorCode.DuplicateRequestId, ErrorMessage.DuplicateRequestId)));
            var careContextReferenceNumbers = addContextsRequest.CareContexts
                .Select(context => context.ReferenceNumber)
                .ToArray();
            var linkConfirmationRepresentations = careContexts
                .Where(cc => cc.HiTypes != null && cc.HiTypes.Any())
                .SelectMany(cc => cc.HiTypes.Select(hiType => new { HiType = hiType, CareContext = cc }))
                .GroupBy(x => x.HiType)
                .Select(group => new LinkConfirmationRepresentation(addContextsRequest.PatientReferenceNumber,
                    addContextsRequest.PatientName,
                    group.Select(x => new CareContextRepresentation(x.CareContext.ReferenceNumber, x.CareContext.Display))
                        .ToList(),
                    group.Key.ToString(),
                    group.Count()))
                .ToList();
            var (_, exception1) = await linkPatientRepository.SaveRequestWith(
                    requestId.ToString(),
                    cmSuffix,
                    abhaAddress,
                    addContextsRequest.PatientReferenceNumber,
                    careContextReferenceNumbers)
                .ConfigureAwait(false);
            if (exception1 != null)
                return new Tuple<GatewayAddContextsRequestRepresentation, ErrorRepresentation>
                (null, new ErrorRepresentation(new Error(ErrorCode.ServerInternalError,
                    ErrorMessage.DatabaseStorageError)));
            return new Tuple<GatewayAddContextsRequestRepresentation, ErrorRepresentation>
                (new GatewayAddContextsRequestRepresentation( abhaAddress,linkConfirmationRepresentations), null);
        }
        
        public async Task SetAccessToken(string healthId, string hipId)
        {
            if (UserAuthMap.HealthIdToAccessToken.ContainsKey(healthId))
            {
                var linkToken = UserAuthMap.HealthIdToAccessToken[healthId];
                var error = userAuthService.CheckAccessToken(linkToken);
                if (error == null)
                    return;
            }
            var (linkTokenFromDb,exception) = await userAuthRepository.GetAccessToken(healthId);
            if (linkTokenFromDb != null)
            {
                 var error = userAuthService.CheckAccessToken(linkTokenFromDb);
                 if (error == null)
                 {
                     UserAuthMap.HealthIdToAccessToken.Add(healthId, linkTokenFromDb);
                     return;
                 }
            }

            var demographics = (userAuthRepository.GetDemographics(healthId).Result).ValueOrDefault();
            var requestId = Guid.NewGuid();
            if (demographics == null)
                return;

            // Log.Information("PATH_GENERATE_TOKEN request params: HealthId {0}, Name {1}, Gender {2}, DateOfBirth {3}", 
                // demographics.HealthId, demographics.Name, demographics.Gender, demographics.DateOfBirth);

            var generateTokenPayload = new GenerateLinkTokenRequest(demographics.HealthId, demographics.Name,
                demographics.Gender, demographics.DateOfBirth.Split("-").First());
            
            await gatewayClient.SendDataToGateway(PATH_GENERATE_TOKEN, generateTokenPayload, gatewayConfiguration.CmSuffix,
                Guid.NewGuid().ToString(), hipId:hipId, requestId.ToString());
            var i = 0;
            do
            {
                await Task.Delay(gatewayConfiguration.TimeOut + 8000);
                if (UserAuthMap.RequestIdToErrorMessage.ContainsKey(requestId))
                {
                    var gatewayError = UserAuthMap.RequestIdToErrorMessage[requestId];
                    UserAuthMap.RequestIdToErrorMessage.Remove(requestId);
                    break;
                }

                if (UserAuthMap.RequestIdToAccessToken.ContainsKey(requestId))
                {
                    Log.Information(
                        "Response about to be send for requestId: {RequestId} with accessToken: {AccessToken}",
                        requestId, UserAuthMap.RequestIdToAccessToken[requestId]
                    );
                    break;
                }
                i++;
            } while (i < gatewayConfiguration.Counter);
        }

        public Tuple<GatewayNotificationContextRepresentation, ErrorRepresentation> NotificationContextResponse(
            NewContextRequest notifyContextRequest, CareContextRepresentation context)
        {
            var id = notifyContextRequest.HealthId;
            var patientReference = notifyContextRequest.PatientReferenceNumber;
            var careContextReference = context.ReferenceNumber;
            var hiTypes = context.HiTypes.Select(hiType => hiType.ToString()).ToList();
            // Extract visit UUID from care context reference (format: "patientId:visitUuid")
            var visitUuid = ExtractVisitUuidFromReference(careContextReference);
            var hipId = bahmniConfiguration.GetHfrIdByVisitUuid(visitUuid);
            if (string.IsNullOrEmpty(hipId))
            {
                hipId = bahmniConfiguration.GetDefaultHfrId();
            }
            var patient = new NotificationPatientContext(id);
            var careContext = new NotificationCareContext(patientReference, careContextReference);
            var hip = new NotificationContextHip(hipId);
            var date = DateTime.Now.ToUniversalTime().ToString(DateTimeFormat);
            var notification = new NotificationContext(patient, careContext, hiTypes, date, hip);
            return new Tuple<GatewayNotificationContextRepresentation, ErrorRepresentation>
                (new GatewayNotificationContextRepresentation(notification), null);
        }

        public async Task CallNotifyContext(NewContextRequest newContextRequest, CareContextRepresentation context)
        {
            var (gatewayNotificationContextRepresentation, error) =
                NotificationContextResponse(newContextRequest, context);
            if (error != null)
                Log.Error("Notify for Care Context failed with error: {@Error}", error);
            
            var cmSuffix = gatewayConfiguration.CmSuffix;
            // Extract visit UUID from care context reference
            var visitUuid = ExtractVisitUuidFromReference(context.ReferenceNumber);
            var hipId = bahmniConfiguration.GetHfrIdByVisitUuid(visitUuid);
            if (string.IsNullOrEmpty(hipId))
            {
                hipId = bahmniConfiguration.GetDefaultHfrId();
            }
            try
            {
                Log.Information(
                    "Request for notification-contexts to gateway: {@GatewayResponse}",
                    gatewayNotificationContextRepresentation.dump(gatewayNotificationContextRepresentation));
                await gatewayClient.SendDataToGateway(PATH_NOTIFY_PATIENT_CONTEXTS,
                    gatewayNotificationContextRepresentation,
                    cmSuffix, Guid.NewGuid().ToString(), hipId:hipId);
            }
            catch (Exception exception)
            {
                Log.Error("Error happened for notification-care context request", exception);
            }
        }

        public async Task CallAddContext(NewContextRequest newContextRequest)
        {
            var abhaAddress = newContextRequest.HealthId;
            // Extract visit UUID from first care context (if available)
            var visitUuid = newContextRequest.CareContexts?.First() != null 
                ? ExtractVisitUuidFromReference(newContextRequest.CareContexts.First().ReferenceNumber)
                : null;
            string hipId = bahmniConfiguration.GetHfrIdByVisitUuid(visitUuid);
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
            await SetAccessToken(abhaAddress, hipId);
            if (!UserAuthMap.HealthIdToAccessToken.ContainsKey(abhaAddress))
            {
                Log.Error("Unable to get link token for healthId: {healthId}",
                    abhaAddress);
                throw new Exception("Unable to get link token");
            }
            var linkToken = UserAuthMap.HealthIdToAccessToken[abhaAddress];
            var cmSuffix = gatewayConfiguration.CmSuffix;
            var requestId = Guid.NewGuid();
            var (gatewayAddContextsRequestRepresentation, error) =
                await AddContextsResponse(newContextRequest,cmSuffix,requestId);
            if (error != null)
                Log.Error("Linking Care Context failed with error: {@Error}", error);
            try
            {
                Log.Information(
                    "Request for add-context to gateway: {@GatewayResponse}",
                    gatewayAddContextsRequestRepresentation.dump(gatewayAddContextsRequestRepresentation));
                await gatewayClient.SendDataToGateway(PATH_ADD_PATIENT_CONTEXTS,
                    gatewayAddContextsRequestRepresentation,
                    cmSuffix, null, hipId:hipId, linkToken:linkToken, requestId: requestId.ToString());
            }
            catch (Exception exception)
            {
                Log.Error("Error happened for add-care context request", exception);
            }
        }

        public bool IsLinkedContext(List<string> careContexts, string context)
        {
            return careContexts.Any(careContext => careContext.Equals(context));
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
                var visitPath = $"ws/rest/v1/visit/{visitUuid}?v=full";
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
                var attributes = locationJson["attributes"] as JArray;
                if (attributes != null)
                {
                    foreach (var attribute in attributes)
                    {
                        var attributeType = attribute["attributeType"]?["display"]?.ToString() ?? 
                                          attribute["attributeType"]?["name"]?.ToString();
                        if (attributeType != null)
                        {
                            // Extract HFR ID
                            if ((attributeType.Equals("ABDM HFR ID", StringComparison.OrdinalIgnoreCase) ||
                                 attributeType.Contains("HFR ID", StringComparison.OrdinalIgnoreCase)))
                            {
                                hfrId = attribute["value"]?.ToString();
                                if (!string.IsNullOrEmpty(hfrId))
                                {
                                    Log.Information($"SetHfrIdForVisitAsync: Found HFR ID: {hfrId} for location {locationRef}");
                                }
                            }
                            // Extract ABDM HFR Name
                            if ((attributeType.Equals("ABDM HFR Name", StringComparison.OrdinalIgnoreCase) ||
                                attributeType.Contains("HFR Name", StringComparison.OrdinalIgnoreCase)))
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