using System;
using In.ProjectEKA.HipLibrary.Patient.Model;
using In.ProjectEKA.HipService.Common;
using In.ProjectEKA.HipService.Common.Model;
using In.ProjectEKA.HipService.SmsNotification.Model;

namespace In.ProjectEKA.HipService.SmsNotification
{
    using static Constants;
    public class SmsNotificationService : ISmsNotificationService
    {
        public Tuple<GatewaySmsNotifyRequestRepresentation, ErrorRepresentation> SmsNotifyRequest(
            SmsNotifyRequest smsNotifyRequest, BahmniConfiguration bahmniConfiguration)
        {
            var timeStamp = DateTime.Now.ToUniversalTime();
            var requestId = Guid.NewGuid();
            var hip = new SmsNotifyHip(bahmniConfiguration.Name, bahmniConfiguration.Id);
            var notification = new Model.SmsNotification(smsNotifyRequest.phoneNo,hip);

            return new Tuple<GatewaySmsNotifyRequestRepresentation, ErrorRepresentation>(
                new GatewaySmsNotifyRequestRepresentation(requestId, timeStamp, notification), null);

        }
    }
}