namespace In.ProjectEKA.HipService.UserAuth
{
    public class AuthConfirm
    {
        public string HealthId { get; }
        public string HipId { get; }
        public string AccessToken { get; }
        public string HipName { get; }
        public AuthConfirm(
            string healthId,
            string hipId,
            string accessToken,
            string hipName
        )
        {
            HealthId = healthId;
            HipId = hipId;
            AccessToken = accessToken;
            HipName = hipName;
        }
    }
}