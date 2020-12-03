using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Web;
using System.Web.Mvc;
using Ingeniux.Runtime;
using Ingeniux.Runtime.Models;
using System.Configuration;
using System.IO;
using System.Globalization;
using System.Web.Mvc.Html;
using Ingeniux.Runtime.XSLT.ICE;
using Ingeniux.Preview;
using Ingeniux.Runtime.RenderingEngine;
using System.Text;
using Ingeniux.Runtime.DesignTime.Presentation;
using Ingeniux.Search;

namespace Ingeniux.Runtime.Controllers
{
	/// <summary>
	/// This is the default controller for CMS pages
	/// It uses the route map file to locate page id with route
	/// then retrieve the Xml Document for given id and pass it down as model for view
	/// It will use the root element name as view name
	/// this file will be deleted on upgrade, so please inherit from CMSPageDefaultController
	/// and overinstead of making direct changes
	/// </summary>
	[SessionState(System.Web.SessionState.SessionStateBehavior.ReadOnly)]
	public class CMSPageDefaultController : Controller
	{
		public static SiteSearch GetSiteSearch()
		{
			var sitePath = CmsRoute.GetSitePath();

			initSiteSearch(sitePath);

			return _SiteSearch;

		}

		static readonly object loc = new object();
		protected ICMSRoutingRequest _CMSPageRoutingRequest;
		internal string _SitePath;
		internal string _DTSitePath;
		private const string PAGEFACTORY_APPSTATE_NAME = "PageFactory";
		internal CMSPageFactory _PageFactory;
		protected bool _AllowTfrm = true;
		internal bool _LegacyTransformation = true;
		private bool _IsDTAuthenticated;
		private bool _IsDesignTime = false;
		private XmlResult _InvalidPageResult;
		private bool _UseTempStylesheetsLocation;
		public SiteSearch SiteSearch { get
			{
				return _SiteSearch;
			}
		}

		static SiteSearch _SiteSearch;
		static void initSiteSearch(string sitePath)
		{
			if (_SiteSearch == null)
				lock (loc)
					if (_SiteSearch == null)
					{
						SearchIndexer searchIndexer = Ingeniux.Search.SearchIndexer.Get(RuntimeLogger.GetLogger(sitePath));
						_SiteSearch = new SiteSearch(searchIndexer.Logger);
						
						System.Web.HttpContext.Current.Application["SiteSearch"] = _SiteSearch;

						//start indexing service, use runtime logger
						searchIndexer.Start(_SiteSearch);
					}
		}

		public bool IsDTAuthenticated
		{
			get { return _IsDTAuthenticated; }
		}

		public bool IsDesignTime
		{
			get { return _IsDesignTime; }
		}

		public bool XsltTransformAtTempLocation
		{
			get
			{
				return _UseTempStylesheetsLocation;
			}
		}

		protected override void OnActionExecuting(ActionExecutingContext filterContext)
		{
			base.OnActionExecuting(filterContext);
			if (_IsDesignTime && !_IsDTAuthenticated)
				filterContext.Result = new DesignTimeNotAuthenticatedResult();
		}

		internal virtual void init(System.Web.Routing.RequestContext requestContext)
		{
			this.Initialize(requestContext);
		}

