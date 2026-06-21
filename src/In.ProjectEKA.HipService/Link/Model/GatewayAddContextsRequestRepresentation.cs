using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace In.ProjectEKA.HipService.Link.Model
{
    public class GatewayAddContextsRequestRepresentation
    {
        public string RequestId { get; }
        public string RequesterId { get; }
        public string AbhaAddress { get; }
        public IEnumerable<CareContextLinkRequest> CareContexts { get; }

        public GatewayAddContextsRequestRepresentation(
            string requestId,
            string requesterId,
            string abhaAddress,
            IEnumerable<CareContextLinkRequest> careContexts)
        {
            RequestId = requestId;
            RequesterId = requesterId;
            AbhaAddress = abhaAddress;
            CareContexts = careContexts;
        }

        public string dump(Object o)
        {
            return JsonConvert.SerializeObject(o);
        }
    }
}
