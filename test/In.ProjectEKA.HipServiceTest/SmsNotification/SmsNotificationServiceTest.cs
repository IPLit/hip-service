using FluentAssertions;
using In.ProjectEKA.HipService.Common.Model;
using In.ProjectEKA.HipService.OpenMrs;
using In.ProjectEKA.HipService.SmsNotification;
using In.ProjectEKA.HipService.SmsNotification.Model;
using In.ProjectEKA.HipService.UserAuth;
using Moq;
using Xunit;

namespace In.ProjectEKA.HipServiceTest.SmsNotification
{
    public class SmsNotificationServiceTest
    {
        public SmsNotificationServiceTest()
        {
            // UserAuthMap.PhoneNumberToHealthId.Clear();
            // UserAuthMap.HealthIdToLatestVisitUuid.Clear();
        }

        [Fact]
        public void SmsNotifyRequest_should_use_latest_healthId_for_phone_to_resolve_visit_facility()
        {
            var healthId = "patient@sbx";
            var visitUuid = "visit-uuid-123";
            var phoneNo = "+919876543210";

            UserAuthMap.UpdateHealthIdToPhoneNumber(phoneNo, healthId);
            UserAuthMap.HealthIdToLatestVisitUuid[healthId] = visitUuid;

            var openMrsClient = new Mock<IOpenMrsClient>();
            var bahmniConfiguration = new BahmniConfiguration(openMrsClient.Object)
            {
                Id = "IN0000000001",
                Name = "Default Facility"
            };
            var smsNotificationService = new SmsNotificationService();

            var result = smsNotificationService.SmsNotifyRequest(
                new SmsNotifyRequest(phoneNo),
                bahmniConfiguration);

            result.Item1.Should().NotBeNull();
            result.Item2.Should().BeNull();
            result.Item1.notification.phoneNo.Should().Be(phoneNo);
        }

        [Fact]
        public void SmsNotifyRequest_should_fall_back_to_default_facility_when_visit_mapping_missing()
        {
            var healthId = "patient-fallback@sbx";
            var phoneNo = "+919999988877";

            UserAuthMap.UpdateHealthIdToPhoneNumber(phoneNo, healthId);
            UserAuthMap.HealthIdToLatestVisitUuid.TryRemove(healthId, out _);

            var openMrsClient = new Mock<IOpenMrsClient>();
            var bahmniConfiguration = new BahmniConfiguration(openMrsClient.Object)
            {
                Id = "IN0000000001",
                Name = "Default Facility"
            };
            var smsNotificationService = new SmsNotificationService();

            var result = smsNotificationService.SmsNotifyRequest(
                new SmsNotifyRequest(phoneNo),
                bahmniConfiguration);

            result.Item1.Should().NotBeNull();
            result.Item2.Should().BeNull();
            result.Item1.notification.hip.id.Should().Be("IN0000000001");
            result.Item1.notification.hip.name.Should().Be("Default%20Facility");
        }

    }
}
