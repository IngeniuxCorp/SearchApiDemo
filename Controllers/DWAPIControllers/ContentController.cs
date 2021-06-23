using Ingeniux.Runtime.Models.APIModels.Helpers;
using System.Web.Http;
using System.Web.Routing;
using static Ingeniux.Runtime.Models.APIModels.Helpers.ContentSearchConvert;

namespace Ingeniux.Runtime.Controllers
{
    [RoutePrefix("api/content")]
	public class ContentController: SearchApiController
    {
        [HttpGet]
		[Route("")]
        public SearchContentResult GetContentByIds([FromUri]string[] pageIds)
		{
			//had to put this in here to replicate the input of DWs
			//var ids = pageIds.Split(',');
			SearchContentResult results = ContentSearchHelper.GetPagesById(pageIds);
			return results;
		}
	}
}