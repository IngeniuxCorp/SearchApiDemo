using Ingeniux.Runtime.Models.APIModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Ingeniux.Runtime.Controllers
{
    public abstract class DWController : Controller
    {
		private const int DEFAULT_OCAPI_WARNING_TIMEOUT = 2000;

		public string SitePath { get {
				return ConfigurationManager.AppSettings["PageFilesLocation"];
			}
		}

		public int ResponseWarningLength
		{
			get
			{
				int warningLen;
				if(!int.TryParse(ConfigurationManager.AppSettings["OCAPIResponseWarningLength"],out warningLen))
				{
					return DEFAULT_OCAPI_WARNING_TIMEOUT;
				}
				return warningLen;
			}
		}

		public RuntimeLogger Logger
		{
			get
			{
				return RuntimeLogger.GetLogger(SitePath);
			}
		}
		//public RuntimeLogger Logger = RuntimeLogger.GetLogger(CMSPageFactory.GetDefaultSitePath());
		public ContentResult GetJSONContent(object obj)
		{
			JsonSerializerSettings settings = new JsonSerializerSettings();

			settings.ContractResolver = new DWContractResolver();
			settings.NullValueHandling = NullValueHandling.Ignore;

			string json = JsonConvert.SerializeObject(obj, settings);

			return Content(json, "application/json", System.Text.Encoding.UTF8);
		}
	}
}