		protected override void Initialize(System.Web.Routing.RequestContext requestContext)
		{
			base.Initialize(requestContext);

			_SitePath = CmsRoute.GetSitePath();

			initSiteSearch(_SitePath);

			_DTSitePath = ConfigurationManager.AppSettings["DesignTimeAssetsLocation"]
				.ToNullOrEmptyHelper()
				.Return(_SitePath);
			_AllowTfrm = ConfigurationManager.AppSettings["EnableTFRMParameter"].ToNullHelper().Branch(
				str => str.Trim().ToLowerInvariant() == "true",
				() => true);
			_LegacyTransformation = ConfigurationManager.AppSettings["LegacyRendering"].ToNullHelper()
				.Branch(
					str => str.Trim().ToLowerInvariant() != "false",
				() => true);

			_UseTempStylesheetsLocation = ConfigurationManager.AppSettings["UseTempStylesheetsLocation"].ToNullHelper().Branch(str => str.Trim().ToLowerInvariant() != "false", () => true);

			_IsDesignTime = Reference.Reference.IsDesignTime(_SitePath);

			try
			{

				//page factory is a cheap object to construct, since both surl map and ssmap are cached most time
				_PageFactory = CMSPageFactoryHelper.GetPageFactory(_IsDesignTime, _SitePath, requestContext, _LegacyTransformation, out _IsDTAuthenticated) as CMSPageFactory;
			}
			catch (Ingeniux.CMS.Exceptions.LicensingException e)
			{
				if (e.Expired)
				{
					_InvalidPageResult = new XmlResult(
						new XElement("TrialLicenseExpired",
							new XAttribute("ExpirationDateUTC", e.ExpirationDate.ToIso8601DateString(true)),
							new XElement("IngeniuxSupport",
								new XAttribute("Phone", "1.877.299.8900"),
								new XAttribute("Email", "support@ingeniux.com"),
								new XAttribute("Portal", "http://support.ingeniux.com/"))));
				}
				else
				{
					_InvalidPageResult = new XmlResult(
						new XElement("InvalidCMSLicense",
							new XElement("IngeniuxSupport",
								new XAttribute("Phone", "1.877.299.8900"),
								new XAttribute("Email", "support@ingeniux.com"),
								new XAttribute("Portal", "http://support.ingeniux.com/"))));
				}
			}

			if (requestContext.HttpContext.Request.HttpMethod.Equals("options", StringComparison.InvariantCultureIgnoreCase))
			{
				requestContext.RouteData.Values["action"] = "Options";
				return;
			}

			if (_InvalidPageResult == null)
			{
				if (_PageFactory.CacheSiteControls)
				{
					// only set site control schemas when list is not empty
					string[] siteControlSchemas = ConfigurationManager.AppSettings["SiteControlSchemas"].ToNullHelper().Branch<string[]>(
						str => str.Split(';').Select(
							s => s.Trim()).Where(
							s => s != "").ToArray(),
						() => new string[] { });

					if (siteControlSchemas.Length > 0)
						_PageFactory.SiteControlSchemas = siteControlSchemas;
				}

				_PageFactory.LocalExportsIncludeLinks = ConfigurationManager.AppSettings["LocalExportsIncludeLinks"].ToNullHelper().Branch<bool>(
					str => str.Trim().ToLowerInvariant() == "true",
					() => true);

				//move page getting to here so it can be used to OnActionExecuting
				try
				{
					_CMSPageRoutingRequest = _PageFactory.GetPage(Request, false);
				}
				catch (DesignTimeInvalidPageIDException exp)
				{
					//this message is exclusive to design time and need to present a special page
					_InvalidPageResult = new XmlResult(new XElement("DynamicPreviewError", exp.Message));
				}
			}

			if (_CMSPageRoutingRequest != null && string.IsNullOrWhiteSpace(_CMSPageRoutingRequest.RemaingPath))
				HttpContext.Items["PageRequest"] = _CMSPageRoutingRequest;
		}

		[HttpOptions]
		public virtual ActionResult Options()
		{
			string path = Request.GetRelativePath().Trim().ToLowerInvariant();
			path = HttpUtility.UrlDecode(path);
			path = "/" + path.TrimStart('/');
			var urlData = _PageFactory.UrlMap.GetPageDataByPath(path);
			if (urlData == null)
			{
				throw new HttpException(404, string.Empty);
			}

			Response.AppendHeader("Access-Control-Allow-Methods", "HEAD, GET, POST, OPTIONS");
			return Content(string.Empty);
		}



		/// <summary>
		/// The action to channel all CMS requests
		/// </summary>
		/// <returns></returns>
		[IGXRuntimeCache(VaryByHeader = "User-Agent", VaryByCustom = "RTA,Scheme", Duration = 5)]
		[IngeniuxPageBuilderRenderingFilter]
		public virtual ActionResult Index()
		{
			return handleRequest(handleNoneCmsPageRoute, handleCmsPageRequestWithRemainingPath, handleStandardCMSPageRequest);
		}

