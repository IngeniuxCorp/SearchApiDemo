using Ingeniux.Runtime.Models;
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
using static Ingeniux.Runtime.Models.APIModels.Helpers.ContentSearchConvert;

namespace Ingeniux.Runtime.Controllers
{
	[RoutePrefix("api/content_search")]
	public class ContentSearchController:DWController
    {
		public const string JsonContentType = "application/json";

		[HttpGet]
		[Route("")]
		public System.Web.Mvc.ContentResult ContentSearch(string sort ="", int start = 0, int count = 10,  string q = "")
		{
			ContentSearchResult results = ContentSearchHelper.GetSearchResults(Request, sort, start, count, q);

			string json = JsonConvert.SerializeObject(results,Formatting.Indented);

			return Content(json, JsonContentType, System.Text.Encoding.UTF8);
		}
	}
}