using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using System.Web.Http.Routing;

namespace Ingeniux.Runtime.Models.APIModels
{
	public class IGXWebApiCacheAttribute: ActionFilterAttribute
	{
		public IGXWebApiCacheAttribute():base()
		{
			var sitePath = CMSPageFactory.GetSitePath();
			Settings settings = Settings.Get(new FileInfo(Path.Combine(sitePath, "settings/settings.xml")));
			TriggerFile = Path.Combine(sitePath, settings.GetSetting<string>("RuntimeCache", "TriggerFile"));
		}

		public string TriggerFile { get; private set; }
		public int Duration { get; set; }
		public bool UseCache { get; set; }


		public override void OnActionExecuting(HttpActionContext actionContext)
		{
			var sitePath = CMSPageFactory.GetSitePath();

			Settings settings = Settings.Get(new FileInfo(Path.Combine(sitePath, "settings/settings.xml")));

			var path = actionContext.Request.RequestUri.LocalPath.ToLowerInvariant();

			ObjectCache cache = MemoryCache.Default;

			var cachedContentObject = cache.Get($"{MEM_CACHE_PREFIX}{path}");
			if (cachedContentObject != null)
			{
				if (cachedContentObject is string cachedContent)
				{
					var response = actionContext.Request.CreateResponse(HttpStatusCode.OK);
					response.Content = new StringContent(cachedContent);
					actionContext.Response = response;
					return;
				}
			}


			IGXPageLevelCache pageLevelCache = IGXPageLevelCache.Get(TriggerFile);
			PageCache pageCache = pageLevelCache.GetPageCacheSettings(path);

			UseCache = settings.GetSetting<bool>("RuntimeCache", "UseRuntimeCache") && pageCache != null && pageCache.Cache;

			if (!UseCache)
			{
				Duration = 0;
			}
			else
			{
				int durationSettings = pageCache.CacheTime > 0 ? pageCache.CacheTime : settings.GetSetting<int>("RuntimeCache", "ExpireTime");
				Duration = durationSettings;
			}
			

			base.OnActionExecuting(actionContext);
		}

		public static readonly string NAVS_CACHED_PROP_NAME = "UsingCachedNavs";
		public static readonly string MEM_CACHE_PREFIX = "__IGXWEBAPICACHE__";

		public override void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
		{
			bool usingCachedNavs = true;
			if(actionExecutedContext.Request.Properties.TryGetValue(NAVS_CACHED_PROP_NAME, out object temp))
			{
				usingCachedNavs = (bool)temp;
			}
			var localPath = actionExecutedContext.Request.RequestUri.LocalPath.ToLowerInvariant();
			IGXPageLevelCache pageLevelCache = IGXPageLevelCache.Get(TriggerFile);

			//add page level caching information, first request to the same page will always executing. 2nd request will set the cachability
			pageLevelCache.CheckPageCache(localPath, null, usingCachedNavs);

			if (Duration > 0)
			{
				ObjectCache cache = MemoryCache.Default;
				CacheItemPolicy policy = new CacheItemPolicy();

				List<string> filePaths = new List<string>();
				filePaths.Add(TriggerFile);
				policy.AbsoluteExpiration = DateTimeOffset.Now.AddSeconds(Duration);
				policy.ChangeMonitors.Add(new HostFileChangeMonitor(filePaths));

				var path = actionExecutedContext.Request.RequestUri.LocalPath.ToLowerInvariant();
				var contentValue = actionExecutedContext.Response.Content.ReadAsStringAsync().Result;
				cache.Set($"{MEM_CACHE_PREFIX}{path}", contentValue, policy);
			}

			base.OnActionExecuted(actionExecutedContext);
		}
	}
}