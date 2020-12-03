using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Ingeniux.Runtime.RuntimeAuth;
using System.Configuration;
using Ingeniux.Runtime.Models;
using System.Xml.Linq;

namespace Ingeniux.Runtime.Controllers
{
    public class AuthenticationController : Controller
    {
		private const string STR_Info = "Info";
		private const string STR_Json = "json";
		private const string STR_Error = "Error";
		private AuthenticationManager _Authman;

		protected override void Initialize(System.Web.Routing.RequestContext requestContext)
		{
			base.Initialize(requestContext);
			string sitePath = CmsRoute.GetSitePath();
			_Authman = AuthenticationManager.Get(sitePath);
		}

		[AcceptVerbs(HttpVerbs.Post | HttpVerbs.Get)]
        public ActionResult Login()
        {
			string redir = AuthenticationManager.Settings.RedirectionQueryStringName;
			string userName = Request.Form.Get(AuthenticationManager.Settings.AuthUserFieldName);
			string password = Request.Form.Get(AuthenticationManager.Settings.AuthPasswordFieldName);
			
			string redirectUrl = Request[redir];

			try
			{
				redirectUrl = Uri.UnescapeDataString(redirectUrl);
			}
			catch { }

			bool backgroundAuth = Request.QueryString["background"].ToNullHelper()
				.Propagate(
					qs => qs.Trim().ToLowerInvariant() == "true")
				.Return(false);

			CMSPageRedirectRequest redirectContext;

			try
			{
				redirectContext = _Authman.Login(userName, password, redirectUrl, backgroundAuth, Request);
			}
			catch (HttpException e)
			{
				Response.StatusCode = e.WebEventCode;
				return new XmlResult(new XElement("RuntimeAuthentication",
					e.Message));
			}

			performRedirection(redirectContext);
			return null;
        }

		[AcceptVerbs(HttpVerbs.Post | HttpVerbs.Get)]
		public ActionResult Logout()
		{
			string redirectUrl = Request["redir"];
			CMSPageRedirectRequest redirectContext = _Authman.LogOut(Request.RequestContext.HttpContext, redirectUrl);
			performRedirection(redirectContext);
			return null;
		}

		/// <summary>
		/// This action will return XmlResult, which contains session data
		/// </summary>
		/// <returns></returns>
		public ActionResult GetSession()
		{
			string qs = Request.QueryString["type"];
			string sessionID = Request.QueryString["sessionID"];

			if (string.IsNullOrWhiteSpace(sessionID))
			{
				//go to home page is session id is not provided
				Response.RedirectPermanent(Request.ApplicationPath);
				return null;
			}

			// check querystring to find out if it is xml or json
			if (!IsClientAllowed())
			{
				string msg = Ingeniux.Runtime.Properties.Resources.YouAreNotFromTheAuthorizedPoolOfIPAddresse;

				if (qs == STR_Json)
				{
					return Json(new
					{
						Error = msg
					});
				}
				else
				{
					return new XmlResult(new XElement(STR_Error, new XCData(msg)));
				}
			}

			// lookup the UserData object in the application cache
			UserData userData = Request.RequestContext.HttpContext.Cache["IGXU_" + sessionID] as UserData;

			string invalidSessionMessage = string.Format(Ingeniux.Runtime.Properties.Resources.NoSessionExistsForTokenX0, sessionID);

			if (qs == STR_Json)
			{
				return userData != null ? Json(userData) : Json( new {
						Info = invalidSessionMessage
					});
			}
			else
			{
				return userData != null ? new XmlResult(userData.ToXml()) : new XmlResult(
					new XElement(STR_Info, invalidSessionMessage));						
			}
		}

		/// <summary>
		/// Determines if the originating IP address is allow to perform the request
		/// </summary>
		/// <param name="request">An instance of the HttpRequest</param>
		/// <returns>A value indicating the result</returns>
		private bool IsClientAllowed()
		{
			string clientIp = getClientIP().Trim();
			HashSet<string> allowedHosts = AuthenticationManager.Settings.AllowedRequestIPs;
			return (allowedHosts.Count() == 0 || allowedHosts.Contains(clientIp));
		}

		private string getClientIP()
		{
			// in case ISP IP shielding is involved.
			string ip = Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
			if (ip == string.Empty || ip == null)
			{
				ip = Request.ServerVariables["REMOTE_ADDR"];
			}

			return ip;
		}

		private void performRedirection(CMSPageRedirectRequest redirectContext)
		{
			if (redirectContext != null)
			{
				string absoluteUrl = redirectContext.FinalUrl;
				if (!absoluteUrl.StartsWith("http://") && !absoluteUrl.StartsWith("https://"))
					absoluteUrl = VirtualPathUtility.ToAbsolute("~/" + absoluteUrl.TrimStart('/'));
				Response.Redirect(absoluteUrl);
			}
		}
    }
}
