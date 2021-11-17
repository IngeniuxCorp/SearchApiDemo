using System.Net.Http.Headers;
using System.Web.Http;

namespace Ingeniux.Runtime
{
	public static class WebApiConfig
	{
		public static void Register(HttpConfiguration config)
		{
			// TODO: Add any additional configuration code.

			// Web API routes
			config.MapHttpAttributeRoutes();

			config.Routes.MapHttpRoute(
				name: "DefaultApi",
				routeTemplate: "api/{controller}/{id}",
				defaults: new { id = RouteParameter.Optional }
			);

			// WebAPI when dealing with JSON & JavaScript!
			// Setup json serialization to serialize classes to camel (std. Json format)
			var formatter = GlobalConfiguration.Configuration.Formatters.JsonFormatter;

			formatter.SerializerSettings.ContractResolver =
				new Newtonsoft.Json.Serialization.DefaultContractResolver();

			//config.Formatters.JsonFormatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("text/html"));
			//remove xml support to force Json
			config.Formatters.XmlFormatter.SupportedMediaTypes.Clear();

			var json = GlobalConfiguration.Configuration.Formatters.JsonFormatter;
			json.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
			json.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
		}
	}
}