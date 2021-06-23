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
	[RoutePrefix("api/search")]
	public class SearchController: SearchApiController
    {
		public const string JsonContentType = "application/json";

		[HttpGet]
		[Route("")]
		public ContentSearchResult ContentSearch(string query, string sort ="", int start = 0, int count = 10)
		{
			ContentSearchResult results = ContentSearchHelper.GetSearchResults(new KeyValuePair<string,string>[0], sort, start, count, query).Result;
			return results;
		}
	}
}