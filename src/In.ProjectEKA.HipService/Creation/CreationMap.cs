using System.Collections.Concurrent;
using System.Collections.Generic;
using In.ProjectEKA.HipService.Creation.Model;

namespace In.ProjectEKA.HipService.Creation
{
    public static class CreationMap
    {
        public static ConcurrentDictionary<string, string> TxnDictionary = new ConcurrentDictionary<string, string>();
        
        public static ConcurrentDictionary<string, string> HealthIdNumberDictionary = new ConcurrentDictionary<string, string>();
        
        public static ConcurrentDictionary<string, TokenRequest> HealthIdNumberTokenDictionary = new ConcurrentDictionary<string, TokenRequest>();
        
        public static ConcurrentDictionary<string, string> HealthIdTokenDictionary = new ConcurrentDictionary<string, string>();
        
        public static ConcurrentDictionary<string, string> VerifiedMobileTokenDictionary = new ConcurrentDictionary<string, string>();
        
        public static ConcurrentDictionary<string, List<string>> HealthIdLoginScopeDictionary = new ConcurrentDictionary<string, List<string>>();
    }
}