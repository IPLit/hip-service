using System.Linq;
using Hl7.Fhir.Model;

namespace In.ProjectEKA.HipLibrary.Patient
{
    using System.Threading.Tasks;
    using Model;
    using Optional;

    public interface IPatientRepository
    {
        Task<Option<Patient>> PatientWithAsync(string patientIdentifier);

        Task<Option<PatientR4Demographics>> PatientR4WithAsync(string patientIdentifier);

        Task<IQueryable<Patient>> PatientsWithVerifiedId(string healthId);

        Task<IQueryable<Patient>> PatientsWithDemographics(string name, AdministrativeGender? gender, string yearOfBirth, string phoneNumber);
    }
}