using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http.Filters;

namespace Ingeniux.Runtime.Models.APIModels.Helpers
{
	public class AccessControlAllowOriginAttribute : ActionFilterAttribute
	{
		public string OriginFilter;
		public AccessControlAllowOriginAttribute(string originFilter) : base()
		{
			OriginFilter = originFilter;
		}

		public override void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
		{
			base.OnActionExecuted(actionExecutedContext);
			actionExecutedContext.ActionContext.Response.Headers.Add("Access-Control-Allow-Origin", OriginFilter);
		}
	}
}