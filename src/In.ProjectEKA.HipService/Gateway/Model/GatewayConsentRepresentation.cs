namespace In.ProjectEKA.HipService.Gateway.Model
{
    using System;
    using Common.Model;
    using HipLibrary.Patient.Model;

    public class GatewayConsentRepresentation
    {
        public GatewayConsentRepresentation(Guid requestId, string timestamp,
            ConsentUpdateResponse acknowledgement, Error error, Resp resp)
        {
            RequestId = requestId;
            Timestamp = timestamp;
            Acknowledgement = acknowledgement;
            Resp = resp;
            Error = error;
        }

        public Guid RequestId { get; }
        public string Timestamp { get; }
        public ConsentUpdateResponse Acknowledgement { get; }
        public Resp Resp { get; }
        public Error Error { get; }
    }
}