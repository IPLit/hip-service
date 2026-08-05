using System;
using FluentAssertions;
using In.ProjectEKA.HipService.UserAuth;
using Xunit;

namespace In.ProjectEKA.HipServiceTest.UserAuth
{
    public class UserAuthMapTest
    {
        public UserAuthMapTest()
        {
            // UserAuthMap.PhoneNumberToHealthId.Clear();
            // UserAuthMap.HealthIdToLatestVisitUuid.Clear();
        }

        [Theory]
        [InlineData("+919876543210", "9876543210")]
        [InlineData("919876543210", "9876543210")]
        [InlineData("9876543210", "9876543210")]
        public void NormalizePhoneNumber_should_strip_country_code(string input, string expected)
        {
            UserAuthMap.NormalizePhoneNumber(input).Should().Be(expected);
        }

        [Fact]
        public void GetHealthIdByPhoneNumber_should_return_matching_healthId()
        {
            UserAuthMap.PhoneNumberToHealthId["patient@sbx"] = "9876543210";

            UserAuthMap.GetHealthIdByPhoneNumber("+919876543210").Should().Be("patient@sbx");
        }
        
        [Fact]
        public void GetHealthIdByPhoneNumber_should_return_null_when_phone_is_not_mapped()
        {
            UserAuthMap.GetHealthIdByPhoneNumber("9999999999").Should().BeNull();
        }
    }
}
