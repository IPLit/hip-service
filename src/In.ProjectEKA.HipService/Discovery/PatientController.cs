using System.Collections.Generic;
using System.Linq;
using In.ProjectEKA.HipService.Discovery.Mapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace In.ProjectEKA.HipService.Discovery
{
    using System;
    using System.Threading.Tasks;
    using Gateway;
    using Gateway.Model;
    using static Common.Constants;
    using Hangfire;
    using HipLibrary.Patient.Model;
    using Logger;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using static In.ProjectEKA.HipService.Discovery.DiscoveryReqMap;
    using Common;


    [Authorize]
    [Route(PATH_CARE_CONTEXTS_DISCOVER)]
    [ApiController]
    public class CareContextDiscoveryController : Controller
    {
        private readonly IPatientDiscovery patientDiscovery;
        private readonly IGatewayClient gatewayClient;
        private readonly IBackgroundJobClient backgroundJob;
        private readonly ILogger<CareContextDiscoveryController> logger;

        public CareContextDiscoveryController(IPatientDiscovery patientDiscovery,
            IGatewayClient gatewayClient,
            IBackgroundJobClient backgroundJob, ILogger<CareContextDiscoveryController> logger)
        {
            this.patientDiscovery = patientDiscovery;
            this.gatewayClient = gatewayClient;
            this.backgroundJob = backgroundJob;
            this.logger = logger;
        }

        public AcceptedResult DiscoverPatientCareContexts(
            [FromHeader(Name = CORRELATION_ID)] string correlationId,
            [FromHeader(Name = REQUEST_ID)] string requestId,
            [FromHeader(Name = TIMESTAMP)] string timestamp,
            [FromBody] DiscoveryRequest request,
            [FromHeader(Name = "X-HIP-ID")] string hipId = null)
        {
            requestId = String.IsNullOrEmpty(requestId) ? Guid.NewGuid().ToString() : requestId;
            Log.Information($"discovery request received for {request.Patient.Id} with {requestId}.");
            Log.Information("Started Execution");
            Log.Information("calling GetPatientCareContext Method, Patient Name -> " + request.Patient.Name);
            Log.Information("Correlation Id -----> " + correlationId);
            Log.Information("X-HIP-ID -----> " + hipId);
            backgroundJob.Enqueue(() => GetPatientCareContext(request, correlationId, requestId, hipId));
            return Accepted();
        }

        [NonAction]
        public async Task GetPatientCareContext(DiscoveryRequest request, string correlationId, string requestId,
            string hipId = null)
        {
            Log.Information("In GetPatientCareContext Method -----> ");
            var patientId = request.Patient.Id;
            Log.Information("Patient Id -----> " + patientId);
            var cmSuffix = patientId.Substring(patientId.LastIndexOf("@", StringComparison.Ordinal) + 1);
            Log.Information("CM suffix -----> " + cmSuffix);
            hipId = string.IsNullOrWhiteSpace(hipId) ? null : hipId;
            try
            {
                var (response, error) = await patientDiscovery.PatientFor(request);
                Log.Information("PatientFor executed successfully" + response);
                List<PatientDiscoveryRepresentation> patientDiscoveryRepresentation = PatientDiscoveryMapper.Map(response?.Patient);
                if (error == null && (patientDiscoveryRepresentation == null || patientDiscoveryRepresentation.Count == 0))
                {
                    error = new ErrorRepresentation(new Error(ErrorCode.CareContextNotFound, "No care context found"));
                }
                else if (string.IsNullOrWhiteSpace(hipId))
                {
                    foreach (var p in patientDiscoveryRepresentation)
                    {
                        var careContext = p.CareContexts.ToList().Find(c => !string.IsNullOrWhiteSpace(c.ReferenceNumber));
                        if (careContext != null)
                        {
                            hipId = careContext.ReferenceNumber.Split(":")[1];
                            break;
                        }
                    }
                }

                var discovered = error == null;
                var gatewayDiscoveryRepresentation = new GatewayDiscoveryRepresentation(
                    discovered ? patientDiscoveryRepresentation : null,
                    discovered ? MatchedByFor(request, response?.Patient) : null,
                    request.TransactionId,
                    error?.Error,
                    new Resp(requestId));
                PatientInfoMap.TryAdd(patientId, request.Patient);

                Log.Information("on-discover payload for {RequestId} transaction {TransactionId} is {Payload}",
                    requestId, request.TransactionId, Serialize(gatewayDiscoveryRepresentation));
                await gatewayClient.SendDataToGateway(PATH_ON_DISCOVER, gatewayDiscoveryRepresentation, cmSuffix,
                    correlationId, hipId, requestId);
            }
            catch (Exception exception)
            {
                var gatewayDiscoveryRepresentation = new GatewayDiscoveryRepresentation(
                    null,
                    null,
                    request.TransactionId,
                    new Error(ErrorCode.ServerInternalError, "Unreachable external service"),
                    new Resp(requestId));
                Log.Information("on-discover error payload for {RequestId} is {Payload}",
                    requestId, Serialize(gatewayDiscoveryRepresentation));
                await gatewayClient.SendDataToGateway(PATH_ON_DISCOVER, gatewayDiscoveryRepresentation, cmSuffix,
                    correlationId, hipId);
                logger.LogError(LogEvents.Discovery, exception, $"Error happened for {requestId}");
            }
        }

        private static List<string> MatchedByFor(DiscoveryRequest request, PatientEnquiryRepresentation patient)
        {
            var matchedBy = PatientDiscoveryMapper.ToAbdmMatchedBy(patient?.MatchedBy);
            if (matchedBy.Count > 0)
            {
                return matchedBy;
            }

            var identifiers = (request.Patient?.VerifiedIdentifiers ?? Enumerable.Empty<Identifier>())
                .Concat(request.Patient?.UnverifiedIdentifiers ?? Enumerable.Empty<Identifier>());
            foreach (var identifier in identifiers)
            {
                var mapped = PatientDiscoveryMapper.ToAbdmMatchedBy(new[] {identifier.Type.ToString()});
                if (mapped.Count > 0)
                {
                    return mapped;
                }
            }

            if (!string.IsNullOrEmpty(request.Patient?.Id) && request.Patient.Id.Contains("@"))
            {
                return new List<string> {"ABHA_ADDRESS"};
            }

            return new List<string> {"MOBILE"};
        }

        private static string Serialize(GatewayDiscoveryRepresentation representation)
        {
            return JsonConvert.SerializeObject(representation, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                },
                Converters = new List<JsonConverter> {new StringEnumConverter()}
            });
        }
    }
}