using System.Threading;

namespace In.ProjectEKA.HipService.Common.Model
{
    public class BahmniConfiguration
    {
        private readonly HfrIdCache _hfrIdCache;
        private readonly FacilityNameCache _facilityNameCache;

        public BahmniConfiguration()
        {
            _hfrIdCache = new HfrIdCache();
            _facilityNameCache = new FacilityNameCache();
        }

        public string Id { get; set; }

        public string Name { get; set; }

        /// <summary>
        /// Gets HFR ID for a specific visit UUID from cache
        /// </summary>
        /// <param name="visitUuid">Visit UUID</param>
        /// <returns>HFR ID for the visit, or default Id if not found</returns>
        public string GetHfrIdByVisitUuid(string visitUuid)
        {
            if (string.IsNullOrEmpty(visitUuid))
                return Id;

            return _hfrIdCache.GetHfrId(visitUuid);
        }

        public string GetDefaultHfrId()
        {
            return Id;
        }

        /// <summary>
        /// Sets HFR ID for a specific visit UUID in cache
        /// </summary>
        /// <param name="visitUuid">Visit UUID</param>
        /// <param name="hfrId">HFR ID to store</param>
        public void SetHfrIdForVisit(string visitUuid, string hfrId)
        {
            _hfrIdCache.SetHfrId(visitUuid, hfrId);
        }

        /// <summary>
        /// Gets facility name for a specific visit UUID from cache
        /// </summary>
        /// <param name="visitUuid">Visit UUID</param>
        /// <returns>Facility name for the visit, or default Name if not found</returns>
        public string GetFacilityNameByVisitUuid(string visitUuid)
        {
            if (string.IsNullOrEmpty(visitUuid))
                return Name;

            return _facilityNameCache.GetFacilityName(visitUuid);
        }

        public string GetDefaultFacilityName()
        {
                return Name;
        }

        /// <summary>
        /// Sets facility name for a specific visit UUID in cache
        /// </summary>
        /// <param name="visitUuid">Visit UUID</param>
        /// <param name="facilityName">Facility name to store</param>
        public void SetFacilityNameForVisit(string visitUuid, string facilityName)
        {
            if (!string.IsNullOrEmpty(facilityName))
            {
                _facilityNameCache.SetFacilityName(visitUuid, facilityName);
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
    }
}