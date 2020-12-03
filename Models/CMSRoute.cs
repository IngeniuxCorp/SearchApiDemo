using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Xml.Linq;
using Ingeniux.Preview;
using Ingeniux.Runtime.Controllers;

namespace Ingeniux.Runtime
{
	public class CmsRoute : RouteBase
	{
		#region Static Members
		private const string DEFAULT_CONTROLLER_NAME = "CMSPageDefault";
		private static readonly string defaultAction = "Index";
		private static string _SitePath = ConfigurationManager.AppSettings["PageFilesLocation"];
		private static CMSPageFactory _PageFactory;
		private static ControllerSummary[] controllerSummaries;

		private static string _BaseController = null;

		static CmsRoute()
		{
			_PageFactory = new CMSPageFactory(_SitePath);
			initControlleSummeries();
		}

		private static void initControlleSummeries()
		{
			controllerSummaries =
				(from c in TypeFinder.GetDerivedTypesFromAppDomain(typeof(CMSPageDefaultController))
				 where !c.IsAbstract && !c.IsInterface
				 select new ControllerSummary(c)).ToArray();
		}
		#endregion

		public static string GetSitePath() 
		{
			return CMSPageFactory.GetSitePath();
		}

		public override RouteData GetRouteData(HttpContextBase httpContext)
		{
			if (string.IsNullOrWhiteSpace(_BaseController))
			{
				_BaseController = ConfigurationManager.AppSettings["BaseControllerName"]
					.ToNullOrEmptyHelper()
					.Return(DEFAULT_CONTROLLER_NAME);
			}

			ICMSRequest pageRequest = null;
			RouteData data = null;
			string action = defaultAction;
			string controller = _BaseController;
			
			string appPath = httpContext.Request.ApplicationPath;
			if (appPath == "/")
				appPath = string.Empty;

			string relativePath = httpContext.Request.Path.Substring(appPath.Length).ToLowerInvariant();
			bool isPreview = httpContext.Request.QueryString["_previewAsset_"] == "true";
			isPreview |= Reference.Reference.IsDesignTime(_SitePath);

			if (relativePath.StartsWith("/igxdynamicpreview") || isPreview || _PageFactory == null)
			{
				action = "DynamicPreview";
				try
				{
					CMS.IContentStore contentStore = CMSPageFactoryHelper.GetPreviewContentStore(httpContext.Request.RequestContext, _SitePath);
					CMS.IReadonlyUser currentUser = CMSPageFactoryHelper.GetPreviewCurrentUser(contentStore,
						httpContext.Request.RequestContext, httpContext.Session);

					_PageFactory = new DocumentPreviewPageFactory(contentStore, currentUser, _SitePath);
					_PageFactory.CacheSiteControls = false;

					pageRequest = _PageFactory.GetDynamicPreviewPage(httpContext.Request, true, true);

					if (pageRequest == null)
					{
						var map = _PageFactory.AssetMap;
						string path = httpContext.Request.GetRelativePath().Trim().ToLowerInvariant();
						path = HttpUtility.UrlDecode(path);
						var asset = map.GetAssetByPath(path);
						if (asset != null)
							data = createRouteData("Get", "AssetAsync");
						else
							data = createRouteData(defaultAction, controller);
						return data;
					}
					// because the preview does not get a url, but rather an id and xml, 
					// it is not possible to look for other actions based on the URL

					locateController(pageRequest, ref controller);
				}
				catch
				{
					controller = "CMSPageDefault";
				}
				data = createRouteData(action, controller);
			}
			else
			{
				_PageFactory = new CMSPageFactory(_SitePath);

				//only need the schema data, not the whole page
				ICMSRoutingRequest routingRequest = _PageFactory.GetPage(httpContext.Request, true);
				if (routingRequest != null && routingRequest.CMSRequest != null && routingRequest.CMSRequest.Exists)
				{
					pageRequest = routingRequest.CMSRequest;
					if (pageRequest != null)
					{
						//use default controller for routing request
						if (pageRequest is CMSPageRedirectRequest)
							data = createRouteData(defaultAction, controller);
						else
						{
							var matchingController = locateController(pageRequest, ref controller);

							if (!string.IsNullOrEmpty(routingRequest.RemaingPath.Trim('/')))
							{
								//use the first section of the remaining path as action, this is arbitrary.
								string actionInPath = routingRequest.RemaingPath.Trim('/').SubstringBefore("/");

								//if the controller doesn't have this action, use default action
								action = matchingController.ToNullHelper()
									.Propagate(
										c => c.ActionNames
											.Where(an => an.ToLowerInvariant() == actionInPath.ToLowerInvariant())
											.FirstOrDefault())
									.Return(defaultAction);

								if (action == defaultAction)
								{
									var map = _PageFactory.AssetMap;
									string path = httpContext.Request.GetRelativePath().Trim().ToLowerInvariant();
									path = HttpUtility.UrlDecode(path);
									var asset = map.GetAssetByPath(path);
									if (asset != null)
										return createRouteData("Get", "AssetAsync");
								}
								//get the remaining path in the controller action itself
							}
							else
							{
								action = defaultAction;
							}

							data = createRouteData(action, controller);
						}
					}
					else
					{
						data = createRouteData(defaultAction, controller);
					}
				}
				//else if (routingRequest.CMSRequest == null)
				//{
				//	data = createRouteData("Get", "AssetAsync");
				//}
				else
				{
					var map = _PageFactory.AssetMap;
					string path = httpContext.Request.GetRelativePath().Trim().ToLowerInvariant();
					path = HttpUtility.UrlDecode(path);
					var asset = map.GetAssetByPath(path);
					if (asset != null)
						data = createRouteData("Get", "AssetAsync");
					else
						data = createRouteData(defaultAction, controller);
				}
			}

			return data;
		}

		private RouteData createRouteData(string action, string controller)
		{
			MvcRouteHandler mvcHandler = new MvcRouteHandler(new DefaultControllerFactory());
			var data = new RouteData(this, mvcHandler);
			data.Values["action"] = action;

			//this is the value that will get passed to the ControllerFactory by MVC
			data.Values["controller"] = controller;
			return data;
		}

		private static ControllerSummary locateController(ICMSRequest pageRequest, ref string controller)
		{
			var schema = (pageRequest as ICMSPageTypeRequest).Schema;

			var matchingController = controllerSummaries.Where(
					c => !string.IsNullOrEmpty(schema) && c.Name == schema + "Controller")
				.FirstOrDefault();

			if (matchingController != null)
			{
				controller = schema;
			}
			return matchingController;
		}

		public override VirtualPathData GetVirtualPath(RequestContext requestContext, RouteValueDictionary values)
		{
			return new VirtualPathData(this, string.Empty);
		}
	}
}
