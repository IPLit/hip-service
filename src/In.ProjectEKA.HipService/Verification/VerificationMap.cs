using System.Collections.Concurrent;

namespace In.ProjectEKA.HipService.Verification
{
    public static class VerificationMap
    {
        public static ConcurrentDictionary<string, string> TxnDictionary = new ConcurrentDictionary<string, string>();
    }
}
