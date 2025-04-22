namespace In.ProjectEKA.HipService.UserAuth.Model
{
    public class NdhmDemographics
    {
        public string HealthId { get; }
        public string Name { get; }
        public string Gender { get; }
        public string DateOfBirth { get; }
        public string PhoneNumber { get; }

        public NdhmDemographics(string healthId, string name, string gender, string dateOfBirth, string phoneNumber)
        {
            HealthId = healthId;
            Name = name;
            Gender = gender;
            DateOfBirth = dateOfBirth;
            PhoneNumber = phoneNumber.Trim();
        }
    }
}