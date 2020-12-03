using Ingeniux.Runtime.Models.APIModels.Helpers;
using Ingeniux.Search;
using Lucene.Net.Search;

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Ingeniux.Runtime.Models;
using static Ingeniux.Runtime.Models.APIModels.Helpers.ContentSearchConvert;

namespace Ingeniux.Runtime.Controllers
{
	public class ContentController:DWController
    {
        [HttpGet]
		[Route("api/content/{pageIds}")]
		[OutputCache(NoStore =true,Duration =0)]
        [IGXRuntimeCache(Duration = 5)]
        public System.Web.Mvc.ContentResult GetContentByIds(string pageIds)
		{
			//had to put this in here to replicate the input of DWs
			var ids = pageIds.Split(',');
			SearchContentResult results = ContentSearchHelper.GetPagesById(Request, ids);

			string json = JsonConvert.SerializeObject(results, Formatting.Indented);

			return Content(json, "application/json", System.Text.Encoding.UTF8);
		}
	}
}