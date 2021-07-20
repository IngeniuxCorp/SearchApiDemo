using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ingeniux.Runtime.Models.APIModels
{
	public class PageModel
	{
		public Dictionary<string, string> Attributes { get; set; }
		public IEnumerable<ElementModel> Elements { get; set; }
	}
}