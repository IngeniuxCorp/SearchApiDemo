using Ingeniux.CMS;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Lib.Web.Mvc;
using Ingeniux.Runtime.Models;
using System.Xml.Linq;

namespace Ingeniux.Runtime.Controllers
{
	[SessionState(System.Web.SessionState.SessionStateBehavior.ReadOnly)]
	[OutputCache(Duration = 0)]
	public class PreviewAssetController : AssetAsyncController
	{
		//protected string _XmlFolderLocation;
		//protected ContentStore _ContentStore;
		//protected IReadonlyUser _CurrentUser;
		protected bool isPreviewRequest;
		protected string SitePath;


		protected override void Initialize(System.Web.Routing.RequestContext requestContext)
		{
			base.Initialize(requestContext);

			SitePath = CmsRoute.GetSitePath();
			isPreviewRequest = requestContext.HttpContext.Request.QueryString["_previewAsset_"] == "true";
			isPreviewRequest |= Reference.Reference.IsDesignTime(SitePath);

			if (isPreviewRequest)
			{
				HttpApplicationStateBase application = requestContext.HttpContext.Application;
				_GetContentStore(requestContext, application);
			}
		}

		public ActionResult Asset(string assetIdNum, string isCheckedIn)
		{
			string assetId = "a/" + assetIdNum;
			DateTime now = DateTime.Now;
			if (isPreviewRequest)
			{
				using (IUserSession session = _ContentStore.OpenReadSession(_CurrentUser))
				{
					IAsset asset = session.Site.Asset(assetId);

					if (asset.IsExternal)
						Response.Redirect(asset.ExternalUrl, true);

					if (asset == null || asset.StartDate > now)
						throw new HttpException(404, string.Format("Asset {0} not found.", assetId));
					if (asset.EndDate < now)
						throw new HttpException(404, string.Format("Asset {0} has expired.", assetId));

					FileInfo file = asset.File();
					string mimeType = MimeMapping.GetMimeMapping(file.FullName);
					return new RangeFilePathResult(mimeType, file.FullName, file.LastWriteTimeUtc, file.Length);
					//return base.File(file.FullName, mimeType);
				}
			}
			else
			{
				SitePath = CmsRoute.GetSitePath();
				Assets.AssetFactory map = Assets.AssetFactory.Get(SitePath);
				var assetMapEntry = map.GetAssetByID(assetId);

				Assets.AssetTree tree = Assets.AssetTree.Get(CmsRoute.GetSitePath());
				var assetTreeEntry = tree.GetNode(assetId);
				if (assetTreeEntry != null && !assetTreeEntry.Valid())
				{
					if (assetMapEntry != null)
						throw new HttpException(404, string.Format("Asset {0} is invalid.", assetMapEntry.Url));
					else
						throw new HttpException(404, string.Format("Asset {0} is invalid.", assetId));
				}

				if (assetMapEntry != null && assetMapEntry.IsExternal)
					Response.Redirect(assetMapEntry.FilePath, true);

				if (assetMapEntry == null)
					throw new HttpException(404, string.Format("Asset {0} not found.", assetId));
				if (!assetMapEntry.Valid())
					throw new HttpException(404, string.Format("Asset {0} is invalid.", assetMapEntry.FilePath));

				string filePath = Uri.UnescapeDataString(assetMapEntry.FilePath);
				FileInfo file = new FileInfo(filePath);
				string mimeType = MimeMapping.GetMimeMapping(file.FullName);

				if (file.Length == 0)
					return File(file.FullName, mimeType);

				return new RangeFilePathResult(mimeType, file.FullName, file.LastWriteTimeUtc, file.Length);
				//return base.File(file.FullName, mimeType);
			}
		}

		public ActionResult AssetMetaData(string assetIdNum, string isCheckedIn)
		{
			string assetId = assetIdNum.StartsWith("a/") ? assetIdNum : "a/" + assetIdNum;
			DateTime now = DateTime.Now;
			XElement ele;
			if (isPreviewRequest)
			{
				using (IUserSession session = _ContentStore.OpenReadSession(_CurrentUser))
				{
					var map = Assets.DocumentPreviewAssetFactory.Get(session);
					var asset = map.GetAssetByID(assetId);

					if (asset == null)
						throw new HttpException(404, string.Format("Asset {0} not found.", assetId));
					if (!asset.Valid())
						throw new HttpException(404, string.Format("Asset {0} is invalid.", asset.FilePath));

					ele = asset.Metadata.Serialize();
				}
			}
			else
			{
				SitePath = CmsRoute.GetSitePath();
				var map = Assets.AssetFactory.Get(SitePath);
				var asset = map.GetAssetByID(assetId);

				if (asset == null)
					throw new HttpException(404, string.Format("Asset {0} not found.", assetId));
				if (!asset.Valid())
					throw new HttpException(404, string.Format("Asset {0} is invalid.", asset.FilePath));

				ele = asset.Metadata.Serialize();
			}

			return new XmlResult(ele);
		}

		public override void _GetContentStore(System.Web.Routing.RequestContext requestContext, HttpApplicationStateBase application)
		{
			//xml folder is in the app data folder now.
			_XmlFolderLocation = ConfigurationManager.AppSettings["PageFilesLocation"];

			_ContentStore = CMSPageFactoryHelper.GetPreviewContentStore(requestContext, _XmlFolderLocation) as ContentStore;
			_CurrentUser = CMSPageFactoryHelper.GetPreviewCurrentUser(_ContentStore,
				requestContext, requestContext.HttpContext.Session);
		}
	}
}