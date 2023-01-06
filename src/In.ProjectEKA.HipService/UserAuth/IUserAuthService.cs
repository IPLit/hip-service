using System;
using System.Threading.Tasks;
using In.ProjectEKA.HipLibrary.Patient.Model;
using In.ProjectEKA.HipService.Common.Model;
using In.ProjectEKA.HipService.Gateway;
using In.ProjectEKA.HipService.UserAuth.Model;

namespace In.ProjectEKA.HipService.UserAuth
{
    public interface IUserAuthService
    {
        public Tuple<GatewayFetchModesRequestRepresentation, ErrorRepresentation> FetchModeResponse(
            FetchRequest fetchRequest, BahmniConfiguration bahmniConfiguration);
        
        public Task<ErrorRepresentation> AuthInit(AuthInitRequest authInitRequest, string correlationId,
            BahmniConfiguration bahmniConfiguration, GatewayConfiguration gatewayConfiguration);

        public Task<Tuple<AuthConfirmResponse, ErrorRepresentation>> AuthConfirm(AuthConfirmRequest authConfirmRequest,
            string correlationId, GatewayConfiguration gatewayConfiguration);

        
        public Task<Tuple<AuthConfirm, ErrorRepresentation>> OnAuthConfirmResponse(
            OnAuthConfirmRequest onAuthConfirmRequest);

        public Task Dump(NdhmDemographics ndhmDemographics);

     
    }
}