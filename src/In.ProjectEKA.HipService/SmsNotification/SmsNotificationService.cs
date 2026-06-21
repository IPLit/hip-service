using System;
using In.ProjectEKA.HipLibrary.Patient.Model;
using In.ProjectEKA.HipService.Common;
using In.ProjectEKA.HipService.Common.Model;
using In.ProjectEKA.HipService.SmsNotification.Model;
using In.ProjectEKA.HipService.UserAuth;

namespace In.ProjectEKA.HipService.SmsNotification
{
    using static Constants;
    public class SmsNotificationService : ISmsNotificationService
    {
        public Tuple<GatewaySmsNotifyRequestRepresentation, ErrorRepresentation> SmsNotifyRequest(
            SmsNotifyRequest smsNotifyRequest, BahmniConfiguration bahmniConfiguration)
        {
            var hipId = bahmniConfiguration.GetDefaultHfrId();
            var hipName = bahmniConfiguration.GetDefaultFacilityName();

            var normalizedPhone = UserAuthMap.NormalizePhoneNumber(smsNotifyRequest.phoneNo);
            if (UserAuthMap.PhoneNumberToHealthId.TryGetValue(normalizedPhone, out var healthId)
                && UserAuthMap.HealthIdToLatestVisitUuid.TryGetValue(healthId, out var visitUuid))
            {
                var visitHipId = bahmniConfiguration.GetHfrIdByVisitUuid(visitUuid);
                var visitHipName = bahmniConfiguration.GetFacilityNameByVisitUuid(visitUuid);
                if (!string.IsNullOrEmpty(visitHipId))
                    hipId = visitHipId;
                if (!string.IsNullOrEmpty(visitHipName))
                    hipName = visitHipName;
            }

            var hip = new SmsNotifyHip(hipName, hipId);
            var notification = new Model.SmsNotification(smsNotifyRequest.phoneNo, hip);

            return new Tuple<GatewaySmsNotifyRequestRepresentation, ErrorRepresentation>(
                new GatewaySmsNotifyRequestRepresentation(notification), null);

        }
    }
}
