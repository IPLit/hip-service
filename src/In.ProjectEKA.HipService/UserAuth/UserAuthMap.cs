using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using In.ProjectEKA.HipLibrary.Patient.Model;
using In.ProjectEKA.HipService.Common.Model;
using In.ProjectEKA.HipService.Link.Model;
using In.ProjectEKA.HipService.UserAuth.Model;
using Microsoft.AspNetCore.Http;

namespace In.ProjectEKA.HipService.UserAuth
{
    public static class UserAuthMap{
        public static ConcurrentDictionary<Guid, List<Mode>> RequestIdToAuthModes = new ConcurrentDictionary<Guid, List<Mode>>();
        public static ConcurrentDictionary<Guid, string> RequestIdToTransactionIdMap = new ConcurrentDictionary<Guid, string>();
        public static ConcurrentDictionary<Guid, string> RequestIdToAccessToken = new ConcurrentDictionary<Guid, string>();
        public static ConcurrentDictionary<string, string> HealthIdToTransactionId = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<Guid, AuthConfirmPatient> RequestIdToPatientDetails = new ConcurrentDictionary<Guid, AuthConfirmPatient>();
        public static ConcurrentDictionary<Guid, Error> RequestIdToErrorMessage = new ConcurrentDictionary<Guid, Error>();
        public static ConcurrentDictionary<string, string> HealthIdToAccessToken = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, string> RequestIdToHipId = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, string> HealthIdToLatestVisitUuid = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, string> PhoneNumberToHealthId = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static ConcurrentDictionary<Guid, AuthNotifyStatus> TransactionIdToAuthNotifyStatus = new ConcurrentDictionary<Guid, AuthNotifyStatus>();
        public static ConcurrentDictionary<Guid, AuthConfirmPatient> TransactionIdToPatientDetails = new ConcurrentDictionary<Guid, AuthConfirmPatient>();
        public static Dictionary<string, int> ErrorCodeToStatusCode = new Dictionary<string, int>()
        {
            {ErrorCode.BadRequest, StatusCodes.Status400BadRequest},
            {ErrorCode.GatewayTimedOut, StatusCodes.Status504GatewayTimeout},
            {ErrorCode.ServerInternalError, StatusCodes.Status500InternalServerError},
            {ErrorCode.ConsentNotGranted, StatusCodes.Status504GatewayTimeout}
        };

        public static void UpdateHealthIdToLatestVisitUuid(string healthId, string visitUuid)
        {
            if (string.IsNullOrEmpty(healthId) || string.IsNullOrEmpty(visitUuid))
                return;

            HealthIdToLatestVisitUuid[healthId] = visitUuid;
        }

        /// <summary>
        /// Updates or sets the latest health ID associated with a phone number.
        /// </summary>
        public static void UpdateHealthIdToPhoneNumber(string phoneNumber, string healthId)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(healthId))
                return;

            var normalizedPhone = NormalizePhoneNumber(phoneNumber);
            // Atomic update/insert: automatically replaces old healthId with the new one
            PhoneNumberToHealthId[normalizedPhone] = healthId.Trim();
        }

        /// <summary>
        /// Retrieves the latest health ID associated with a phone number in O(1) time.
        /// </summary>
        public static string GetHealthIdByPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return null;

            var normalizedPhone = NormalizePhoneNumber(phoneNumber);
            PhoneNumberToHealthId.TryGetValue(normalizedPhone, out var healthId);
            return healthId; // Returns null if not found
        }

        public static string NormalizePhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber))
                return phoneNumber;

            phoneNumber = phoneNumber.Trim();
            if (phoneNumber.StartsWith("+91"))
                phoneNumber = phoneNumber.Substring(3);
            else if (phoneNumber.StartsWith("91"))
                phoneNumber = phoneNumber.Substring(2);
            return phoneNumber;
        }
    }
}
