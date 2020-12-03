using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Ingeniux.Runtime
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
			routes.MapMvcAttributeRoutes();

			routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
			//routes.IgnoreRoute("{*favicon}", new { favicon = @"(.*/)?favicon.ico(/.*)?" });
			routes.IgnoreRoute("favicon.ico");
			routes.IgnoreRoute("bootstrap3/{*pathInfo}");

			routes.MapRoute(
				"RTA Session Retrieving Handler",
				"Session.ashx",
				new
				{
					controller = "Authentication",
					action = "GetSession"
				});

			routes.MapRoute(
				"RTALoginHandler",
				"Login.ashx",
				new
				{
					controller = "Authentication",
					action = "Login"
				});

			routes.MapRoute(
				"RTA Log out Handler",
				"Logout.ashx",
				new
				{
					controller = "Authentication",
					action = "Logout"
				});

			routes.MapRoute(
				"Dynamic Xml Preview",
				"IGXDynamicXmlPreview",
				new
				{
					controller = "CMSPageDefault",
					action = "DynamicXmlPreview"
				});

			routes.MapRoute(
				"Dynamic Diff Preview",
				"IGXDynamicDiffPreview",
				new
				{
					controller = "CMSPageDefault",
					action = "DynamicDiffPreview"
				});

			routes.MapRoute(
				"Content Unit Preview",
				"IGXContentUnitPreview",
				new
				{
					controller = "CMSPageDefault",
					action = "ContentUnitPreview"
				});

			routes.MapRoute(
				"ICEUpdate",
				"IGXDTICEUpdate",
				new
				{
					controller = "CMSPageDefault",
					action = "IceUpdate"
				});

            // v10 Asset Preview Routes

            routes.MapRoute(
                "PreviewAssets",
                "a/{assetIdNum}",
                new
                {
                    controller = "PreviewAsset",
                    action = "Asset"
                });

			routes.MapRoute(
			   "AssetMetaData",
			   "amd/{assetIdNum}",
			   new
			   {
				   controller = "PreviewAsset",
				   action = "AssetMetaData"
			   });

			routes.MapRoute(
                "PreviewAssetsWithPrefix",
                "{type}/a/{assetIdNum}",
                new
                {
                    controller = "PreviewAsset",
                    action = "Asset"
                });

            routes.MapRoute(
                "PreviewAssetsWithExtendedPrefix",
                "assets/{type}/a/{assetIdNum}",
                new
                {
                    controller = "PreviewAsset",
                    action = "Asset"
                });

            // End v10 Asset Preview Routes

            routes.MapRoute(
				"Stylesheets",
				"stylesheets/{*path}",
				new
				{
					controller = "AssetAsync",
					action = "Get"
				});

			routes.MapRoute(
				"Settings",
				"settings/{*path}",
				new
				{
					controller = "AssetAsync",
					action = "Get"
				});

			routes.MapRoute(
				"Media",
				"media/{*path}",
				new
				{
					controller = "AssetAsync",
					action = "Get"
				});

			routes.MapRoute(
				"Images",
				"images/{*path}",
				new
				{
					controller = "AssetAsync",
					action = "Get"
				});

			routes.MapRoute(
				"Documents",
				"documents/{*path}",
				new
				{
					controller = "AssetAsync",
					action = "Get"
				});

			routes.MapRoute(
				"Prebuilt",
				"prebuilt/{*path}",
				new
				{
					controller = "AssetAsync",
					action = "Get"
				});

            routes.MapRoute(
                "Assets",
                "assets/{*path}",
                new
                {
                    controller = "AssetAsync",
                    action = "Get"
                });           

            routes.Add(new CmsRoute());
        }
    }
}
