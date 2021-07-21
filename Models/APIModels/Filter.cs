using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ingeniux.Runtime.Models.APIModels
{
    public class QueryFilter
    {
        public static IEnumerable<IEnumerable<QueryFilter>> Parse(IEnumerable<string> filterValues)
        {
            var filterStrings = filterValues?.Select(f => f.Split(new[] { "," },StringSplitOptions.RemoveEmptyEntries)) ?? new string[0][];
            return filterStrings.Select(f => f.Select(q => new QueryFilter(q)));
        }

        public QueryFilter()
        {

        }

        public QueryFilter(string queryFilterString)
        {
            var filterValues = queryFilterString.Split('=');
            Name = filterValues[0];
            Value = filterValues[1];
        }

        public string Name { get; set; }
        public string Value { get; set; }
    }
}