namespace In.ProjectEKA.HipService.DataFlow.Model
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using Hl7.Fhir.Model;

    public class DataNotificationRequest
    {
        [Key] public string TransactionId { get; }
        public string ConsentId { get; }
        public DateTime DoneAt { get; }
        public Notifier Notifier { get; }
        public StatusNotification StatusNotification { get; }

        public DataNotificationRequest()
        {
        }
        
        public DataNotificationRequest(
            string transactionId,
            DateTime doneAt,
            Notifier notifier,
            StatusNotification statusNotification,
            string consentId)
        {
            TransactionId = transactionId;
            DoneAt = doneAt;
            Notifier = notifier;
            StatusNotification = statusNotification;
            ConsentId = consentId;
        }
    }
}