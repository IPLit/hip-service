namespace In.ProjectEKA.HipService.DataFlow
{
    using System;

    public class DataFlowNotificationRequest
    {
        public DataFlowNotificationRequest(string transactionId, string consentId, string doneAt, Notifier notifier,
            StatusNotification statusNotification)
        {
            TransactionId = transactionId;
            ConsentId = consentId;
            DoneAt = doneAt;
            Notifier = notifier;
            StatusNotification = statusNotification;
        }

        public string TransactionId { get; }
        public string ConsentId { get; }
        public string DoneAt { get; }
        public Notifier Notifier { get; }
        public StatusNotification StatusNotification { get; }
    }
}