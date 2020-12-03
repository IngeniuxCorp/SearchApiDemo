using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using System.Threading.Tasks;
using System.IO;
using Ingeniux.Runtime.RuntimeAuth;
using Ingeniux.Runtime.Models;
using Lib.Web.Mvc;
using Ingeniux.CMS;
using System.Text.RegularExpressions;

namespace Ingeniux.Runtime.Controllers
{
	[SessionState(System.Web.SessionState.SessionStateBehavior.ReadOnly)]
	public class AssetAsyncController : AsyncController
	{
		public AuthenticationManager Authman;
		bool isPreviewRequest = false;
		public string AssetBasePath { get; protected set; }
		protected string _XmlFolderLocation;
		protected ContentStore _ContentStore;
		protected IReadonlyUser _CurrentUser;
		protected override void Initialize(System.Web.Routing.RequestContext requestContext)
		{
			base.Initialize(requestContext);

			string sitePath = CmsRoute.GetSitePath();

			isPreviewRequest = requestContext.HttpContext.Request.QueryString["_previewAsset_"] == "true";
			isPreviewRequest |= Reference.Reference.IsDesignTime(sitePath);

			if (isPreviewRequest)
			{
				HttpApplicationStateBase application = requestContext.HttpContext.Application;
				_GetContentStore(requestContext, application);
			}

			//check if the asset followed with querystring "previewAsset"
			AssetBasePath = (isPreviewRequest) ?
				ConfigurationManager.AppSettings["DesignTimeAssetsLocation"] :
				sitePath;

			if (string.IsNullOrEmpty(AssetBasePath))
				AssetBasePath = sitePath;

			Authman = AuthenticationManager.Get(sitePath);
		}

		public virtual void _GetContentStore(System.Web.Routing.RequestContext requestContext, HttpApplicationStateBase application)
		{
			//xml folder is in the app data folder now.
			_XmlFolderLocation = ConfigurationManager.AppSettings["PageFilesLocation"];

			_ContentStore = _ContentStore ?? CMSPageFactoryHelper.GetPreviewContentStore(requestContext, _XmlFolderLocation) as ContentStore;
			_CurrentUser = _CurrentUser ?? CMSPageFactoryHelper.GetPreviewCurrentUser(_ContentStore,
				requestContext, requestContext.HttpContext.Session);
		}

		private bool changed(DateTime modificationDate)
		{
			string modifiedHeaderValue = Request.Headers["If-Modified-Since"]
				.ToNullOrEmptyHelper()
				.Return(Request.Headers["If-Range"]);

			if (string.IsNullOrWhiteSpace(modifiedHeaderValue))
				return true;

			var modifiedSince = DateTime.MinValue;
			try
			{
				modifiedSince = DateTime.Parse(modifiedHeaderValue).ToLocalTime();
			}
			catch (Exception e) { }

			TimeSpan modifiedDuration = (modificationDate - modifiedSince);

			//remove the second discrepency. header value is only accurate to seconds.
			return modifiedDuration.TotalSeconds > 1;
		}
        
