using System;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using In.ProjectEKA.HipLibrary.Patient.Model;
using In.ProjectEKA.HipService.Logger;
using In.ProjectEKA.HipService.Common.Model;
using In.ProjectEKA.HipService.SmsNotification.Model;
using In.ProjectEKA.HipService.UserAuth;

namespace In.ProjectEKA.HipService.SmsNotification
{
    public class SmsNotificationService : ISmsNotificationService
    {
        private readonly IUserAuthRepository userAuthRepository;

        public SmsNotificationService(IUserAuthRepository userAuthRepository)
        {
            this.userAuthRepository = userAuthRepository;
        }

        public async Task<Tuple<GatewaySmsNotifyRequestRepresentation, ErrorRepresentation>> SmsNotifyRequest(
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

            var healthId = await ResolveHealthId(phoneNo).ConfigureAwait(false);
            if (string.IsNullOrEmpty(healthId))
            {
                Log.Information("Sms notify rejected. No ABHA address mapped for phone {Phone}", phoneNo);
                return new Tuple<GatewaySmsNotifyRequestRepresentation, ErrorRepresentation>(null,
                    new ErrorRepresentation(new Error(ErrorCode.BadRequest,
                        "No details found for the given phone number " + phoneNo)));
            }

            // Prefer visit-specific facility when available; fall back to default HIP for race/timing gaps.
            if (UserAuthMap.HealthIdToLatestVisitUuid.TryGetValue(healthId, out var visitUuid)
                && !string.IsNullOrEmpty(visitUuid))
            {
                var hfrDetails = await bahmniConfiguration.GetHfrDetailsByVisitUuid(visitUuid);
                if (!string.IsNullOrEmpty(hfrDetails?.HfrId))
                {
                    hipId = hfrDetails.HfrId;
                    if (!string.IsNullOrEmpty(hfrDetails.HfrName))
                        hipName = hfrDetails.HfrName;
                }
            }
            else
            {
                var (hfrDetails, error) = await userAuthRepository.GetAuthDetailsByHealthId(healthId);
                if (error != null)
                {
                    return new Tuple<GatewaySmsNotifyRequestRepresentation, ErrorRepresentation>(null,
                        new ErrorRepresentation(new Error(ErrorCode.BadRequest,
                            "No hfr details found for the given phone number " + phoneNo)));
                }
                hipId = hfrDetails?.HipId;
                hipName = hfrDetails?.HipId;
            }

            var hip = new SmsNotifyHip(UrlEncoder.Default.Encode(hipName), UrlEncoder.Default.Encode(hipId));
            var notification = new Model.SmsNotification(phoneNo, hip);
            Log.Information("SmsNotify hip: {name}, {id} of abha address {healthId}", hip.name, hip.id, healthId);

            return new Tuple<GatewaySmsNotifyRequestRepresentation, ErrorRepresentation>(
                new GatewaySmsNotifyRequestRepresentation(notification), null);
        }

        private async Task<string> ResolveHealthId(string phoneNo)
        {
            var healthId = UserAuthMap.GetHealthIdByPhoneNumber(phoneNo);
            if (!string.IsNullOrEmpty(healthId) || userAuthRepository == null)
                return healthId;

            healthId = await userAuthRepository.GetHealthIdByPhoneNumber(phoneNo).ConfigureAwait(false);
            if (string.IsNullOrEmpty(healthId))
                return null;

            UserAuthMap.UpdateHealthIdToPhoneNumber(phoneNo, healthId);
            Log.Information("Sms notify resolved ABHA address {HealthId} from demographics for phone {Phone}",
                healthId, phoneNo);
            return healthId;
        }
    }
}
