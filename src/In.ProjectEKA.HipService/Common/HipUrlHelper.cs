using System;
using In.ProjectEKA.HipService.Common.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace In.ProjectEKA.HipService.Common
{
    public class HipUrlHelper
    {
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly HipServiceConfiguration hipServiceConfiguration;

        public HipUrlHelper(IHttpContextAccessor httpContextAccessor, HipServiceConfiguration hipServiceConfiguration)
        {
            this.httpContextAccessor = httpContextAccessor;
            this.hipServiceConfiguration = hipServiceConfiguration;
        }
        
        public string getHipUrl()
        {
            if (hipServiceConfiguration.BaseUrl.Length == 0)
                hipServiceConfiguration.BaseUrl = new Uri(httpContextAccessor.HttpContext.Request.GetDisplayUrl()).GetLeftPart(UriPartial.Authority);
            return hipServiceConfiguration.BaseUrl + hipServiceConfiguration.Path;
        }
    }
}