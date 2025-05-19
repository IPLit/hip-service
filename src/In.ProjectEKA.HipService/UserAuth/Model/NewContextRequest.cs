using System.Collections.Generic;
using In.ProjectEKA.HipLibrary.Patient.Model;

namespace In.ProjectEKA.HipService.UserAuth.Model
{
    public class NewContextRequest
    {
        public string PatientReferenceNumber { get; }
        public string PatientName { get; }

        public string HealthId { get; }

        public List<CareContextRepresentation> CareContexts { get; }

        public NewContextRequest(string patientReferenceNumber, string patientName,
            List<CareContextRepresentation> careContexts, string healthId)
        {
            PatientReferenceNumber = patientReferenceNumber;
            PatientName = patientName;
            CareContexts = careContexts;
            HealthId = healthId;
        }
    }
}