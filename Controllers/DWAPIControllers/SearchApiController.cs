using Ingeniux.Runtime.Models.APIModels;
using Ingeniux.Search;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.Http.Results;
using System.Web.Mvc;

namespace Ingeniux.Runtime.Controllers
{
    public abstract class SearchApiController : ApiController
    {
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