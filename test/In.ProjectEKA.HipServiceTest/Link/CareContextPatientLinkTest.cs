namespace In.ProjectEKA.HipServiceTest.Link
{
    using System.Collections.Generic;
    using System.Linq;
    using FluentAssertions;
    using HipLibrary.Patient.Model;
    using HipService.Link;
    using Xunit;

    public class CareContextPatientLinkTest
    {
        [Fact]
        public void IncludesPatientWhenCareContextHasNoHiTypes()
        {
            var careContexts = new List<CareContextRepresentation>
            {
                new CareContextRepresentation("GAN204041:visit-1", "OPD Visit")
            };

            var patient = CareContextService.BuildPatientLinks("GAN204041", "Shilpa", careContexts);

            patient.Should().ContainSingle();
            patient[0].ReferenceNumber.Should().Be("GAN204041");
            patient[0].Display.Should().Be("Shilpa");
            patient[0].HiType.Should().Be(HiType.OPConsultation.ToString());
            patient[0].Count.Should().Be(1);
            patient[0].CareContexts.Should().ContainSingle();
            patient[0].CareContexts.Single().ReferenceNumber.Should().Be("GAN204041:visit-1");
        }

        [Fact]
        public void KeepsTheHiTypeSuppliedOnTheCareContext()
        {
            var careContexts = new List<CareContextRepresentation>
            {
                new CareContextRepresentation("visit-1", "Prescription", "VISIT", new[] { HiType.Prescription })
            };

            var patient = CareContextService.BuildPatientLinks("GAN204041", "Shilpa", careContexts);

            patient.Should().ContainSingle();
            patient[0].HiType.Should().Be(HiType.Prescription.ToString());
        }
    }
}
