using Ingeniux.Runtime.Models.APIModels;
using Ingeniux.Runtime.Models.APIModels.Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Web.Routing;
using static Ingeniux.Runtime.Models.APIModels.Helpers.ContentSearchConvert;

namespace Ingeniux.Runtime.Controllers
{
	[AccessControlAllowOrigin("*")]
	[RoutePrefix("api/search")]
	public class SearchController: SearchApiController
    {
		public const string JsonContentType = "application/json";

		[HttpGet]
		[Route("")]
		public ContentSearchResult ContentSearch(string query = "", [FromUri] List<string> filters=null, string sort ="", int start = 1, int count = 10)
		{
			ContentSearchResult results = ContentSearchHelper.GetSearchResults(QueryFilter.Parse(filters), sort, start, count, query, Url.Content("~/")).Result;
			return results;
		}
	}
}