using System;
using System.Threading.Tasks;
using In.ProjectEKA.HipService.Common;
using In.ProjectEKA.HipService.Common.Model;
using In.ProjectEKA.HipService.Link.Model;
using In.ProjectEKA.HipService.Logger;
using In.ProjectEKA.HipService.OpenMrs;
using In.ProjectEKA.HipService.UserAuth.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace In.ProjectEKA.HipService.Link
{
    using static Constants;

    [ApiController]
    public class CareContextController : Controller
    {
        private readonly ICareContextService careContextService;
        private readonly ILinkPatientRepository linkPatientRepository;
        private readonly IOpenMrsClient openMrsClient;
        private BahmniConfiguration bahmniConfiguration;
        
        public CareContextController(
            ICareContextService careContextService,
            ILinkPatientRepository linkPatientRepository,
            IOpenMrsClient openMrsClient,
            BahmniConfiguration bahmniConfiguration
        )
        {
            this.careContextService = careContextService;
            this.linkPatientRepository = linkPatientRepository;
            this.openMrsClient = openMrsClient;
            this.bahmniConfiguration = bahmniConfiguration;
        }
        
        [Authorize]
        [HttpPost(PATH_ON_NOTIFY_CONTEXTS)]
        public AcceptedResult HipLinkOnNotifyContexts(HipLinkOnNotifyConfirmation confirmation)
        {
            Log.Information("Link on-notify context received.");
            if (confirmation.Error != null)
                Log.Information($" Error Code:{confirmation.Error.Code}," +
                                $" Error Message:{confirmation.Error.Message}");
            else if (confirmation.Acknowledgement != null)
                Log.Information($" Acknowledgment Status:{confirmation.Acknowledgement.Status}");
            Log.Information($" Resp RequestId:{confirmation.Response.RequestId}");
            return Accepted();
        }

        [Route(PATH_NEW_CARECONTEXT)]
        public async Task<ActionResult> PassContext([FromBody] NewContextRequest newContextRequest)
        {
            bool isFirstTime = true;
            var (careContexts, exception) =
                await linkPatientRepository.GetLinkedCareContextsOfPatient(newContextRequest.PatientReferenceNumber);
            foreach (var context in newContextRequest.CareContexts)
            {
                Log.Information("Processing care context for patient: " + newContextRequest.PatientReferenceNumber);
                Log.Information($"context.ReferenceNumber: {context.ReferenceNumber} context.display: {context.Display}");
                if (isFirstTime)
                {
                    isFirstTime = false;
                    var visitUuid = context.ReferenceNumber!=null && context.ReferenceNumber.Split(":").Length >= 2
                        ? context.ReferenceNumber.Split(":")[1] : null;
                    await SetHfrId(visitUuid).ConfigureAwait(false);
                }
                if (careContexts != null && careContextService.IsLinkedContext(careContexts, context.ReferenceNumber))
                {
                    await careContextService.CallNotifyContext(newContextRequest, context);
                }
                else
                {
                    await careContextService.CallAddContext(newContextRequest);
                }
            }
            return StatusCode(StatusCodes.Status200OK);
        }

        [Route(PATH_SET_HFR_ID)]
        public async Task<string> SetHfrId(String visitUuid)
        {
            try
            {
                if (string.IsNullOrEmpty(visitUuid))
                {
                    Log.Error("SetHfrId: visitUuid is null or empty");
                    return null;
                }
                Log.Information($"SetHfrId: Retrieving HFR ID for visit UUID: {visitUuid}");
                // Get visit from OpenMRS with full representation to include location details
                var visitPath = $"ws/rest/v1/visit/{visitUuid}?v=full";
                var visitResponse = await openMrsClient.GetAsync(visitPath);
                if (visitResponse == null || !visitResponse.IsSuccessStatusCode)
                {
                    Log.Error($"SetHfrId: Failed to retrieve visit {visitUuid} from OpenMRS. Status: {visitResponse?.StatusCode}");
                    return null;
                }
                var visitContent = await visitResponse.Content.ReadAsStringAsync();
                var visitJson = JObject.Parse(visitContent);
                // Extract location UUID from visit
                var locationRef = visitJson["location"]?["uuid"]?.ToString();
                if (string.IsNullOrEmpty(locationRef))
                {
                    Log.Error($"SetHfrId: Visit {visitUuid} does not have a location");
                    return null;
                }
                Log.Information($"SetHfrId: Visit location UUID: {locationRef}");
                // Get location details with attributes to find HFR ID
                var locationPath = $"ws/rest/v1/location/{locationRef}?v=full";
                var locationResponse = await openMrsClient.GetAsync(locationPath);
                if (locationResponse == null || !locationResponse.IsSuccessStatusCode)
                {
                    Log.Error($"SetHfrId: Failed to retrieve location {locationRef} from OpenMRS. Status: {locationResponse?.StatusCode}");
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
                                    Log.Information($"SetHfrId: Found HFR ID: {hfrId} for location {locationRef}");
                                }
                            }
                            // Extract ABDM HFR Name
                            if ((attributeType.Equals("ABDM HFR Name", StringComparison.OrdinalIgnoreCase) ||
                                attributeType.Contains("HFR Name", StringComparison.OrdinalIgnoreCase)))
                            {
                                facilityName = attribute["value"]?.ToString();
                                if (!string.IsNullOrEmpty(facilityName))
                                {
                                    Log.Information($"SetHfrId: Found ABDM HFR Name: {facilityName} for location {locationRef}");
                                }
                            }
                        }
                    }
                }
                if (string.IsNullOrEmpty(hfrId))
                {
                    Log.Information($"SetHfrId: WARNING - HFR ID not found in location {locationRef} attributes");
                    return null;
                }
                // Store HFR ID in cache for this visit UUID (existing behavior)
                bahmniConfiguration.SetHfrIdForVisit(visitUuid, hfrId);
                Log.Information($"SetHfrId: Successfully stored HFR ID {hfrId} for visit UUID {visitUuid} in cache");

                // Store facility name in cache for this visit UUID
                bahmniConfiguration.SetFacilityNameForVisit(visitUuid, facilityName);
                Log.Information($"SetHfrId: Successfully stored location info (HFR ID: {hfrId}, Name: {facilityName}) for visit UUID {visitUuid}"); 
                return hfrId;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"SetHfrId: Error processing request: {ex.Message}");
                return null;
            }
        }
    }
}