namespace In.ProjectEKA.HipService.Gateway.Model
{
    using System;
    using HipLibrary.Patient.Model;

    public class GatewayDiscoveryRepresentation
    {
        public GatewayDiscoveryRepresentation(PatientEnquiryRepresentation patient,
            Guid requestId,
            string timestamp,
            string transactionId,
            Error error,
            DiscoveryResponse resp)
        {
            Patient = patient;
            RequestId = requestId;
            Timestamp = timestamp;
            TransactionId = transactionId;
            Error = error;
            Resp = resp;
        }

        public PatientEnquiryRepresentation Patient { get; }
        public Guid RequestId { get; }
        public string Timestamp { get; }
        public string TransactionId { get; }
        public Error Error { get; }
        public Resp Resp { get; }
    }
}