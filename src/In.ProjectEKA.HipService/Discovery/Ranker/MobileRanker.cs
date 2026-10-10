namespace In.ProjectEKA.HipService.Discovery.Ranker
{
    using System.Linq;
    using HipLibrary.Patient.Model;
    using static RankBuilder;
    using static MetaBuilder;

    public class MobileRanker : IRanker<Patient>
    {
        public PatientWithRank<Patient> Rank(Patient patient, string mobile)
        {
            return SameMobile(patient.PhoneNumber, mobile)
                ? new PatientWithRank<Patient>(patient, StrongMatchRank, FullMatchMeta(Match.Mobile))
                : new PatientWithRank<Patient>(patient, EmptyRank, EmptyMeta);
        }

        private static bool SameMobile(string left, string right)
        {
            var normalizedLeft = LastTenDigits(left);
            var normalizedRight = LastTenDigits(right);
            return normalizedLeft != null && normalizedLeft == normalizedRight;
        }

        private static string LastTenDigits(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            var digits = new string(value.Where(char.IsDigit).ToArray());
            return digits.Length < 10 ? null : digits.Substring(digits.Length - 10);
        }
    }
}