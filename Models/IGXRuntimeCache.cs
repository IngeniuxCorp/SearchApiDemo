using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Xml.Linq;
using Ingeniux.Runtime.Controllers;
using System.Web.Caching;
using System.IO;

namespace Ingeniux.Runtime.Models
{
	public class PageCache
	{
		/// <summary>
		/// To cache this page or not
		/// </summary>
		public bool Cache { get; set; }

		/// <summary>
		/// By seconds
		/// </summary>
		public int CacheTime { get; set; }
	}

	public class IGXPageLevelCache
	{
		private const string M_PageLevelCache = "pageLevelCache";
		Dictionary<string, PageCache> pageCacheSettings = new Dictionary<string, PageCache>();

		static readonly object loc = new object();

		public PageCache CheckPageCache(string path, XElement pageEle, bool usingNavCache = false)
		{
			int cacheSeconds = 0;
			bool toCache = !usingNavCache && PageOutputCache.CheckPageCache(pageEle, out cacheSeconds);

			PageCache pageCache = new PageCache()
			{
				Cache = toCache,
				CacheTime = cacheSeconds
			};

			lock (loc)
			{
				pageCacheSettings[path] = pageCache;
			}

			return pageCache;
		}

		public PageCache GetPageCacheSettings(string path)
		{
			PageCache pageCache = null;
			lock (loc)
			{
				pageCache = pageCacheSettings.ContainsKey(path) ? pageCacheSettings[path] : null;
			}
			return pageCache;
		}

		public static IGXPageLevelCache Get(string triggerFile)
		{
			var pageLevelCache = HttpRuntime.Cache[M_PageLevelCache] as IGXPageLevelCache;
			if (pageLevelCache == null)
			{
				pageLevelCache = new IGXPageLevelCache();
				HttpRuntime.Cache.Insert(M_PageLevelCache, pageLevelCache,
					new CacheDependency(triggerFile),
						System.Web.Caching.Cache.NoAbsoluteExpiration,
						System.Web.Caching.Cache.NoSlidingExpiration); ;
			}

			return pageLevelCache;
		}
	}


	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
	public class IGXRuntimeCacheAttribute : OutputCacheAttribute
	{
		public bool UseCache { get; private set; }

		public string TriggerFile { get; private set; }

		/// <summary>
		/// The cache attribute that use Ingeniux Runtime settings file to override the settings of
		/// Duration and Location. Use input value of these parameters are not effective
		/// </summary>
		public IGXRuntimeCacheAttribute()
			: base()
		{
		}

		/// <summary>
		/// Load settings from settings file and override several parameters
		/// </summary>
		/// <param name="filterContext"></param>
		public override void OnActionExecuting(ActionExecutingContext filterContext)
		{
			//if design time, not caching
			string sitePath = CmsRoute.GetSitePath();
			var isDesignTime = Reference.Reference.IsDesignTime(sitePath);

			if (isDesignTime)
			{
				Duration = 0;
				return;
			}

			Settings settings = Settings.Get(new FileInfo(Path.Combine(sitePath, "settings/settings.xml")));

			TriggerFile = System.IO.Path.Combine(sitePath, settings.GetSetting<string>("RuntimeCache", "TriggerFile"));
			IGXPageLevelCache pageLevelCache = IGXPageLevelCache.Get(TriggerFile);

			//page level setting trump system settings
			PageCache pageCache = pageLevelCache.GetPageCacheSettings(filterContext.HttpContext.Request.Path.ToLowerInvariant());

			//when pageCache is doesn't exist yet, don't do caching, since the first actual request through controller action
			//will set it. This means the caching kicks after 2nd request
			if (pageCache == null)
			{
				Duration = 0; //set duration to 0 to stop caching
			}
			else
			{
				//override cache settings
				//get runtimecache settings, only use 2 of them
				UseCache = !Reference.Reference.IsDesignTime(sitePath) && settings.GetSetting<bool>("RuntimeCache", "UseRuntimeCache") && pageCache.Cache;
				int durationSettings = pageCache.CacheTime > 0 ? pageCache.CacheTime : settings.GetSetting<int>("RuntimeCache", "ExpireTime");

				//use default duration specified in attribute parameter if no settings is found
				if (durationSettings > 0)
					Duration = durationSettings;
				else if (durationSettings == 0)
				{
					//never expire it by set a large value (24 hours is good enough for now)
					Duration = (int)Math.Round(TimeSpan.FromDays(1).TotalSeconds);
				}
				else
					Duration = 0;

				if (!filterContext.IsChildAction)
				{
					Location = System.Web.UI.OutputCacheLocation.Server;
				}

				if (UseCache && Duration != 0)
				{
					if (System.IO.File.Exists(TriggerFile))
						filterContext.HttpContext.Response.AddFileDependency(TriggerFile);
				}
				else
					Duration = 0; //set duration to 0 to stop caching
			}
			base.OnActionExecuting(filterContext);
		}
	}
}