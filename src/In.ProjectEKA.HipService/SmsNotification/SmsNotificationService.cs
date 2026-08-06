using System;
using System.Text.Encodings.Web;
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
            if (smsNotifyRequest == null || string.IsNullOrWhiteSpace(smsNotifyRequest.phoneNo))
            {
                return new Tuple<GatewaySmsNotifyRequestRepresentation, ErrorRepresentation>(null,
                    new ErrorRepresentation(new Error(ErrorCode.BadRequest, "Phone number is required")));
            }

            var phoneNo = smsNotifyRequest.phoneNo.Trim();
            var hipId = bahmniConfiguration.GetDefaultHfrId();
            var hipName = bahmniConfiguration.GetDefaultFacilityName();

            var healthId = UserAuthMap.GetHealthIdByPhoneNumber(phoneNo);
            if (string.IsNullOrEmpty(healthId))
            {
                return new Tuple<GatewaySmsNotifyRequestRepresentation, ErrorRepresentation>(null,
                    new ErrorRepresentation(new Error(ErrorCode.BadRequest,
                        "No details found for the given phone number " + phoneNo)));
            }

            // Prefer visit-specific facility when available; fall back to default HIP for race/timing gaps.
            if (UserAuthMap.HealthIdToLatestVisitUuid.TryGetValue(healthId, out var visitUuid)
                && !string.IsNullOrEmpty(visitUuid))
            {
                var visitHipId = bahmniConfiguration.GetHfrIdByVisitUuid(visitUuid);
                var visitHipName = bahmniConfiguration.GetFacilityNameByVisitUuid(visitUuid);
                if (!string.IsNullOrEmpty(visitHipId))
                    hipId = visitHipId;
                if (!string.IsNullOrEmpty(visitHipName))
                    hipName = visitHipName;
            }

            var hip = new SmsNotifyHip(UrlEncoder.Default.Encode(hipName), UrlEncoder.Default.Encode(hipId));
            var notification = new Model.SmsNotification(phoneNo, hip);
            Log.Information("SmsNotify hip: {name}, {id} of abha address {healthId}", hip.name, hip.id, healthId);

            return new Tuple<GatewaySmsNotifyRequestRepresentation, ErrorRepresentation>(
                new GatewaySmsNotifyRequestRepresentation(notification), null);
        }
    }
}
