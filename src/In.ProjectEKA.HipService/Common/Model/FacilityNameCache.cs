using System;
using System.Collections.Concurrent;

namespace In.ProjectEKA.HipService.Common.Model
{
    /// <summary>
    /// Thread-safe in-memory cache for storing facility name per location UUID.
    /// Many visits share a location, so the facility name is cached once per location.
    /// </summary>
    public class FacilityNameCache
    {
        private readonly ConcurrentDictionary<string, string> _locationUuidToFacilityName = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// Stores facility name for a given location UUID
        /// </summary>
        public void SetFacilityName(string locationUuid, string facilityName)
        {
            if (string.IsNullOrEmpty(locationUuid))
                throw new ArgumentException("Location UUID cannot be null or empty", nameof(locationUuid));
            if (string.IsNullOrEmpty(facilityName))
                throw new ArgumentException("Facility name cannot be null or empty", nameof(facilityName));

            _locationUuidToFacilityName.AddOrUpdate(locationUuid, facilityName, (key, oldValue) => facilityName);
        }

        /// <summary>
        /// Gets facility name for a given location UUID
        /// </summary>
        /// <returns>Facility name if found, null otherwise</returns>
        public string GetFacilityName(string locationUuid)
        {
            if (string.IsNullOrEmpty(locationUuid))
                return null;

            _locationUuidToFacilityName.TryGetValue(locationUuid, out var facilityName);
            return facilityName;
        }

        /// <summary>
        /// Gets facility name for a given location UUID, or returns default facility name if not found
        /// </summary>
        public string GetFacilityNameOrDefault(string locationUuid, string defaultFacilityName)
        {
            var facilityName = GetFacilityName(locationUuid);
            return facilityName ?? defaultFacilityName;
        }

        /// <summary>
        /// Removes facility name for a given location UUID
        /// </summary>
        public bool RemoveFacilityName(string locationUuid)
        {
            if (string.IsNullOrEmpty(locationUuid))
                return false;

            return _locationUuidToFacilityName.TryRemove(locationUuid, out _);
        }

        /// <summary>
        /// Clears all cached facility names
        /// </summary>
        public void Clear()
        {
            _locationUuidToFacilityName.Clear();
        }

        /// <summary>
        /// Gets the count of cached entries
        /// </summary>
        public int Count => _locationUuidToFacilityName.Count;
    }
}
