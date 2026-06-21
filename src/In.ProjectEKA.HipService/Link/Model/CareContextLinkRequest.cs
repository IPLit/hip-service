namespace In.ProjectEKA.HipService.Link.Model
{
    public class CareContextLinkRequest
    {
        public CareContextLinkRequest(string referenceNumber, string display, string hiType)
        {
            ReferenceNumber = referenceNumber;
            Display = display;
            HiType = hiType;
        }

        public string ReferenceNumber { get; }

        public string Display { get; }

        public string HiType { get; }
    }
}
