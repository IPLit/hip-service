namespace In.ProjectEKA.HipService.Gateway
{
    public class GatewayConfiguration
    {
        public string Url { get; set; }
        
        public int TimeOut { get; set; }
        
        public int Counter { get; set; }
        public string ClientId { get; set; }

        public string ClientSecret { get; set; }

        public string CmSuffix { get; set; }
        
        public string AbhaNumberServiceUrl { get; set; }
        
        public string AbhaAddressServiceUrl { get; set; }
        
        public string BenefitName { get; set; }

        /// <summary>
        /// Minutes before <c>expiresIn</c> at which the cached gateway session token is renewed.
        /// </summary>
        public int SessionTokenRenewalBufferMinutes { get; set; } = 2;
    }
}