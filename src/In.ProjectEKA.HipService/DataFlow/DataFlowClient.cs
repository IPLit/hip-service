using In.ProjectEKA.HipService.Common;

namespace In.ProjectEKA.HipService.DataFlow
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Mime;

    using System.Threading.Tasks;
    using Gateway;
    using HipLibrary.Patient.Model;
    using Logger;
    using Microsoft.Net.Http.Headers;

    using Model;
    using RabbitMQ.Client;

    using static Common.HttpRequestHelper;

    public class DataFlowClient
    {
        private readonly DataFlowNotificationClient dataFlowNotificationClient;
        private readonly GatewayConfiguration gatewayConfiguration;
        private readonly HipService.Common.Model.BahmniConfiguration bahmniConfiguration;
        private readonly HttpClient httpClient;
        private readonly GatewayClient gatewayClient;

        public DataFlowClient(HttpClient httpClient,
            DataFlowNotificationClient dataFlowNotificationClient,
            GatewayConfiguration gatewayConfiguration,
            HipService.Common.Model.BahmniConfiguration bahmniConfiguration,
            GatewayClient gatewayClient)
        {
            this.gatewayClient = gatewayClient;
            this.httpClient = httpClient;
            this.dataFlowNotificationClient = dataFlowNotificationClient;
            this.gatewayConfiguration = gatewayConfiguration;
            this.bahmniConfiguration = bahmniConfiguration;
        }

        public virtual async Task SendDataToHiu(TraceableDataRequest dataRequest,
            IEnumerable<Entry> data,
            KeyMaterial keyMaterial)
        {
            await PostTo(dataRequest.ConsentId,
                dataRequest.DataPushUrl,
                dataRequest.CareContexts,
                new DataResponse(dataRequest.TransactionId, data, keyMaterial),
                dataRequest.CmSuffix,
                dataRequest.CorrelationId).ConfigureAwait(false);
        }

        private async Task PostTo(string consentId,
            string dataPushUrl,
            IEnumerable<GrantedContext> careContexts,
            DataResponse dataResponse,
            string cmSuffix,
            string correlationId)
        {
            var grantedContexts = careContexts as GrantedContext[] ?? careContexts.ToArray();
            var hiStatus = HiStatus.DELIVERED;
            var sessionStatus = SessionStatus.TRANSFERRED;
            var message = "Successfully delivered health information";
            try
            {
                // TODO: Need to handle non 2xx response also
                httpClient.DefaultRequestHeaders.Remove("Authorization");
                var token = await gatewayClient.Authenticate(correlationId, bahmniConfiguration.Id).ConfigureAwait(false);
                if (token.HasValue)
                {
                    var reqDataPush = CreateHttpRequestWithContentType(HttpMethod.Post, dataPushUrl, dataResponse, token.ValueOr(String.Empty), cmSuffix, correlationId,
                         MediaTypeNames.Application.Json, bahmniConfiguration.Id, Guid.NewGuid().ToString(), null,
                        null, null, dataResponse.TransactionId);
                    await httpClient.SendAsync(reqDataPush).ConfigureAwait(false);
                }
                else
                {
                    hiStatus = HiStatus.ERRORED;
                    sessionStatus = SessionStatus.FAILED;
                    message = "Failed to deliver health information";
                    Log.Error($"Failed to authenticate while delivering health information to {dataPushUrl}");
                }
            }
            catch (Exception exception)
            {
                hiStatus = HiStatus.ERRORED;
                sessionStatus = SessionStatus.FAILED;
                message = "Failed to deliver health information";
                Log.Error(exception, exception.StackTrace);
            }

            try
            {
                var statusResponses = grantedContexts
                    .Select(grantedContext =>
                        new StatusResponse(grantedContext.CareContextReference, hiStatus,
                            message))
                    .ToList();
                var dataNotificationRequest = new DataNotificationRequest(dataResponse.TransactionId,
                    DateTime.Now.ToUniversalTime().ToString(Common.Constants.DateTimeFormat),
                    new Notifier(Type.HIP, bahmniConfiguration.Id),
                    new StatusNotification(sessionStatus, bahmniConfiguration.Id, statusResponses),
                    consentId,
                    Guid.NewGuid());
                await GetDataNotificationRequest(dataNotificationRequest, cmSuffix, correlationId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }
        }

        private async Task GetDataNotificationRequest(DataNotificationRequest dataNotificationRequest,
            string cmSuffix,
            string correlationId)
        {
            await dataFlowNotificationClient.NotifyGateway(cmSuffix, dataNotificationRequest, correlationId);
        }
    }
}