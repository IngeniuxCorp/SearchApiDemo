using System.Web.Routing;
using static Ingeniux.Runtime.Models.APIModels.Helpers.ContentSearchConvert;
using System.Web.Http;

namespace Ingeniux.Runtime.Controllers
{
    [RoutePrefix("api/categories")]
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