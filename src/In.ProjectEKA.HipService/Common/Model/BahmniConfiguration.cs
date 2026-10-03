using System;
using System.Threading.Tasks;
using In.ProjectEKA.HipService.Logger;
using In.ProjectEKA.HipService.OpenMrs;
using Newtonsoft.Json.Linq;

namespace In.ProjectEKA.HipService.Common.Model
{
    public class BahmniConfiguration
    {
        private readonly HfrIdCache _hfrIdCache;
        private readonly FacilityNameCache _facilityNameCache;
        private readonly IOpenMrsClient _openMrsClient;

        public BahmniConfiguration(IOpenMrsClient openMrsClient)
        {
            _openMrsClient = openMrsClient;
            _hfrIdCache = new HfrIdCache();
            _facilityNameCache = new FacilityNameCache();
        }

        public string Id { get; set; }

        public string Name { get; set; }

        /// <summary>
        /// Gets HFR ID for a visit. Location UUID is always loaded from the OpenMRS visit API,
        /// then HFR ID is read from the location cache.
        /// </summary>
        /// <param name="visitUuid">Visit UUID</param>
        /// <returns>HFR ID for the visit location, default Id when visit UUID is empty, or null on cache miss</returns>
        public async Task<string> GetHfrIdByVisitUuid(string visitUuid)
        {
            if (string.IsNullOrEmpty(visitUuid))
                return Id;

            var locationUuid = await GetLocationUuidForVisitAsync(visitUuid);
            if (string.IsNullOrEmpty(locationUuid))
                return null;

            return _hfrIdCache.GetHfrId(locationUuid);
        }

        public string GetDefaultHfrId()
        {
            return Id;
        }

        /// <summary>
        /// Sets HFR ID for a location UUID in cache
        /// </summary>
        /// <param name="locationUuid">Location UUID</param>
        /// <param name="hfrId">HFR ID to store</param>
        public void SetHfrIdForLocation(string locationUuid, string hfrId)
        {
            _hfrIdCache.SetHfrId(locationUuid, hfrId);
        }

        /// <summary>
        /// Gets facility name for a visit. Location UUID is always loaded from the OpenMRS visit API,
        /// then facility name is read from the location cache.
        /// </summary>
        /// <param name="visitUuid">Visit UUID</param>
        /// <returns>Facility name for the visit location, default Name when visit UUID is empty, or null on cache miss</returns>
        public async Task<string> GetFacilityNameByVisitUuid(string visitUuid)
        {
            if (string.IsNullOrEmpty(visitUuid))
                return Name;

            var locationUuid = await GetLocationUuidForVisitAsync(visitUuid);
            if (string.IsNullOrEmpty(locationUuid))
                return null;

            return _facilityNameCache.GetFacilityName(locationUuid);
        }

        public string GetDefaultFacilityName()
        {
                return Name;
        }

        /// <summary>
        /// Sets facility name for a location UUID in cache
        /// </summary>
        /// <param name="locationUuid">Location UUID</param>
        /// <param name="facilityName">Facility name to store</param>
        public void SetFacilityNameForLocation(string locationUuid, string facilityName)
        {
            if (!string.IsNullOrEmpty(facilityName))
            {
                _facilityNameCache.SetFacilityName(locationUuid, facilityName);
            }
        }

        /// <summary>
        /// Gets the HFR ID cache instance
        /// </summary>
        public HfrIdCache HfrIdCache => _hfrIdCache;

        /// <summary>
        /// Gets the facility name cache instance
        /// </summary>
        public FacilityNameCache FacilityNameCache => _facilityNameCache;

        /// <summary>
        /// Extracts visit UUID from care context reference number.
        /// Care context reference format is typically "patientId:visitUuid".
        /// </summary>
        public string ExtractVisitUuidFromReference(string careContextReference)
        {
            if (string.IsNullOrEmpty(careContextReference))
                return null;

            var parts = careContextReference.Split(':');
            if (parts.Length >= 2)
            {
                return parts[1];
            }
            return careContextReference;
        }

        /// <summary>
        /// Resolves HFR ID for a visit. Uses the location cache first.
        /// Calls the OpenMRS location API only when that location is not cached.
        /// </summary>
        public async Task<string> SetHfrIdForVisitAsync(string visitUuid)
        {
            try
            {
                if (string.IsNullOrEmpty(visitUuid))
                {
                    Log.Error("SetHfrIdForVisitAsync: visitUuid is null or empty");
                    return null;
                }

                var locationUuid = await GetLocationUuidForVisitAsync(visitUuid);
                if (string.IsNullOrEmpty(locationUuid))
                    return null;

                var cachedHfrId = _hfrIdCache.GetHfrId(locationUuid);
                if (!string.IsNullOrEmpty(cachedHfrId))
                {
                    Log.Information($"SetHfrIdForVisitAsync: Using cached HFR ID {cachedHfrId} for location {locationUuid}");
                    return cachedHfrId;
                }

                return await FetchAndCacheLocationDetailsAsync(locationUuid);
            }
            catch (Exception ex)
            {
                Log.Error($"SetHfrIdForVisitAsync: Failed to retrieve HFR ID for visit {visitUuid} from OpenMRS.");
                Log.Error($"SetHfrIdForVisitAsync: Error processing request: {ex.Message}");
                return null;
            }
        }

