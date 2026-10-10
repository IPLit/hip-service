using System;
using System.Collections.Generic;
using System.Linq;
using In.ProjectEKA.HipLibrary.Patient.Model;

namespace In.ProjectEKA.HipService.Discovery.Mapper;

public static class PatientDiscoveryMapper
{
    private static readonly Dictionary<string, string> AbdmMatchedBy =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"Mobile", "MOBILE"},
            {"Mr", "MR"},
            {"NdhmHealthNumber", "ABHA_NUMBER"},
            {"ABHA_NUMBER", "ABHA_NUMBER"},
            {"HEALTH_NUMBER", "ABHA_NUMBER"},
            {"HealthId", "ABHA_ADDRESS"},
            {"abhaAddress", "ABHA_ADDRESS"},
            {"ConsentManagerUserId", "ABHA_ADDRESS"}
        };

    public static List<PatientDiscoveryRepresentation> Map(PatientEnquiryRepresentation patientEnquiryRepresentation)
    {
        if (patientEnquiryRepresentation?.CareContexts == null)
        {
            return null;
        }

        return patientEnquiryRepresentation.CareContexts
            .SelectMany(cc => HiTypesOf(cc).Select(hiType => new { HiType = hiType, CareContext = cc }))
            .GroupBy(x => x.HiType)
            .Select(group => new PatientDiscoveryRepresentation(patientEnquiryRepresentation.ReferenceNumber,
                patientEnquiryRepresentation.Display,
                group.Select(x => new CareContextRepresentation(x.CareContext.ReferenceNumber, x.CareContext.Display))
                    .ToList(),
                group.Key.ToString(),
                group.Count()))
            .ToList();
    }

    public static List<string> ToAbdmMatchedBy(IEnumerable<string> matchedBy)
    {
        if (matchedBy == null)
        {
            return new List<string>();
        }

        return matchedBy
            .Select(field => field != null && AbdmMatchedBy.TryGetValue(field, out var mapped) ? mapped : null)
            .Where(field => field != null)
            .Distinct()
            .ToList();
    }

    private static IEnumerable<HiType> HiTypesOf(CareContextRepresentation careContext)
    {
        if (careContext.HiTypes != null && careContext.HiTypes.Any())
        {
            return careContext.HiTypes;
        }

        return new[] {HiType.OPConsultation};
    }
}