using System.Threading;

namespace In.ProjectEKA.HipService.Common.Model
{
    public class BahmniConfiguration
    {
        private string _id;
        private readonly object _lockObject = new object();
        private readonly HfrIdCache _hfrIdCache;

        public BahmniConfiguration()
        {
            _hfrIdCache = new HfrIdCache();
        }

        public BahmniConfiguration(HfrIdCache hfrIdCache)
        {
            _hfrIdCache = hfrIdCache ?? new HfrIdCache();
        }

        /// <summary>
        /// Default HFR ID (used as fallback when visit UUID is not provided or not found in cache)
        /// </summary>
        public string Id 
        { 
            get
            {
                lock (_lockObject)
                {
                    return _id;
                }
            }
            set
            {
                lock (_lockObject)
                {
                    _id = value;
                }
            }
        }

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

            return _hfrIdCache.GetHfrIdOrDefault(visitUuid, Id);
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
        /// Gets the HFR ID cache instance
        /// </summary>
        public HfrIdCache HfrIdCache => _hfrIdCache;
    }
}