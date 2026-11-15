using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using In.ProjectEKA.HipService.Common.Model;
using In.ProjectEKA.HipService.OpenMrs;
using In.ProjectEKA.HipService.UserAuth;
using Moq;
using Xunit;

namespace In.ProjectEKA.HipServiceTest.Common
{
    public class HipFacilityResolverTest
    {
        [Fact]
        public async Task Resolve_does_not_store_an_auth_row_when_the_link_token_is_missing()
        {
            var healthId = "shilpa309@sbx";
            var hipId = "IN2710004666_1";
            var userAuthRepository = new Mock<IUserAuthRepository>();
            userAuthRepository
                .Setup(repository => repository.GetAuthDetailsByHealthId(healthId, hipId))
                .ReturnsAsync(new Tuple<AuthConfirm, Exception>(null, null));

            var resolver = new HipFacilityResolver(
                userAuthRepository.Object,
                ConfigurationWithUnresolvedVisit(hipId, "IPLit Wellness Clinic"));

            var resolved = await resolver.Resolve(healthId, "ef87d121-119f-4d87-9fb9-9a71e554281e");

            resolved.HfrId.Should().Be(hipId);
            resolved.HfrName.Should().Be("IPLit Wellness Clinic");
            userAuthRepository.Verify(
                repository => repository.Add(It.IsAny<AuthConfirm>()),
                Times.Never);
            userAuthRepository.Verify(
                repository => repository.Delete(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task Resolve_keeps_an_existing_row_when_its_access_token_is_empty()
        {
            var healthId = "shilpa309@sbx";
            var hipId = "IN2710004666_1";
            var userAuthRepository = new Mock<IUserAuthRepository>();
            userAuthRepository
                .Setup(repository => repository.GetAuthDetailsByHealthId(healthId, hipId))
                .ReturnsAsync(new Tuple<AuthConfirm, Exception>(
                    new AuthConfirm(healthId, hipId, "", "IPLit Wellness Clinic"), null));

            var resolver = new HipFacilityResolver(
                userAuthRepository.Object,
                ConfigurationWithUnresolvedVisit(hipId, "IPLit Wellness Clinic"));

            await resolver.Resolve(healthId, "ef87d121-119f-4d87-9fb9-9a71e554281e");

            userAuthRepository.Verify(
                repository => repository.Update(It.IsAny<AuthConfirm>()),
                Times.Never);
            userAuthRepository.Verify(
                repository => repository.Add(It.IsAny<AuthConfirm>()),
                Times.Never);
            userAuthRepository.Verify(
                repository => repository.Delete(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        private static BahmniConfiguration ConfigurationWithUnresolvedVisit(string hipId, string hipName)
        {
            var openMrsClient = new Mock<IOpenMrsClient>();
            openMrsClient.Setup(client => client.GetAsync(It.IsAny<string>()))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));
            return new BahmniConfiguration(openMrsClient.Object)
            {
                Id = hipId,
                Name = hipName
            };
        }
    }
}
