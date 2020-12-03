using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Ingeniux.Runtime;
using System.Web.Configuration;
using FiftyOne.Foundation.Mobile.Detection;
using Ingeniux.Runtime.Models;
using Ingeniux.Runtime.Mobile;
using Ingeniux.Runtime.RuntimeAuth;
using System.Configuration;
using System.IO;
using System.Web.Optimization;
using System.Web.Hosting;
using Microsoft.ApplicationInsights.Extensibility;

namespace Ingeniux.Runtime
{
	// Note: For instructions on enabling IIS6 or IIS7 classic mode, 
	// visit http://go.microsoft.com/?LinkId=9394801

	public class MvcApplication : System.Web.HttpApplication
	{
		protected void Application_Start()
		{
			if (FiftyOne.Foundation.Mobile.Detection.Configuration.Manager.Enabled)
				HttpCapabilitiesBase.BrowserCapabilitiesProvider = new IGXMobileCapabilitiesProvider();

			TelemetryConfiguration.Active.TelemetryInitializers.Add(new IGXTelemetryInitializer());

			ViewEngines.Engines.Clear();
			ViewEngines.Engines.Add(new CMSMobileRazorViewEngine());

			AreaRegistration.RegisterAllAreas();

			FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
			RouteConfig.RegisterRoutes(RouteTable.Routes);
			BundleConfig.RegisterBundles(BundleTable.Bundles);

			//delete the temp xslt folder, this will make the start up slower though
			string dssTempFolder = Server.MapPath("~/App_Data/_dss_temp_stylesheets_");
			//bypass if delete can't happen, make the site start anyway
			try
			{
				foreach (string f in Directory.GetFiles(dssTempFolder))
				{
					File.SetAttributes(f, FileAttributes.Normal);
					File.Delete(f);
				}

				Directory.Delete(dssTempFolder, true);
			}
			catch { }
		}

		public override string GetVaryByCustomString(HttpContext context, string custom)
		{
			HashSet<string> customStrs = new HashSet<string>(custom
							.ToNullHelper()
							.Propagate(
								c => c.ToLowerInvariant().Split(',')
									.Select(
										s => s.Trim())
									.Where(
										s => !string.IsNullOrWhiteSpace(s)))
							.Return(new string[0]));

			string varyStr = base.GetVaryByCustomString(context, custom);

			if (customStrs.Contains("rta"))
			{
				UserData rtaSession = AuthenticationManager.GetRuntimeAuthenticationUserData(context.Request);

				varyStr += "|" + (rtaSession != null ?
					rtaSession.ToJson() : string.Empty);

				//get cookie settings
				string cookies = ConfigurationManager.AppSettings["CacheVariationCookieNames"] ?? string.Empty;

				if (!string.IsNullOrWhiteSpace(cookies))
				{
					varyStr += string.Join("|",
						cookies.Split(';')
							.Select(
								cName => cName.Trim())
							.Where(
								cName => !string.IsNullOrWhiteSpace(cName))
							.Select(
								cName => context.Request.Cookies[cName]
									.ToNullHelper()
									.Propagate(
										c => c.Value)
									.Return(string.Empty)));
				}
			}

			if (customStrs.Contains("scheme"))
			{
				string forwardHeader = Request.ServerVariables["HTTP_X_FORWARDED_PROTO"];
				string urlScheme = !string.IsNullOrWhiteSpace(forwardHeader)
					? forwardHeader : Request.Url.Scheme;

				varyStr += "|" + urlScheme;
			}

			return varyStr;
		}

		protected void Application_PostAcquireRequestState()
		{
			var requestTelemetry = Context.GetRequestTelemetry();

			if (HttpContext.Current.Session != null && requestTelemetry != null && string.IsNullOrEmpty(requestTelemetry.Context.User.Id))
			{
				requestTelemetry.Context.User.Id = Session.SessionID;
			}
		}
	}
}