using System;
using System.Collections.Concurrent;

namespace In.ProjectEKA.HipService.Common.Model
{
    /// <summary>
    /// Thread-safe in-memory cache for storing HFR ID per location UUID.
    /// Many visits share a location, so the HFR ID is cached once per location.
    /// </summary>
    public class HfrIdCache
    {
        private readonly ConcurrentDictionary<string, string> _locationUuidToHfrId = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// Stores HFR ID for a given location UUID
        /// </summary>
        public void SetHfrId(string locationUuid, string hfrId)
        {
            if (string.IsNullOrEmpty(locationUuid))
                throw new ArgumentException("Location UUID cannot be null or empty", nameof(locationUuid));
            if (string.IsNullOrEmpty(hfrId))
                throw new ArgumentException("HFR ID cannot be null or empty", nameof(hfrId));

            _locationUuidToHfrId.AddOrUpdate(locationUuid, hfrId, (key, oldValue) => hfrId);
        }

        /// <summary>
        /// Gets HFR ID for a given location UUID
        /// </summary>
        /// <returns>HFR ID if found, null otherwise</returns>
        public string GetHfrId(string locationUuid)
        {
            if (string.IsNullOrEmpty(locationUuid))
                return null;

            _locationUuidToHfrId.TryGetValue(locationUuid, out var hfrId);
            return hfrId;
        }

        /// <summary>
        /// Gets HFR ID for a given location UUID, or returns default HFR ID if not found
        /// </summary>
        public string GetHfrIdOrDefault(string locationUuid, string defaultHfrId)
        {
            var hfrId = GetHfrId(locationUuid);
            return hfrId ?? defaultHfrId;
        }

        /// <summary>
        /// Removes HFR ID for a given location UUID
        /// </summary>
        public bool RemoveHfrId(string locationUuid)
        {
            if (string.IsNullOrEmpty(locationUuid))
                return false;

            return _locationUuidToHfrId.TryRemove(locationUuid, out _);
        }

        /// <summary>
        /// Clears all cached HFR IDs
        /// </summary>
        public void Clear()
        {
            _locationUuidToHfrId.Clear();
        }

        /// <summary>
        /// Gets the count of cached entries
        /// </summary>
        public int Count => _locationUuidToHfrId.Count;
    }
}
