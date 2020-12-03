using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

//this is a testing sample of how to create an schema name based cms page controller to handle remaining path

namespace Ingeniux.Runtime.Controllers
{
	public class IGXSampleCustomController : CMSPageDefaultController
    {
        //
        // GET: /Detail/

        public ActionResult Test()
        {
            var result = handleRequest(
                handleNoneCmsPageRoute,
                handleCmsPageRequestWithRemainingPath,
                handleStandardCMSPageRequest);

            if (result is ViewResult)
            {
                var viewResult = result as ViewResult;
                viewResult.ViewData["Test"] = "Test Subclass Controller";
            }

            return result;
        }


        protected override ActionResult handleCmsPageRequestWithRemainingPath(CMSPageRequest pageRequest, string remainingPath)
        {

            ViewData["RemaingPath"] = remainingPath;
            return View(pageRequest);
        }
    }
}
