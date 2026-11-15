namespace In.ProjectEKA.HipLibrary.Patient.Model
{
    public class PatientR4Demographics
    {
        public PatientR4Demographics(string name, Gender? gender, string dateOfBirth, string phoneNumber)
        {
            Name = name;
            Gender = gender;
            DateOfBirth = dateOfBirth;
            PhoneNumber = phoneNumber;
        }

        public string Name { get; }

        public Gender? Gender { get; }

        public string DateOfBirth { get; }

        public string PhoneNumber { get; }
    }
}
