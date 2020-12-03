using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Ingeniux.Runtime.Controllers
{
    [SessionState(System.Web.SessionState.SessionStateBehavior.ReadOnly)]
    [Obsolete("This class is obsolete. Call AssetAsyncController instead.", false)]
    public class AssetController : AssetAsyncController
    {
	}
}