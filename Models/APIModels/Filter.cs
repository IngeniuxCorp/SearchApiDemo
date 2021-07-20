using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ingeniux.Runtime.Models.APIModels
{
    public class QueryFilter
    {
        public QueryFilter()
        {

        }

        public QueryFilter(string queryFilterString)
        {
            var filterValues = queryFilterString.Split('=');
            Name = filterValues[0];
            Values = filterValues[1].Split(',');
        }

        public string Name { get; set; }
        public IEnumerable<string> Values { get; set; }
    }
}