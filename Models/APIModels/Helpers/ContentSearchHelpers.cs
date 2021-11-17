using Ingeniux.Runtime.Controllers;
using Ingeniux.Runtime.Models.SearchSource;
using Ingeniux.Search;
using Ingeniux.Search.StatsProviders;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Spatial;
using Lucene.Net.Spatial.Prefix;
using Lucene.Net.Spatial.Prefix.Tree;
using Lucene.Net.Spatial.Queries;
using Lucene.Net.Spatial.Util;
using Newtonsoft.Json;
using Spatial4n.Core.Context;
using Spatial4n.Core.Distance;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using static Ingeniux.Runtime.Models.APIModels.Helpers.ContentSearchConvert;

namespace Ingeniux.Runtime.Models.APIModels.Helpers
{
	public class ContentSearchHelper
	{
		private static readonly Regex _SearchCleaner = new Regex("\\*|\\?|\u25CA|\u2122|\u00AE|\u00A9|\u2014");
		private const string SEARCH_XID_FILED_NAME = "xID";
		private const string SEARCH_OLD_ID_FILED_NAME = "ID";

		private static readonly HashSet<string> _SkippedRefinements = new HashSet<string>()
		{
			"categorynodes"
		};

		public static async Task<ContentSearchResult> GetSearchResults(IEnumerable<IEnumerable<QueryFilter>> filters = null, string sort = "", int page = 1, int size = 10, string query = "", string baseUri="")
		{
			int c;
			IEnumerable<KeyValuePair<string, int>> categoryStats;

			if(filters == null)
            {
				filters = new QueryFilter[0][];
			}

			Ingeniux.Search.Search search = new Ingeniux.Search.Search();
			var siteSearch = CMSPageDefaultController.GetSiteSearch();
			SearchInstruction instructions = new SearchInstruction(siteSearch.DefaultQueryAnalyzer);
			var apiSourceName = ConfigurationManager.AppSettings["APISourceName"] ?? string.Empty;
			if (!string.IsNullOrWhiteSpace(apiSourceName))
			{
				instructions.AddQuery(new TermQuery(new Term("_SOURCENAME_", apiSourceName)), Occur.MUST);
			}

            string[] termsA = query?.Split(new[] { ',' },StringSplitOptions.RemoveEmptyEntries)
				.Select(t => $"*{t}*").ToArray() ?? new string[0];

			BooleanQuery termQuery = new BooleanQuery();

			if (termsA.Any())
			{
				termQuery.Add(instructions.GetFullTextTermQuery(
					Occur.MUST,
					true,
					termsA
				));
				if (termQuery.Any())
				{
					instructions.AddQuery(termQuery, Occur.MUST);
				}
			}

			var filterQuery = new BooleanQuery();

			foreach(var filterGroup in filters)
            {
				var filterGroupQuery = _ProcessFilters(filterGroup, instructions);
                if (filterGroup.Any())
                {
					filterQuery.Add(filterGroupQuery, Occur.SHOULD);
                }
            }
            if (filterQuery.Any())
            {
				instructions.AddQuery(filterQuery, Occur.MUST);
            }
			

			if (!string.IsNullOrWhiteSpace(sort))
			{
				var orderByStr = sort.SubstringAfter("=");
				var sortByFieldName = sort.SubstringBefore("=");
				var sortReverse = orderByStr.ToLowerInvariant() == "dec" ? true : false;

				//custom sorting for HierarchyValues 
				if (sortByFieldName.Equals(HierarchyCompare.HIERARCHY_VALUE_NAME, StringComparison.InvariantCultureIgnoreCase)
					|| sortByFieldName.Equals("cmshierarchy", StringComparison.InvariantCultureIgnoreCase))
				{
					sortReverse = orderByStr.ToLowerInvariant() == "dec" ? true : false;
					var hierarchySort = new SortField(HierarchyCompare.HIERARCHY_VALUE_NAME, new HierarchyCompareSource(), sortReverse);
					instructions.sorts.Add(hierarchySort);
				}
				else
				{
					instructions.AddSort(new SortField(sortByFieldName, CultureInfo.InvariantCulture, sortReverse));
				}
			}

			var searchResults = search.QueryFinal(siteSearch, out c, instructions, page: page, size: size);

			var categoryStatsProviders = new CategoryStatsProviders(siteSearch);
			categoryStats = categoryStatsProviders.GetStats(searchResults).Where(p => p.Value > 0);


			ContentSearchResult results = ConvertContentSearchResults(searchResults, c, filters, categoryStats, baseUri, page);

			return results;
		}

