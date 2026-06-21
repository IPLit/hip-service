using System;
using In.ProjectEKA.HipLibrary.Patient.Model;
using In.ProjectEKA.HipService.Logger;
using In.ProjectEKA.HipService.Common.Model;
using In.ProjectEKA.HipService.SmsNotification.Model;
using In.ProjectEKA.HipService.UserAuth;

namespace In.ProjectEKA.HipService.SmsNotification
{
    public class SmsNotificationService : ISmsNotificationService
    {
        public Tuple<GatewaySmsNotifyRequestRepresentation, ErrorRepresentation> SmsNotifyRequest(
            SmsNotifyRequest smsNotifyRequest, BahmniConfiguration bahmniConfiguration)
        {
            var hipId = bahmniConfiguration.GetDefaultHfrId();
            var hipName = bahmniConfiguration.GetDefaultFacilityName();

            var healthId = UserAuthMap.GetHealthIdByPhoneNumber(smsNotifyRequest.phoneNo);
            if (!string.IsNullOrEmpty(healthId)
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
            Log.Information("SmsNotify hip: {hip} of abha address {healthId}", hip.ToString(), healthId);

            return new Tuple<GatewaySmsNotifyRequestRepresentation, ErrorRepresentation>(
                new GatewaySmsNotifyRequestRepresentation(notification), null);

        }
    }
}