        private async Task<string> GetLocationUuidForVisitAsync(string visitUuid)
        {
            try
            {
                Log.Information($"SetHfrIdForVisitAsync: Retrieving location for visit UUID: {visitUuid}");
                var visitPath = $"ws/rest/v1/visit/{visitUuid}";
                var visitResponse = await _openMrsClient.GetAsync(visitPath);
                if (visitResponse == null || !visitResponse.IsSuccessStatusCode)
                {
                    Log.Error($"SetHfrIdForVisitAsync: Failed to retrieve visit {visitUuid} from OpenMRS. Status: {visitResponse?.StatusCode}");
                    return null;
                }

                var visitContent = await visitResponse.Content.ReadAsStringAsync();
                var visitJson = JObject.Parse(visitContent);
                var locationUuid = visitJson["location"]?["uuid"]?.ToString();
                if (string.IsNullOrEmpty(locationUuid))
                {
                    Log.Error($"SetHfrIdForVisitAsync: Visit {visitUuid} does not have a location");
                    return null;
                }

                Log.Information($"SetHfrIdForVisitAsync: Visit {visitUuid} location UUID: {locationUuid}");
                return locationUuid;
            }
            catch (Exception ex)
            {
                Log.Error($"SetHfrIdForVisitAsync: Failed to retrieve visit {visitUuid} from OpenMRS.");
                Log.Error($"SetHfrIdForVisitAsync: Error processing request: {ex.Message}");
                return null;
            }
        }

        private async Task<string> FetchAndCacheLocationDetailsAsync(string locationUuid)
        {
            Log.Information($"SetHfrIdForVisitAsync: HFR ID not cached for location {locationUuid}. Calling OpenMRS location API");
            var locationPath = $"ws/rest/v1/location/{locationUuid}?v=full";
            var locationResponse = await _openMrsClient.GetAsync(locationPath);
            if (locationResponse == null || !locationResponse.IsSuccessStatusCode)
            {
                Log.Error($"SetHfrIdForVisitAsync: Failed to retrieve location {locationUuid} from OpenMRS. Status: {locationResponse?.StatusCode}");
                return null;
            }

            var locationContent = await locationResponse.Content.ReadAsStringAsync();
            var locationJson = JObject.Parse(locationContent);
            string hfrId = null;
            string facilityName = null;
            var attributes = locationJson["attributes"] as JArray;
            if (attributes != null)
            {
                foreach (var attribute in attributes)
                {
                    var attributeType = attribute["attributeType"]?["display"]?.ToString() ??
                                      attribute["attributeType"]?["name"]?.ToString();
                    if (attributeType == null)
                        continue;

                    if (attributeType.Equals("ABDM HFR ID", StringComparison.OrdinalIgnoreCase) ||
                        attributeType.Contains("HFR ID", StringComparison.OrdinalIgnoreCase))
                    {
                        hfrId = attribute["value"]?.ToString();
                        if (!string.IsNullOrEmpty(hfrId))
                            Log.Information($"SetHfrIdForVisitAsync: Found HFR ID: {hfrId} for location {locationUuid}");
                    }

                    if (attributeType.Equals("ABDM HFR Name", StringComparison.OrdinalIgnoreCase) ||
                        attributeType.Contains("HFR Name", StringComparison.OrdinalIgnoreCase))
                    {
                        facilityName = attribute["value"]?.ToString();
                        if (!string.IsNullOrEmpty(facilityName))
                            Log.Information($"SetHfrIdForVisitAsync: Found ABDM HFR Name: {facilityName} for location {locationUuid}");
                    }
                }
            }

            if (string.IsNullOrEmpty(hfrId))
            {
                hfrId = GetDefaultHfrId();
                Log.Information($"SetHfrIdForVisitAsync: WARNING - HFR ID not found in location {locationUuid} attributes. Using default {hfrId}");
            }

            if (string.IsNullOrEmpty(facilityName))
                facilityName = GetDefaultFacilityName();

            if (!string.IsNullOrEmpty(hfrId))
            {
                SetHfrIdForLocation(locationUuid, hfrId);
                Log.Information($"SetHfrIdForVisitAsync: Stored HFR ID {hfrId} for location UUID {locationUuid}");
            }

            if (!string.IsNullOrEmpty(facilityName)) {
                SetFacilityNameForLocation(locationUuid, facilityName);
                Log.Information($"SetHfrIdForVisitAsync: Stored facility name {facilityName} for location UUID {locationUuid}");
            }
            return hfrId;
        }
    }
}