		private static BooleanQuery _ProcessFilters(IEnumerable<QueryFilter> filters, SearchInstruction searchInstruction)
        {
			var filterQuery = new BooleanQuery();
			var categoryRefinements = filters?.Where(r => r.Name.Equals("categorynodes", StringComparison.InvariantCultureIgnoreCase)) ?? new QueryFilter[0];

			foreach (var categoryRefinement in categoryRefinements)
			{

				if (string.IsNullOrWhiteSpace(categoryRefinement.Value))
				{
					continue;
				}

				var catTerms = ContentSearchConvert.GetChildAndSelfCategoryIds(categoryRefinement.Value).ToArray();
				var catQuery = new BooleanQuery();

				foreach (var catId in catTerms)
				{
					if (string.IsNullOrWhiteSpace(catId))
					{
						continue;
					}
					var catIdQuery = new TermQuery(new Term("_CATS_ID", catId));
					catQuery.Add(catIdQuery, Occur.SHOULD);
				}

				filterQuery.Add(catQuery, Occur.MUST);
			}


			foreach (var filter in filters)
			{
				if (_SkippedRefinements.Contains(filter.Name) || string.IsNullOrWhiteSpace(filter.Value))
				{
					continue;
				}

				//var fieldRefinementQuery = new BooleanQuery();

				//var refinementQuery = new TermQuery(new Term(filter.Name, filter.Value));
				//fieldRefinementQuery.Add(refinementQuery, Occur.SHOULD);

				//filterQuery.Add(fieldRefinementQuery, Occur.MUST);

				var fieldRefinementQuery = searchInstruction.GetFieldTermQuery(Occur.MUST, filter.Name, false, filter.Value);
				filterQuery.Add(fieldRefinementQuery);
            }

			return filterQuery;
		}

		public static SearchContentResult GetPagesById(IEnumerable<string> pageIds, string baseUri)
		{
			int c;
			
			Ingeniux.Search.Search search = new Ingeniux.Search.Search();
			var siteSearch = CMSPageDefaultController.GetSiteSearch(); 
			SearchInstruction instructions = new SearchInstruction(siteSearch.DefaultQueryAnalyzer);
			var apiSourceName = ConfigurationManager.AppSettings["APISourceName"] ?? string.Empty;
			if (!string.IsNullOrWhiteSpace(apiSourceName))
			{
				instructions.AddQuery(new TermQuery(new Term("_SOURCENAME_", apiSourceName)), Occur.MUST);
			}

			var pageIdQuery = new BooleanQuery();
			foreach(var pageId in pageIds)
			{
				//clean out lucene control chars
				var cleanId = _SearchCleaner.Replace(pageId, "");
				var idQuery = instructions.GetFieldTermQuery(Occur.SHOULD, SEARCH_XID_FILED_NAME, false, cleanId);
				var oldQuery = instructions.GetFieldTermQuery(Occur.SHOULD, SEARCH_OLD_ID_FILED_NAME, false, cleanId);
				pageIdQuery.Add(idQuery);
				pageIdQuery.Add(oldQuery);
			}

			instructions.AddQuery(pageIdQuery, Occur.MUST);
			
			var searchResults = search.QueryFinal(siteSearch, out c, instructions, page: 1, size: 25);
			SearchContentResult result = ContentSearchConvert.ConvertContentResults(searchResults, searchResults.Count(), baseUri);

			return result;
		}
	}
}