using System.Web.Routing;
using static Ingeniux.Runtime.Models.APIModels.Helpers.ContentSearchConvert;
using System.Web.Http;
using Ingeniux.Runtime.Models.APIModels.Helpers;

namespace Ingeniux.Runtime.Controllers
{
	[AccessControlAllowOrigin("*")]
	[RoutePrefix("api/category")]
	public class CategoriesController : SearchApiController
    {
        [HttpGet]
		[Route("{categoryId}")]
		public Category GetCategory(string categoryId)
		{
			Category result = GetCategoryResults(categoryId);
			return result;
		}
	}
}