		protected virtual ActionResult handleRequest(Func<ICMSRoutingRequest, ActionResult> nonCmsRouteHandle, Func<CMSPageRequest, string, ActionResult> remainingPathHandle, Func<CMSPageRequest, ActionResult> standardCmsPageHandle)
		{
			if (_InvalidPageResult != null)
				return _InvalidPageResult;

			if (_AllowTfrm)
			{
				//exception for settings.xml and urlmap.xml, allow them to display only if 
				string path = Request.Url.AbsolutePath.ToLower();
				if (path.EndsWith("settings/settings.xml") || path.EndsWith("settings/urlmap.xml"))
				{
					var relativePath = path.Substring(Request.ApplicationPath.Length);
					string filePath = Path.Combine(_SitePath, relativePath.TrimStart('/').Replace("/", @"\"));
					if (System.IO.File.Exists(filePath))
					{
						return new XmlResult(XDocument.Load(filePath));
					}
				}
			}

			if (_CMSPageRoutingRequest.CMSRequest == null || !_CMSPageRoutingRequest.CMSRequest.Exists)
			{
				return nonCmsRouteHandle(_CMSPageRoutingRequest);
			}

			ICMSRequest cmsRequest = _CMSPageRoutingRequest.CMSRequest;

			//if the request is for redirection, do redirect
			if (cmsRequest is CMSPageRedirectRequest)
			{
				var cmsRedir = cmsRequest as CMSPageRedirectRequest;
				//string absoluteUrl = cmsRedir.FinalUrl.ToAbsoluteUrl();
				string absoluteUrl = cmsRedir.FinalUrl;
				if (!absoluteUrl.StartsWith("http://") && !absoluteUrl.StartsWith("https://"))
					absoluteUrl = VirtualPathUtility.ToAbsolute("~/" + absoluteUrl.TrimStart('/'));

				if (cmsRedir.NoRedirectCache)
				{
					Response.Expires = -1;
					Response.Cache.SetCacheability(HttpCacheability.NoCache);
				}

				if (cmsRedir.IsPermanent)
				{
					Response.RedirectPermanent(absoluteUrl);
				}
				else
				{
					Response.Redirect(absoluteUrl);
				}
				return null;
			}
			else if (cmsRequest is CMSPageRequest)
			{
				CMSPageRequest pageRequest = cmsRequest as CMSPageRequest;

				//first handle requests with remaining path, if the method doesn't return any result, proceed
				//to treat it as a standard CMS page
				string remainigPath = _CMSPageRoutingRequest.RemaingPath;
				if (!string.IsNullOrEmpty(remainigPath))
				{
					ActionResult cmsWithRemainingPathResult = remainingPathHandle(pageRequest, remainigPath);
					if (cmsWithRemainingPathResult != null)
						return cmsWithRemainingPathResult;
				}

				if (pageRequest.RestrictedAccess)
				{
					disableClientSideCaching();
				}

				return standardCmsPageHandle(pageRequest);
			}
			else
			{
				throw new HttpException(501, Ingeniux.Runtime.Properties.Resources.RequestTypeNotRecognized);
			}
		}

		private void disableClientSideCaching()
		{
			Response.Expires = 0;
			Response.Cache.SetCacheability(HttpCacheability.NoCache);
			Response.Cache.SetNoStore();
		}

		/// <summary>
		/// The method that handles standard CMS page request. Subclass can choose to override to change its behavior.
		/// </summary>
		/// <param name="pageRequest"></param>
		/// <returns></returns>
		internal virtual ActionResult handleStandardCMSPageRequest(CMSPageRequest pageRequest)
		{
            //if the transform option is 4 or 5, return xml directly, also, bypass caching
            if (pageRequest.IsComponent || _AllowTfrm && (pageRequest.TransformOption == TransformOptions.ExpansionOnly || pageRequest.TransformOption == TransformOptions.Raw))
            {
                return new XmlResult(pageRequest.ContentDocument);
            }
            else if (pageRequest.IsComponent || _AllowTfrm && (pageRequest.TransformOption == TransformOptions.JsonExpansion))
            {
                return new JsonNetResult()
                {
                    Data = pageRequest.ContentDocument.Root,
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            else
            {
                if (!_IsDesignTime)
                {
                    StructureUrlMap urlMap = _PageFactory.UrlMap;

                    string triggerFile = System.IO.Path.Combine(urlMap.SitePath, urlMap.Settings.GetSetting<string>("RuntimeCache", "TriggerFile"));

                    IGXPageLevelCache pageLevelCache = IGXPageLevelCache.Get(triggerFile);

                    //add page level caching information, first request to the same page will always executing. 2nd request will set the cachability
                    pageLevelCache.CheckPageCache(Request.Path.ToLowerInvariant(), pageRequest.Content, pageRequest.NavigationUsingCacheContent);
                }

                //locate the view based on root element name, and pass in the XElement of page as model
                return viewOrXsltFallback(pageRequest);
            }
		}

		/// <summary>
		/// Get called when routingRequest not pointing to CMS page
		/// Default behavior is to introduce 404 error. 
		/// </summary>
		/// <param name="routingRequest"></param>
		protected virtual ActionResult handleNoneCmsPageRoute(ICMSRoutingRequest routingRequest)
		{
			//throw 404 error, and let the 404 page handle it
			throw new HttpException(404, Ingeniux.Runtime.Properties.Resources.CannotLocateIngeniuxCMSPage);
		}

		/// <summary>
		/// This is the method to process request with remaining path.
		/// Requests with remaining path are routes that will live under a CMS page, and need to use the CMS parent page content
		/// for part of rendering.
		/// By default, it is considered as non-existing route and will be throwing 404. Implementer can indeed customize this method directly
		/// or have sub class to override it.
		/// </summary>
		/// <param name="pageRequest"></param>
		/// <param name="remainingPath"></param>
		/// <remarks>If remainging path request is to be handled for specific subclass controller, suggest to call "handleStandardCMSPageRequest"
		/// and add additional information on the ActionResult (view data)</remarks>
		/// <returns></returns>
		protected virtual ActionResult handleCmsPageRequestWithRemainingPath(CMSPageRequest pageRequest, string remainingPath)
		{
			//throw 404 error, and let the 404 page handle it
			throw new HttpException(404, Ingeniux.Runtime.Properties.Resources.CannotLocateIngeniuxCMSPage);
		}

		/// <summary>
		/// The standard CMS page handling. If a view with the root element name exists, use it. Otherwise, try to transform
		/// the page xml content with XSLT, via traditional MSXML 4 DOM, like the old CMS Runtime engine.
		/// This ensures an existing site can start using the new MVC runtime without any work, and slowly convert to full MVC site page type by page type.
		/// 
		/// </summary>
		/// <param name="pageRequest"></param>
		/// <remarks>This method generally doesn't need overriding at all. Only override it is additional data need to be passed down to view. (e.g Cartella data).e</remarks>
		/// <returns></returns>
		internal virtual ActionResult viewOrXsltFallback(CMSPageRequest pageRequest)
		{
			CMSPageRenderingEngine renderEngine = pageRequest.IsPreview ?
				new PreviewPageRenderingEngine(this,
					(pageRequest as CMSPageDocumentDynamicPreviewRequest).ContentStore,
					(pageRequest as CMSPageDocumentDynamicPreviewRequest).CurrentUser,
					_SitePath, _DTSitePath, _UseTempStylesheetsLocation,
					_LegacyTransformation) :
				new CMSPageRenderingEngine(
				this, _SitePath, _DTSitePath, _UseTempStylesheetsLocation, _LegacyTransformation);

			HttpContext.Items["PageRequest"] = pageRequest;

			CMSPageRequest model = pageRequest;
			if (pageRequest is ContentUnitPreviewRequest)
			{
				model = (pageRequest as ContentUnitPreviewRequest).Root as CMSPageRequest;
				model.Tag = "CUPreview";
			}

			return renderEngine.Render(model);
		}

		internal void exception(ExceptionContext filterContext)
		{
			this.OnException(filterContext);
		}

		/// <summary>
		/// Handle 404 exception and redirect it to the 404 error page, and fallback to NotFoundError view
		/// </summary>
		/// <param name="filterContext"></param>
		protected override void OnException(ExceptionContext filterContext)
		{
			//handle 404 exception
			if (filterContext.Exception is HttpException)
			{
				HttpException httpException = filterContext.Exception as HttpException;
				if (httpException.GetHttpCode() == 404)
				{
					filterContext.HttpContext.Response.StatusCode = 404;
					//add 404 page handling specified by url map

					string error404PageId = _PageFactory.UrlMap
						.ToNullHelper()
						.Propagate(
							urlMap => urlMap.Error404PageId)
						.Return(string.Empty);

					if (!string.IsNullOrEmpty(error404PageId))
					{
						CMSPageRequest page404 = _PageFactory.GetPage(Request, error404PageId) as CMSPageRequest;
						if (page404 != null && page404.Exists)
						{
							filterContext.ExceptionHandled = true;

							ActionResult result404 = viewOrXsltFallback(page404);
							result404.ExecuteResult(ControllerContext);
							return;
						}
					}

					//go to provided Error view, structured url map 404 page is not used, only if the view exists
					//otherwise, use base procedure
					ViewResult viewResult = View("NotFoundError", new Error404()
					{
						Title = Ingeniux.Runtime.Properties.Resources.Error404Title,
						BodyCopy = Ingeniux.Runtime.Properties.Resources.Error404BodyCopy,
						Factory = _PageFactory
					});

					ViewEngineResult viewEngineResult = viewResult.ViewEngineCollection.FindView(ControllerContext, viewResult.ViewName, viewResult.MasterName);
					if (viewEngineResult != null && viewEngineResult.View != null && viewEngineResult.ViewEngine != null)
					{
						filterContext.ExceptionHandled = true;

						viewResult.ExecuteResult(ControllerContext);
						return;
					}
				}
			}

			base.OnException(filterContext);
		}


		/// <summary>
		/// The action to handle page preview with post in xml content and override request parameters
		/// Recommand to use base functionalities in subclass or not overriding it.
		/// </summary>
		/// <returns></returns>
		[AcceptVerbs(HttpVerbs.Post), ValidateInput(false)]
		public virtual ActionResult Preview()
		{
			CMSPagePreviewRequest previewRequest = _PageFactory.GetPreviewPage(Request, false) as CMSPagePreviewRequest;
			return viewOrXsltFallback(previewRequest);
		}

		static readonly object dynamicPreviewLoc = new object();

		/// <summary>
		/// The action to handle page dynamic preview. information passed in via querystrings
		/// </summary>
		/// <returns></returns>
		[ValidateInput(false)]
		[IngeniuxPageBuilderRenderingFilter]
		public virtual ActionResult DynamicPreview()
		{
			if (_InvalidPageResult != null)
				return _InvalidPageResult;

			CMSPageDocumentDynamicPreviewRequest dynamicPreviewRequest;
			//dynamic preview can only be done one at a time, since they share the same navigation builder, which needs the pub target context
			//multi-thread it will cause mix up of urls from different pub targets
			if (_CMSPageRoutingRequest != null)
				dynamicPreviewRequest = _CMSPageRoutingRequest.CMSRequest as CMSPageDocumentDynamicPreviewRequest;
			else
				dynamicPreviewRequest = _PageFactory.GetDynamicPreviewPage(Request, false) as CMSPageDocumentDynamicPreviewRequest;

			//no caching for preview
			disableClientSideCaching();		

			if (dynamicPreviewRequest.IsComponent ||
				_AllowTfrm && (dynamicPreviewRequest.TransformOption == TransformOptions.ExpansionOnly ||
				dynamicPreviewRequest.TransformOption == TransformOptions.Raw))
			{
				return new XmlResult(dynamicPreviewRequest.ContentDocument);
			}
			else if (dynamicPreviewRequest.TransformOption == TransformOptions.Default)
			{
				return handleStandardCMSPageRequest(dynamicPreviewRequest);
			}
			else
			{
				return new PreviewXsltTransformResult(dynamicPreviewRequest, _SitePath, false);
			}
		}

		[ValidateInput(false)]
		[IngeniuxPageBuilderRenderingFilter]
		public virtual ActionResult DynamicDiffPreview()
		{
			if (_InvalidPageResult != null)
				return _InvalidPageResult;

			CMSPageDocumentDynamicPreviewDiffRequest dynamicPreviewRequest = 
				(_PageFactory as DocumentPreviewPageFactory).GetDynamicPreviewDiffPage(Request) as CMSPageDocumentDynamicPreviewDiffRequest;

			//no caching for preview
			disableClientSideCaching();

			if (dynamicPreviewRequest.IsComponent ||
				_AllowTfrm && (dynamicPreviewRequest.TransformOption == TransformOptions.ExpansionOnly ||
				dynamicPreviewRequest.TransformOption == TransformOptions.Raw))
			{
				return new XmlResult(dynamicPreviewRequest.ContentDocument);
			}
			else if (dynamicPreviewRequest.TransformOption == TransformOptions.Default)
			{
				return handleStandardCMSPageRequest(dynamicPreviewRequest);
			}
			else
			{
				return new PreviewXsltTransformResult(dynamicPreviewRequest, _SitePath, false);
			}
		}

		/// <summary>
		/// Preview the HTML output of a Content Unit for previews
		/// </summary>
		/// <returns></returns>
		[ValidateInput(false)]
		public virtual ActionResult ContentUnitPreview()
		{
			if (!(_PageFactory is DocumentPreviewPageFactory))
				return new XmlResult(
					new XElement("ContentUnitPreviewError", "Invalid Preview Context."), 500);

			try
			{
				ContentUnitPreviewRequest cuPreview = (_PageFactory as DocumentPreviewPageFactory)
					.GetContentUnitPreview(Request);

				bool viewXml = Request["xml"] == "true";

				return viewXml ?
					new PreviewXsltTransformResult(cuPreview, _SitePath, true, true) :
					viewOrXsltFallback(cuPreview);
			}
			catch (Exception e)
			{
				return new XmlResult(
					new XElement("ContentUnitPreviewError", e.Message), 500);
			}
		}

		/// <summary>
		/// The action to handle xml preview in dynamic preview mode. It actually returned html
		/// </summary>
		/// <returns></returns>
		[AcceptVerbs(HttpVerbs.Get)]
		public virtual ActionResult DynamicXmlPreview()
		{
			try
			{
				if (_InvalidPageResult != null)
				{
					if (Request.QueryString["viewSource"] == "true")
					{
						_InvalidPageResult.ContentType = "text/plain";
						return _InvalidPageResult;
					}
					else
						return new TransformedXmlResult(_InvalidPageResult.ResultDocument, true);
				}

				CMSPageDocumentDynamicPreviewRequest dynamicPreviewRequest;
				if (_CMSPageRoutingRequest != null)
					dynamicPreviewRequest = _CMSPageRoutingRequest.CMSRequest as CMSPageDocumentDynamicPreviewRequest; 
				else
					dynamicPreviewRequest = _PageFactory.GetDynamicPreviewPage(Request, false) as CMSPageDocumentDynamicPreviewRequest;

				disableClientSideCaching();

				if (Request.QueryString["viewSource"] == "true")
					return new XmlResult(dynamicPreviewRequest.ContentDocument, "text/plain"); //view source will set content to plain text to prevent browser formatting
				else
					return new PreviewXsltTransformResult(dynamicPreviewRequest, _SitePath);
			}
			catch (Exception e)
			{
				XElement resp = new XElement("IGX_DynamicPreviewError",
					new XElement("Message", "Error performing XML preview:" + e.Message));

#if DEBUG
				resp.Add(new XElement("StrackTrace", new XCData(e.StackTrace)));
#endif
				if (Request.QueryString["viewSource"] == "true")
					return new XmlResult(resp, "text/plain"); //view source will set content to plain text to prevent browser formatting
				else
					return new TransformedXmlResult(new XDocument(resp), true);
			}
		}

		/// <summary>
		/// get updated content for a given field on the page.
		/// This method doesn't allow override
		/// </summary>
		/// <returns></returns>
		[AcceptVerbs(HttpVerbs.Post), ValidateInput(false)]
		public CommentFilteredJsonResult IceUpdate()
		{
			CommentFilteredJsonResult result = new CommentFilteredJsonResult();

			disableClientSideCaching();

			try
			{

				//XDocument contentDoc = null;
				//if (_CMSPageRoutingRequest != null)
				//{
				//	//initialize method already processed the request and had a content document already, use it
				//	CMSPageDocumentDynamicPreviewRequest dynamicPreviewRequest = _CMSPageRoutingRequest.CMSRequest as CMSPageDocumentDynamicPreviewRequest;
				//	contentDoc = dynamicPreviewRequest.ContentDocument;
				//}
				//ice update needs to consider content units scenario, so it will look into presentations folder.
				ContentUnitControllerContext controllerContext = new ContentUnitControllerContext(ControllerContext);				

				DocumentPreviewIceFieldMarkupUpdateRequest iceUpdateRequest = _PageFactory.GetIceFieldMarkupUpdater(Request, null) as DocumentPreviewIceFieldMarkupUpdateRequest;

				string fieldViewName = ControllerContext.Controller.GetAvailableView(iceUpdateRequest, controllerContext);

				string contentStr;

				//when not able to locate the template in both editable and normal path, try to use the default fallback templates for text and html
				if (ControllerContext.Controller.ViewExists(fieldViewName, controllerContext))
				{
					using (StringWriter sw = new StringWriter(CultureInfo.InvariantCulture))
					{
						HtmlHelper html = new HtmlHelper(new ViewContext(controllerContext, new WebFormView(ControllerContext, fieldViewName), new ViewDataDictionary() { },
							new TempDataDictionary() { }, sw), new ViewPage());

						contentStr = Server.HtmlDecode(html.Partial(fieldViewName, iceUpdateRequest).ToHtmlString());
					}
				}
				else
				{
					DynamicPreviewICEProcessor iceUpdateProc = new DynamicPreviewICEProcessor(_SitePath,
						iceUpdateRequest.ContentStore, iceUpdateRequest.CurrentUser);
					InContextEditUpdateInfo iceInfo = iceUpdateProc.IceUpdatePreparation(iceUpdateRequest);

					if (iceInfo == null)
						throw new ArgumentException("Cannot locate either MVC view or Xslt template for element \"" + iceUpdateRequest.FieldName + "\". This field cannot be edited. This is an error of site implementation.");

					//use classic transformation engine, even though slower, make sure work with all preview cases
					LegacyTransformationEngine transformer = new LegacyTransformationEngine(_SitePath, string.Empty, true, _UseTempStylesheetsLocation);

					//during ice update preparation, updated xslts werer already processed, we don't need to process it again.
					bool ssUpdated = false;
					contentStr = transformer.Transform(iceInfo.Field, iceInfo.MainStylesheet.FilePath, iceInfo.MainStylesheet.Doc, ssUpdated);

				}

				//only process pretentation if instructed
				if (iceUpdateRequest.PresentationFragTemplate != null)
				{
					contentStr = processComponentUnitInstance(iceUpdateRequest, contentStr);
				}
				else if (iceUpdateRequest.PresentationInformation != null)
				{
					//process templates. this is a tricky scenario. we will need to locate a content unit place holder that has "ElementId" value matching the field unique id
					//only take that part of the presentation and apply to the expanded string's id
					contentStr = processContentUnit(iceUpdateRequest, contentStr);
				}

				result.Data = new XHRResponse(
					new Dictionary<string, object>() 
					{
						{"newMarkup", contentStr},
						//{"fieldXml", iceUpdateRequest.Content.ToString(SaveOptions.DisableFormatting)}
						{"fieldXml", iceUpdateRequest.ExpandedXml}
					});
			}
			catch (Exception e)
			{
				result.Data = new XHRResponse(
					e.Message, XHRResponseType.PROCESSING_ERROR);
			}

			return result;
		}

		private string processContentUnit(DocumentPreviewIceFieldMarkupUpdateRequest iceUpdateRequest, string contentStr)
		{
			StringBuilder contentStrInput = new StringBuilder(contentStr);

			TextWriter outputWriter = new StringWriter();

			InContextEditTemplateExpansionEngine expander = new InContextEditTemplateExpansionEngine(
				iceUpdateRequest, contentStrInput, outputWriter, this);

			expander.Process();

			contentStr = outputWriter.ToString();
			return contentStr;
		}

		private string processComponentUnitInstance(DocumentPreviewIceFieldMarkupUpdateRequest iceUpdateRequest, string contentStr)
		{
			StringBuilder contentStrInput = new StringBuilder(contentStr);

			TextWriter outputWriter = new StringWriter();

			PresentationFragmentTemplateExpansionEngine expander = new PresentationFragmentTemplateExpansionEngine(
				iceUpdateRequest, contentStrInput, outputWriter, this);

			expander.Process();

			contentStr = outputWriter.ToString();
			return contentStr;
		}
	}
}
