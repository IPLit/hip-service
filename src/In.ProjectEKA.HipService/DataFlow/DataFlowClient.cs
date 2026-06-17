using In.ProjectEKA.HipService.Common;

namespace In.ProjectEKA.HipService.DataFlow
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Mime;

    using System.Threading.Tasks;
    using Gateway;
    using HipLibrary.Patient.Model;
    using In.ProjectEKA.HipService.OpenMrs;

    using Logger;
    using Microsoft.Net.Http.Headers;

    using Model;
    using Newtonsoft.Json.Linq;

    using RabbitMQ.Client;

    using static Common.HttpRequestHelper;

    public class DataFlowClient
    {
        private readonly IOpenMrsClient openMrsClient;
        private readonly DataFlowNotificationClient dataFlowNotificationClient;
        private readonly GatewayConfiguration gatewayConfiguration;
        private HipService.Common.Model.BahmniConfiguration bahmniConfiguration;
        private readonly HttpClient httpClient;
        private readonly GatewayClient gatewayClient;
        private static readonly string MEDIA_APPLICATION_FHIR_JSON = "application/fhir+json";

        public DataFlowClient(HttpClient httpClient,
            DataFlowNotificationClient dataFlowNotificationClient,
            GatewayConfiguration gatewayConfiguration,
            HipService.Common.Model.BahmniConfiguration bahmniConfiguration,
            GatewayClient gatewayClient)
        {
            this.gatewayClient = gatewayClient;
            this.httpClient = httpClient;
            this.dataFlowNotificationClient = dataFlowNotificationClient;
            this.gatewayConfiguration = gatewayConfiguration;
            this.bahmniConfiguration = bahmniConfiguration;
        }

        public virtual async Task SendDataToHiu(TraceableDataRequest dataRequest,
            IEnumerable<Entry> data,
            KeyMaterial keyMaterial)
        {
            await PostTo(dataRequest.ConsentId,
                dataRequest.DataPushUrl,
                dataRequest.CareContexts,
                new DataResponse(dataRequest.TransactionId, data, keyMaterial),
                dataRequest.CmSuffix,
                dataRequest.CorrelationId).ConfigureAwait(false);
        }

        private async Task PostTo(string consentId,
            string dataPushUrl,
            IEnumerable<GrantedContext> careContexts,
            DataResponse dataResponse,
            string cmSuffix,
            string correlationId)
        {
            var grantedContexts = careContexts as GrantedContext[] ?? careContexts.ToArray();
            var hiStatus = HiStatus.DELIVERED;
            var sessionStatus = SessionStatus.TRANSFERRED;
            var message = "Successfully delivered health information";
            var requestId = Guid.NewGuid();
            // Extract visit UUID from first care context (format: "patientId:visitUuid")
            var visitUuid = grantedContexts.First() != null 
                ? ExtractVisitUuidFromReference(grantedContexts.First().CareContextReference)
                : null;
            string hipId = bahmniConfiguration.GetHfrIdByVisitUuid(visitUuid);
            try
            {
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

                // TODO: Need to handle non 2xx response also
                httpClient.DefaultRequestHeaders.Remove("Authorization");
                var token = await gatewayClient.Authenticate(correlationId).ConfigureAwait(false);
                if (token.HasValue)
                {
                    var reqDataPush = HttpRequestHelper.CreateHttpRequestWithContentType(HttpMethod.Post, dataPushUrl, dataResponse,
                        token.ValueOr(String.Empty), cmSuffix, correlationId,
                        MediaTypeNames.Application.Json, hipId, requestId.ToString(), null,
                        null, null, dataResponse.TransactionId);
                    await httpClient.SendAsync(reqDataPush).ConfigureAwait(false);
                }
                else
                {
                    hiStatus = HiStatus.ERRORED;
                    sessionStatus = SessionStatus.FAILED;
                    message = "Failed to deliver health information";
                    Log.Error($"Failed to authenticate while delivering health information to {dataPushUrl}");
                }
            }
            catch (Exception exception)
            {
                hiStatus = HiStatus.ERRORED;
                sessionStatus = SessionStatus.FAILED;
                message = "Failed to deliver health information";
                Log.Error(exception, exception.StackTrace);
            }

            try
            {
                var statusResponses = grantedContexts
                    .Select(grantedContext =>
                        new StatusResponse(grantedContext.CareContextReference, hiStatus,
                            message))
                    .ToList();
                var dataNotificationRequest = new DataNotificationRequest(dataResponse.TransactionId,
                    DateTime.Now.ToUniversalTime().ToString(Common.Constants.DateTimeFormat),
                    new Notifier(Type.HIP, hipId),
                    new StatusNotification(sessionStatus, hipId, statusResponses),
                    consentId,
                    requestId);
                await GetDataNotificationRequest(dataNotificationRequest, cmSuffix, correlationId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }
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

        private async Task GetDataNotificationRequest(DataNotificationRequest dataNotificationRequest,
            string cmSuffix,
            string correlationId)
        {
            await dataFlowNotificationClient.NotifyGateway(cmSuffix, dataNotificationRequest, correlationId);
        }

        /// <summary>
        /// Extracts visit UUID from care context reference number
        /// Care context reference format is typically "patientId:visitUuid"
        /// </summary>
        private string ExtractVisitUuidFromReference(string careContextReference)
        {
            if (string.IsNullOrEmpty(careContextReference))
                return null;
            Log.Information($"ExtractVisitUuidFromReference: Care context reference: {careContextReference}");
            var parts = careContextReference.Split(':');
            // If reference contains ":", assume format is "patientId:visitUuid" and return the second part
            if (parts.Length >= 2)
            {
                return parts[1];
            }
            // If no ":" found, the reference itself might be the visit UUID
            return careContextReference;
        }
    }
}