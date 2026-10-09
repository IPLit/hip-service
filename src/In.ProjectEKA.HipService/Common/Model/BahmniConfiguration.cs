using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using In.ProjectEKA.HipService.Logger;
using In.ProjectEKA.HipService.OpenMrs;
using Newtonsoft.Json.Linq;

namespace In.ProjectEKA.HipService.Common.Model
{
    public class HfrDetails
    {
        public HfrDetails(string hfrId, string hfrName)
        {
            HfrId = hfrId;
            HfrName = hfrName;
        }

        public string HfrId { get; }

        public string HfrName { get; }
    }

    public class BahmniConfiguration
    {
        private readonly HfrIdCache _hfrIdCache;
        private readonly FacilityNameCache _facilityNameCache;
        private readonly ConcurrentDictionary<string, string> _visitLocationCache = new ConcurrentDictionary<string, string>();
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
        /// Loads the visit location once, then returns the HFR id and name for that location.
        /// A later call for the same visit or location uses the cache.
        /// HFR id and name are cached when the location has them.
        /// When the location has neither, the configured defaults are cached and both values are returned as null
        /// so the caller applies defaults. A failed lookup also returns nulls and is not cached.
        /// </summary>
        public async Task<HfrDetails> GetHfrDetailsByVisitUuid(string visitUuid)
        {
            try
            {
                if (string.IsNullOrEmpty(visitUuid))
                    return new HfrDetails(null, null);

                var locationUuid = await GetLocationUuidForVisitAsync(visitUuid);
                if (string.IsNullOrEmpty(locationUuid))
                    return new HfrDetails(null, null);

                var cachedHfrId = _hfrIdCache.GetHfrId(locationUuid);
                var cachedHfrName = _facilityNameCache.GetFacilityName(locationUuid);
                if (!string.IsNullOrEmpty(cachedHfrId) || !string.IsNullOrEmpty(cachedHfrName))
                    return new HfrDetails(cachedHfrId, cachedHfrName);

                return await FetchAndCacheLocationDetailsAsync(locationUuid);
            }
            catch (Exception ex)
            {
                Log.Error($"GetHfrDetailsByVisitUuid: Failed to retrieve HFR details for visit {visitUuid} from OpenMRS.");
                Log.Error($"GetHfrDetailsByVisitUuid: Error processing request: {ex.Message}");
                return new HfrDetails(null, null);
            }
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

        private async Task<string> GetLocationUuidForVisitAsync(string visitUuid)
        {
            if (_visitLocationCache.TryGetValue(visitUuid, out var cachedLocationUuid))
                return cachedLocationUuid;

            try
            {
                Log.Information($"GetHfrDetailsByVisitUuid: Retrieving location for visit UUID: {visitUuid}");
                var visitPath = $"ws/rest/v1/visit/{visitUuid}";
                var visitResponse = await _openMrsClient.GetAsync(visitPath);
                if (visitResponse == null || !visitResponse.IsSuccessStatusCode)
                {
                    Log.Error($"GetHfrDetailsByVisitUuid: Failed to retrieve visit {visitUuid} from OpenMRS. Status: {visitResponse?.StatusCode}");
                    return null;
                }

                var visitContent = await visitResponse.Content.ReadAsStringAsync();
                var visitJson = JObject.Parse(visitContent);
                var locationUuid = visitJson["location"]?["uuid"]?.ToString();
                if (string.IsNullOrEmpty(locationUuid))
                {
                    Log.Error($"GetHfrDetailsByVisitUuid: Visit {visitUuid} does not have a location");
                    return null;
                }

                _visitLocationCache.TryAdd(visitUuid, locationUuid);
                Log.Information($"GetHfrDetailsByVisitUuid: Visit {visitUuid} location UUID: {locationUuid}");
                return locationUuid;
            }
            catch (Exception ex)
            {
                Log.Error($"GetHfrDetailsByVisitUuid: Failed to retrieve visit {visitUuid} from OpenMRS.");
                Log.Error($"GetHfrDetailsByVisitUuid: Error processing request: {ex.Message}");
                return null;
            }
        }

        private async Task<HfrDetails> FetchAndCacheLocationDetailsAsync(string locationUuid)
        {
            Log.Information($"GetHfrDetailsByVisitUuid: HFR details not cached for location {locationUuid}. Calling OpenMRS location API");
            var locationPath = $"ws/rest/v1/location/{locationUuid}?v=full";
            var locationResponse = await _openMrsClient.GetAsync(locationPath);
            if (locationResponse == null || !locationResponse.IsSuccessStatusCode)
            {
                Log.Error($"GetHfrDetailsByVisitUuid: Failed to retrieve location {locationUuid} from OpenMRS. Status: {locationResponse?.StatusCode}");
                return new HfrDetails(null, null);
            }

            var locationContent = await locationResponse.Content.ReadAsStringAsync();
            var locationJson = JObject.Parse(locationContent);
            string hfrId = null;
            string hfrName = null;
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
                            Log.Information($"GetHfrDetailsByVisitUuid: Found HFR ID: {hfrId} for location {locationUuid}");
                    }

                    if (attributeType.Equals("ABDM HFR Name", StringComparison.OrdinalIgnoreCase) ||
                        attributeType.Contains("HFR Name", StringComparison.OrdinalIgnoreCase))
                    {
                        hfrName = attribute["value"]?.ToString();
                        if (!string.IsNullOrEmpty(hfrName))
                            Log.Information($"GetHfrDetailsByVisitUuid: Found ABDM HFR Name: {hfrName} for location {locationUuid}");
                    }
                }
            }

            if (string.IsNullOrEmpty(hfrId) && string.IsNullOrEmpty(hfrName))
            {
                CacheDefaultHfrDetails(locationUuid);
                Log.Information($"GetHfrDetailsByVisitUuid: Location {locationUuid} has no HFR id or name. Cached defaults and returning null HFR id");
                return new HfrDetails(null, null);
            }

            if (!string.IsNullOrEmpty(hfrId))
            {
                SetHfrIdForLocation(locationUuid, hfrId);
                Log.Information($"GetHfrDetailsByVisitUuid: Stored HFR ID {hfrId} for location UUID {locationUuid}");
            }

            if (!string.IsNullOrEmpty(hfrName))
            {
                SetFacilityNameForLocation(locationUuid, hfrName);
                Log.Information($"GetHfrDetailsByVisitUuid: Stored HFR name {hfrName} for location UUID {locationUuid}");
            }

            return new HfrDetails(hfrId, hfrName);
        }

        private void CacheDefaultHfrDetails(string locationUuid)
        {
            var defaultHfrId = GetDefaultHfrId();
            var defaultHfrName = GetDefaultFacilityName();
            if (!string.IsNullOrEmpty(defaultHfrId))
                SetHfrIdForLocation(locationUuid, defaultHfrId);
            if (!string.IsNullOrEmpty(defaultHfrName))
                SetFacilityNameForLocation(locationUuid, defaultHfrName);
        }
    }
}
