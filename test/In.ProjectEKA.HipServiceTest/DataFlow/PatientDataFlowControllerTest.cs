using Hl7.Fhir.Model;

namespace In.ProjectEKA.HipServiceTest.DataFlow
{
    using System;
    using Builder;
    using FluentAssertions;
    using Hangfire;
    using Hangfire.Common;
    using Hangfire.States;
    using HipLibrary.Patient.Model;
    using static HipService.Common.Constants;
    using HipService.DataFlow;
    using Microsoft.AspNetCore.Http;
    using Moq;
    using Xunit;
    using HipService.Gateway;
    using HipService.Gateway.Model;
    using Microsoft.Extensions.Logging;

    public class PatientDataFlowControllerTest
    {
        private readonly Mock<IBackgroundJobClient> backgroundJobClient = new Mock<IBackgroundJobClient>();
        private readonly Mock<IDataFlow> dataFlow = new Mock<IDataFlow>();
        private readonly PatientDataFlowController patientDataFlowController;

        private readonly Mock<ILogger<PatientDataFlowController>> logger =
            new Mock<ILogger<PatientDataFlowController>>();
       
        private readonly Mock<GatewayClient> gatewayClient = new Mock<GatewayClient>(MockBehavior.Strict, null, null);

        private readonly GatewayConfiguration gatewayConfiguration = new GatewayConfiguration
        {
            CmSuffix = "ncg"
        };

        public PatientDataFlowControllerTest()
        {
            patientDataFlowController = new PatientDataFlowController(dataFlow.Object,
                backgroundJobClient.Object,
                gatewayClient.Object,
                logger.Object,
                gatewayConfiguration
                );
        }

        [Fact]
        private void ShouldEnqueueDataFlowRequestAndReturnAccepted()
        {
            var gatewayId = TestBuilder.Faker().Random.String();
            var transactionId = TestBuilder.Faker().Random.Hash();
            var requestId = TestBuilder.Faker().Random.Hash();
            var healthInformationRequest = TestBuilder.HealthInformationRequest(transactionId);
            var hiRequest = new HIRequest(healthInformationRequest.Consent,
                healthInformationRequest.DateRange,
                healthInformationRequest.DataPushUrl,
                healthInformationRequest.KeyMaterial);
            var request = new PatientHealthInformationRequest(transactionId, hiRequest);
            var expectedResponse = new HealthInformationTransactionResponse(transactionId);
            var correlationId = Uuid.Generate().ToString();
            dataFlow.Setup(d => d.HealthInformationRequestFor(healthInformationRequest, gatewayId, correlationId))
                .ReturnsAsync(
                    new Tuple<HealthInformationTransactionResponse, ErrorRepresentation>(expectedResponse, null));

            var response = patientDataFlowController.HealthInformationRequestFor(request, gatewayId, correlationId, requestId, It.IsAny<DateTime>().ToString() );
            backgroundJobClient.Verify(client => client.Create(
                It.Is<Job>(job => job.Method.Name == "HealthInformationOf" && job.Args[0] == request),
                It.IsAny<EnqueuedState>()));
            dataFlow.Verify();
            response.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        }

        [Fact]
        private async void ShouldSendDataFlowRequestToGateway()
        {
            var gatewayId = TestBuilder.Faker().Random.String();
            var transactionId = TestBuilder.Faker().Random.Hash();
            var requestId = TestBuilder.Faker().Random.Hash();
            var healthInformationRequest = TestBuilder.HealthInformationRequest(transactionId);
            var hiRequest = new HIRequest(healthInformationRequest.Consent,
                healthInformationRequest.DateRange,
                healthInformationRequest.DataPushUrl,
                healthInformationRequest.KeyMaterial);
            var request = new PatientHealthInformationRequest(transactionId, hiRequest);
            var expectedResponse = new HealthInformationTransactionResponse(transactionId);
            var correlationId = Uuid.Generate().ToString();
            dataFlow.Setup(d => d.HealthInformationRequestFor(healthInformationRequest, gatewayId, correlationId))
                .ReturnsAsync(
                    new Tuple<HealthInformationTransactionResponse, ErrorRepresentation>(expectedResponse, null));
            gatewayClient.Setup(
                client =>
                    client.SendDataToGateway(PATH_HEALTH_INFORMATION_ON_REQUEST,
                        It.IsAny<GatewayDataFlowRequestResponse>(), "ncg", correlationId,null,null,null));

            await patientDataFlowController.HealthInformationOf(request, correlationId, gatewayId, requestId);

            gatewayClient.Verify();
            dataFlow.Verify();
        }
    }
}