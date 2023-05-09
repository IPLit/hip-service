namespace In.ProjectEKA.HipService.DataFlow
{
    using System.Collections.Generic;

    public class StatusNotification
    {
        public StatusNotification(SessionStatus sessionStatus, string hipId, IEnumerable<StatusResponse> statusResponses)
        {
            SessionStatus ss = (SessionStatus) sessionStatus;
            SessionStatus = ss.ToString();
            HipId = hipId;
            StatusResponses = statusResponses;
        }

        public string SessionStatus { get; }
        public string HipId { get; }
        public IEnumerable<StatusResponse> StatusResponses { get; }
    }
}