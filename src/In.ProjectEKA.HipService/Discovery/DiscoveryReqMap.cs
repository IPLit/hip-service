using System.Collections.Concurrent;
using System.Collections.Generic;
using In.ProjectEKA.HipLibrary.Patient.Model;

namespace In.ProjectEKA.HipService.Discovery
{
    public static class DiscoveryReqMap {
        public static ConcurrentDictionary<string, PatientEnquiry> PatientInfoMap = new ConcurrentDictionary<string, PatientEnquiry>();
    }
}