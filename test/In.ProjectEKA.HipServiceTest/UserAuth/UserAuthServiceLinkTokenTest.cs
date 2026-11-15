using System;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using In.ProjectEKA.HipLibrary.Patient.Model;
using In.ProjectEKA.HipService.Common.Model;
using In.ProjectEKA.HipService.Gateway;
using In.ProjectEKA.HipService.OpenMrs;
using In.ProjectEKA.HipService.UserAuth;
using In.ProjectEKA.HipService.UserAuth.Model;
using Microsoft.Extensions.Logging;
using Moq;
using Optional;
using Xunit;

namespace In.ProjectEKA.HipServiceTest.UserAuth
{
    public class UserAuthServiceLinkTokenTest
    {
        [Fact]
        public async Task Generate_link_token_replaces_an_empty_stored_access_token()
        {
            var healthId = "shilpa309@sbx";
            var hipId = "IN2710004666_1";
            var linkToken = UnsignedToken(healthId, hipId);
            var userAuthRepository = new Mock<IUserAuthRepository>();
            userAuthRepository
                .Setup(repository => repository.GetAuthDetailsByHealthId(healthId, hipId))
                .ReturnsAsync(new Tuple<AuthConfirm, Exception>(
                    new AuthConfirm(healthId, hipId, "", "IPLit Wellness Clinic"), null));
            userAuthRepository
                .Setup(repository => repository.Update(It.IsAny<AuthConfirm>()))
                .Returns(true);

            var service = new UserAuthService(
                userAuthRepository.Object,
                new Mock<ILogger<UserAuthController>>().Object,
                new Mock<IGatewayClient>().Object,
                new BahmniConfiguration(new Mock<IOpenMrsClient>().Object) { Id = "other-hip", Name = "Other" });

            var result = await service.HandleOnGenerateLinkToken(new OnGenerateTokenRequest(
                healthId,
                linkToken,
                new Resp(Guid.NewGuid().ToString()),
                null));

            result.Item2.Should().BeNull();
            userAuthRepository.Verify(repository => repository.Update(It.Is<AuthConfirm>(saved =>
                saved.HealthId == healthId &&
                saved.HipId == hipId &&
                saved.AccessToken == linkToken &&
                saved.HipName == "IPLit Wellness Clinic")), Times.Once);
            userAuthRepository.Verify(
                repository => repository.Delete(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task Generate_link_token_inserts_a_row_when_none_is_stored()
        {
            var healthId = "shilpa309@sbx";
            var hipId = "IN2710004666_1";
            var linkToken = UnsignedToken(healthId, hipId);
            var userAuthRepository = new Mock<IUserAuthRepository>();
            userAuthRepository
                .Setup(repository => repository.GetAuthDetailsByHealthId(healthId, hipId))
                .ReturnsAsync(new Tuple<AuthConfirm, Exception>(null, null));
            userAuthRepository
                .Setup(repository => repository.Add(It.IsAny<AuthConfirm>()))
                .ReturnsAsync((AuthConfirm authConfirm) => Option.Some(authConfirm));

            var service = new UserAuthService(
                userAuthRepository.Object,
                new Mock<ILogger<UserAuthController>>().Object,
                new Mock<IGatewayClient>().Object,
                new BahmniConfiguration(new Mock<IOpenMrsClient>().Object) { Id = "other-hip", Name = "Other" });

            var result = await service.HandleOnGenerateLinkToken(new OnGenerateTokenRequest(
                healthId,
                linkToken,
                new Resp(Guid.NewGuid().ToString()),
                null));

            result.Item2.Should().BeNull();
            userAuthRepository.Verify(repository => repository.Add(It.Is<AuthConfirm>(saved =>
                saved.HealthId == healthId &&
                saved.HipId == hipId &&
                saved.AccessToken == linkToken)), Times.Once);
            userAuthRepository.Verify(
                repository => repository.Delete(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        private static string UnsignedToken(string healthId, string hipId)
        {
            var exp = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();
            var header = Base64Url("{\"alg\":\"none\"}");
            var payload = Base64Url(
                "{\"abhaAddress\":\"" + healthId + "\",\"hipId\":\"" + hipId + "\",\"exp\":" + exp + "}");
            return header + "." + payload + ".sig";
        }

        private static string Base64Url(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
