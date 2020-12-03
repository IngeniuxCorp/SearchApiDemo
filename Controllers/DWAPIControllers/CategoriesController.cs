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
    [RoutePrefix("api/categories")]
	public class CategoriesController : DWController
    {
        [HttpGet]
		[Route("{categoryId}")]
        [IGXRuntimeCache(Duration = 5)]
        public System.Web.Mvc.ContentResult GetCategories(string categoryId)
		{
			Category result = ContentSearchConvert.GetCategoryResults(categoryId);
			string json = JsonConvert.SerializeObject(result, Formatting.Indented, new JsonSerializerSettings() {
				NullValueHandling = NullValueHandling.Ignore
			});
			return Content(json, "application/json", System.Text.Encoding.UTF8);
		}
	}
}