        public async Task<ActionResult> Get()
		{
			string path = Uri.UnescapeDataString(Request.GetRelativePath())
				.ToLowerInvariant();

			if (path.StartsWith("http:/"))
			{
				path = "http://" + path.SubstringAfter("http:/");
				Response.Redirect(path, true);
			}
			else if (path.StartsWith("https:/"))
			{
				path = "https://" + path.SubstringAfter("https:/");
				Response.Redirect(path, true);
			}
			else if (path.StartsWith("ftp:/"))
			{
				path = "ftp://" + path.SubstringAfter("ftp:/");
				Response.Redirect(path, true);
			}

			//if it is going to ICE images, use a different system, get image content from resource
			if (path.ToLowerInvariant().EndsWith("images/_ice_/play.png"))
			{
				string ext = Path.GetExtension(path);
				//string mimeType = MIMEAssisant.GetMIMEType("png");
				string mimeType = MIMEAssistant.GetMIMEType("png");

				var image = Properties.Resources.play;

				MemoryStream ms = new MemoryStream();
				image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
				ms.Position = 0;

				return File(ms, mimeType);
			}
			else
			{
				path = path.ToLowerInvariant().TrimStart('/');
				Regex trimRE = new Regex(@"^((images|documents|media)\/)?assets\/");
				if (trimRE.IsMatch(path))
					path = path.SubstringAfter("assets/");

				if (isPreviewRequest)
				{
					// Pull the "assets" off the from of the path for preview. Publish puts it in 'assets'
					path = (path.StartsWith("assets/") ? path.SubstringAfter("assets/") : path.TrimStart('/'));
					using (var session = _ContentStore.OpenReadSession(_CurrentUser))
					{
						IAsset asset = session.Site.AssetByPath(path);
						FileInfo file;
						if (asset != null)
							file = asset.File();
						else
						{
							IUnmanagedAsset unmanaged = session.UnmanagedAssetManager.UnmanagedAsset(path);
							if (unmanaged != null)
								file = unmanaged.FileInfo;
							else
								throw new HttpException(404, string.Format("Asset for Preview {0} is missing.", path));
						}

						string mime = MimeMapping.GetMimeMapping(file.FullName);
						return new RangeFilePathResult(mime, file.FullName, file.LastWriteTimeUtc, file.Length);
						//return base.File(file.FullName, mime);
					}
				}

				//else
				//{
				//	path = "assets/" + (path.StartsWith("assets/") ? path.SubstringAfter("assets/") : path.TrimStart('/'));
				//}

				string assetPath = Path.Combine(AssetBasePath, path.TrimStart('/').Replace("/", @"\"));
				assetPath = Uri.UnescapeDataString(assetPath);
				//if (!path.ToLowerInvariant().StartsWith("assets/") && !System.IO.File.Exists(assetPath))
				//{
				//	path = "assets\\" + path.TrimStart('/').Replace("/", @"\");
				//	assetPath = Path.Combine(AssetBasePath, path);
				//}
				
				Assets.AssetFactory map = Assets.AssetFactory.Get(CmsRoute.GetSitePath());
				Assets.IAsset assetMapEntry = map.GetAssetByPath(path);
				if (assetMapEntry != null)
				{
					var assetId = assetMapEntry.AssetId;
					Assets.AssetTree tree = Assets.AssetTree.Get(CmsRoute.GetSitePath());
					var assetTreeEntry = tree.GetNode(assetId);
					if (assetTreeEntry != null && !assetTreeEntry.Valid())
						throw new HttpException(404, string.Format("Asset {0} is invalid.", assetPath));

					assetPath = assetMapEntry.FilePath;

					if (assetMapEntry.IsExternal)
						Response.Redirect(assetPath);
				}
				else
					throw new HttpException(404, string.Format("Asset {0} is invalid.", assetPath));

				//block any access in settings folder
				string settingsFolder = Path.Combine(AssetBasePath, "settings").ToLowerInvariant() + @"\";
				if (assetPath.ToLowerInvariant().StartsWith(settingsFolder))
				{
					throw new HttpException((int)AssetRequestState.Forbidden, Ingeniux.Runtime.Properties.Resources.AccessToDynamicSiteServerMetaDataIsForbidd);
				}

				if (!System.IO.File.Exists(assetPath))
				{
					path = path.Replace("\\", "/");
					if (path.StartsWith("assets/"))
						path = "/" + path.SubstringAfter("assets/");
					var urlmap = Runtime.StructureUrlMap.Get(CmsRoute.GetSitePath());
					var urlmapEntry = urlmap.GetPageDataByPath(path);
					if (urlmapEntry != null)
					{
						CMSPageDefaultController pageController = initPageController();
						return pageController.Index();
					}
					else
						throw new HttpException(404, Ingeniux.Runtime.Properties.Resources.AssetDoesnTExist);
				}

				DateTime lastWriteTime = System.IO.File.GetLastWriteTime(assetPath);

				if (!changed(lastWriteTime))
					return new HttpStatusCodeResult(304);

				string ext = Path.GetExtension(assetPath).TrimStart('.');

				string mimeType = MIMEAssistant.GetMIMEType(ext);

				bool isAttachment = path.StartsWith("documents/") || mimeType == MIMEAssistant.DEFAULT_MIME_TYPE;

				//if (isAttachment)
				//	Response.AddHeader("Content-Disposition", "attachment");

				string thisUrl = Request.Url.AbsoluteUri;

				if (!isPreviewRequest)
				{
					//if (Authman.IsForbiddenAsset(path) || Authman.IsProtectedAsset(path) || isAttachment)
					//{
					//	setNoCache();
					//}
					//else
					//{
					//	Response.Cache.SetCacheability(HttpCacheability.ServerAndPrivate);
					//	//Response.Cache.SetExpires(DateTime.MaxValue);
					//	Response.CacheControl = "private";
					//	Response.Cache.SetLastModified(System.IO.File.GetLastWriteTime(assetPath));
					//}

					//when runtime request, check protected and forbidden folder
					AssetRequestState stateCheck = Authman.CheckAssetAccessiblility(path, Request);
					if (stateCheck == AssetRequestState.Forbidden)
					{
						string forbiddenResponsePagePath = AuthenticationManager.Settings.ForbiddenFoldersResponsePage;

						//path is blocked, go to forbidden response page
						if (!string.IsNullOrWhiteSpace(forbiddenResponsePagePath))
						{
							if (!forbiddenResponsePagePath.EndsWith(".xml") || !forbiddenResponsePagePath.SubstringBefore(".", false, true).IsXId())
							{

								string fullRedirPath = forbiddenResponsePagePath.ToAbsoluteUrl();
								fullRedirPath += fullRedirPath.Contains("?") ? "&" : "?";
								fullRedirPath += "blockedPath=" + HttpUtility.UrlEncode(HttpUtility.UrlEncode(thisUrl));

								return Redirect(fullRedirPath);
							}
							else
							{
								//if the setting is standar xid.xml, then use more friendly path rewrite
								return rewriteToCmsPath(
									forbiddenResponsePagePath.SubstringBefore(".", false, true),
									new Dictionary<string, string> {
										{"blockedPath", thisUrl}});
							}
						}
						else
							throw new HttpException((int)stateCheck, Ingeniux.Runtime.Properties.Resources.AccessToAssetIsForbidden);
					}

					if (stateCheck == AssetRequestState.Unauthorized)
					{
						string loginPagePath = Authman.LoginPath;
						if (!string.IsNullOrWhiteSpace(loginPagePath))
						{
							string loginPathUrl = loginPagePath.ToAbsoluteUrl();
							loginPathUrl += loginPathUrl.Contains("?") ? "&" : "?";
							loginPathUrl += AuthenticationManager.Settings.RedirectionQueryStringName + "=" + Uri.EscapeDataString(Uri.EscapeDataString(thisUrl));
							return RedirectPermanent(loginPathUrl);
						}
						else
							throw new HttpException((int)stateCheck, Ingeniux.Runtime.Properties.Resources.AccessToAssetIsNotAuthorized);
					}

					//use download manager if is protected asset, this way we can use the download page
					if (!string.IsNullOrWhiteSpace(AuthenticationManager.Settings.BinaryDownloadPage) && Authman.IsProtectedAsset(path))
					{
						DownloadManager downloadsMan = Authman.DownloadsManager;
						string downloadPageId;
						Dictionary<string, string> queryStrings = new Dictionary<string, string>();
						bool presentDownloadPage = downloadsMan.ProcessProtectedDownload(Request.RequestContext.HttpContext,
							out queryStrings, out downloadPageId);

						if (presentDownloadPage)
						{
							//Response.AddHeader("Content-Disposition", "attachment");

							//return File(assetPath, mimeType);
							return rewriteToCmsPath(downloadPageId, queryStrings);
						}

						//return rewriteToCmsPath(downloadPageId, queryStrings);
					}
				}

				setCacheResponses(ext, mimeType, path);

				var forceDownloadDocuments = ConfigurationManager.AppSettings["ForceDownloadDocuments"] != null ?
					ConfigurationManager.AppSettings["ForceDownloadDocuments"].ToBoolean() : true;

				var bypassDownloadDocTypes = ConfigurationManager.AppSettings["DocumentExtBypassDownload"] != null ?
					ConfigurationManager.AppSettings["DocumentExtBypassDownload"].Split(';') : new string[] { "" };

				if (isAttachment && (forceDownloadDocuments && !bypassDownloadDocTypes.Contains(ext)))
				{
					Response.AddHeader("Content-Disposition", "attachment");
				}

				FileInfo assetInfo = new FileInfo(assetPath);

				if (assetInfo.Length == 0)
					return File(assetPath, mimeType);

				try
				{
					return new RangeFilePathResult(mimeType, assetInfo.FullName, assetInfo.LastWriteTimeUtc, assetInfo.Length);
					//return File(assetPath, mimeType);
				}
				catch (Exception e)
				{
					return File(assetPath, mimeType);
				}
			}
		}

		private ActionResult rewriteToCmsPath(string pageId,
			Dictionary<string, string> queryStrings)
		{
			//present download page that hosts this download
			CMSPageDefaultController pageController = initPageController();

			//get page by id
			ICMSRequest interceptPage = pageController._PageFactory.GetPage(Request, pageId);
			//add 3 query strings

			foreach (var pair in queryStrings)
				(interceptPage as ICMSEnvironment).QueryString.Add(pair.Key, pair.Value);

			//redo page content to reserialize the querystrings
			(interceptPage as CMSPageRequest).GetPageContent();

			setNoCache();

			return pageController.viewOrXsltFallback(interceptPage as CMSPageRequest);
		}

		private void setCacheResponses(string ext, string mimeType, string path)
		{
			int imageExpires;
			int scriptExpires;
			int stylesExpires;

			bool enableFallback = false;
			if (!bool.TryParse(ConfigurationManager.AppSettings["EnableFallbackCache"], out enableFallback))
				enableFallback = false;

			if (!int.TryParse(ConfigurationManager.AppSettings["CacheImageMinutes"], out imageExpires))
				imageExpires = 20;

			if (!int.TryParse(ConfigurationManager.AppSettings["CacheScriptsMinutes"], out scriptExpires))
				scriptExpires = 20;

			if (!int.TryParse(ConfigurationManager.AppSettings["CacheStylesMinutes"], out stylesExpires))
				stylesExpires = 20;

			Response.Cache.SetSlidingExpiration(true);

			if (mimeType.Contains("image"))
			{
				Response.Cache.SetMaxAge(TimeSpan.FromMinutes(imageExpires));
			}
			else if (ext == "js")
			{
				Response.Cache.SetMaxAge(TimeSpan.FromMinutes(scriptExpires));
			}
			else if (ext == "css")
			{
				Response.Cache.SetMaxAge(TimeSpan.FromMinutes(stylesExpires));
			}
			else if (path.ToLowerInvariant().StartsWith("documents/"))
				setNoCache();
			else if (enableFallback)
			{
				Response.Cache.SetCacheability(HttpCacheability.Private);
				Response.Cache.SetNoServerCaching();
			}
			else
			{
				setNoCache();
			}
		}

		private void setNoCache()
		{
			//make sure no caching of this page
			//Response.Cache.SetCacheability(HttpCacheability.NoCache);
			//Response.Cache.SetNoStore();
			//Response.Expires = 0;
			Response.Cache.SetExpires(DateTime.UtcNow.AddDays(-1));
			Response.Cache.SetValidUntilExpires(false);
			Response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);
			Response.Cache.SetCacheability(HttpCacheability.NoCache);
			Response.Cache.SetNoStore();
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
					CMSPageDefaultController pageController = initPageController();
					pageController.exception(filterContext);
				}
			}
			base.OnException(filterContext);
		}

		protected virtual CMSPageDefaultController initPageController()
		{
			CMSPageDefaultController pageController = new CMSPageDefaultController();
			pageController.init(this.Request.RequestContext);
			//change route data, otherwise mvc 404 page will not have view applied, since it will not be able to load view
			pageController.ControllerContext.RouteData.Values["controller"] = ConfigurationManager.AppSettings["BaseControllerName"]
					.ToNullOrEmptyHelper()
					.Return("CMSPageDefault");
			pageController.ControllerContext.RouteData.Values["action"] = "Index";
			return pageController;
		}
	}
}
