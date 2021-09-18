using Ingeniux.Runtime.Models.APIModels.Helpers;
using System.Web.Http;
using System.Web.Routing;
using static Ingeniux.Runtime.Models.APIModels.Helpers.ContentSearchConvert;

namespace Ingeniux.Runtime.Controllers
{
	[AccessControlAllowOrigin("*")]
	[RoutePrefix("api/content")]
	public class ContentController: SearchApiController
    {
        [HttpGet]
		[Route("")]
        public SearchContentResult GetContentByIds([FromUri]string[] pageIds)
		{
			SearchContentResult results = ContentSearchHelper.GetPagesById(pageIds, Url.Content("~/"));
			return results;
		}
	}
}