namespace In.ProjectEKA.HipService.DataFlow
{
    public class StatusResponse
    {
        public StatusResponse(string careContextReference, HiStatus hiStatus, string description)
        {
            CareContextReference = careContextReference;
            HiStatus hs = (HiStatus) hiStatus;
            HiStatus = hs.ToString();
            Description = description;
        }

        public string CareContextReference { get; }
        public string HiStatus { get; }
        public string Description { get; }
    }
}