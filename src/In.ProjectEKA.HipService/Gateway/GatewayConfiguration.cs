namespace In.ProjectEKA.HipService.Gateway
{
    public class GatewayConfiguration
    {
        public string Url { get; set; }

        public string SessionM1GatewayUrl { get; set; }
        
        public int TimeOut { get; set; }
        
        public int Counter { get; set; }
        public string ClientId { get; set; }

        public string ClientSecret { get; set; }

        public string CmSuffix { get; set; }
        
        public string AbhaNumberServiceUrl { get; set; }
        
        public string AbhaAddressServiceUrl { get; set; }
        
        public string BenefitName { get; set; }
    }
}