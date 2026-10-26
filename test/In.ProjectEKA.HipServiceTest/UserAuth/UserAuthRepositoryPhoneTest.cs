using System.Threading.Tasks;
using FluentAssertions;
using In.ProjectEKA.HipService.UserAuth;
using In.ProjectEKA.HipService.UserAuth.Database;
using In.ProjectEKA.HipService.UserAuth.Model;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace In.ProjectEKA.HipServiceTest.UserAuth
{
    public class UserAuthRepositoryPhoneTest
    {
        [Fact]
        public async Task GetHealthIdByPhoneNumber_should_match_country_code_and_formatted_values()
        {
            var databaseName = "ndhm-phone-" + System.Guid.NewGuid();
            var repository = new UserAuthRepository(AuthContext(databaseName), DemographicsContext(databaseName));
            await repository.AddDemographics(new NdhmDemographics(
                "patient@sbx", "Name", "M", "1990", "9876543210"));
            await repository.AddDemographics(new NdhmDemographics(
                "formatted@sbx", "Name", "F", "1991", "+91-7000000002"));

            (await repository.GetHealthIdByPhoneNumber("919876543210")).Should().Be("patient@sbx");
            (await repository.GetHealthIdByPhoneNumber("+91 9876543210")).Should().Be("patient@sbx");
            (await repository.GetHealthIdByPhoneNumber("917000000002")).Should().Be("formatted@sbx");
            (await repository.GetHealthIdByPhoneNumber("911111111111")).Should().BeNull();
        }

        private static AuthContext AuthContext(string databaseName)
        {
            var options = new DbContextOptionsBuilder<AuthContext>()
                .UseInMemoryDatabase(databaseName + "-auth")
                .Options;
            return new AuthContext(options);
        }

        private static NdhmDemographicsContext DemographicsContext(string databaseName)
        {
            var options = new DbContextOptionsBuilder<NdhmDemographicsContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;
            return new NdhmDemographicsContext(options);
        }
    }
}
