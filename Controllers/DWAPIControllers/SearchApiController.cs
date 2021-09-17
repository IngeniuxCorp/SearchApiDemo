using Ingeniux.Runtime.Models.APIModels;
using Ingeniux.Search;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Results;
using System.Web.Mvc;

namespace Ingeniux.Runtime.Controllers
{
    public abstract class SearchApiController : ApiController
    {
        protected override void Initialize(HttpControllerContext controllerContext)
        {
            base.Initialize(controllerContext);
			string authorizationToken = ConfigurationManager.AppSettings["AuthorizationToken"];

            if (!string.IsNullOrWhiteSpace(authorizationToken))
            {
				var authHeader = controllerContext.Request?.Headers?.Authorization;
				var tokenValue = authHeader?.Parameter;
				var scheme = authHeader?.Scheme ?? string.Empty;
                if (!(scheme.Equals("bearer", StringComparison.InvariantCultureIgnoreCase) && authorizationToken.Equals(tokenValue)))
                {
					var msg = controllerContext.Request.CreateErrorResponse(System.Net.HttpStatusCode.Unauthorized, new HttpException(401, "Cannot authenticate"));
					throw new HttpResponseException(msg);
				}
			}
        }

        public string SitePath { get {
				return ConfigurationManager.AppSettings["PageFilesLocation"];
			}
		}

		public RuntimeLogger Logger
		{
			get
			{
				return RuntimeLogger.GetLogger(SitePath);
			}
		}
	}
}