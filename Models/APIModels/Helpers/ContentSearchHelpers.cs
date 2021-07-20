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
			"type",
			"xid",
			"categorynodes"
		};

		public static async Task<ContentSearchResult> GetSearchResults(IEnumerable<QueryFilter> refinements = null, string sort = "", int page = 1, int size = 10, string query = "")
		{
			int c;
			IEnumerable<KeyValuePair<string, int>> categoryStats;

			if(refinements == null)
            {
				refinements = new QueryFilter[0];
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


			var categoryRefinements = refinements?.Where(r => r.Name.Equals("categorynodes", StringComparison.InvariantCultureIgnoreCase)) ?? new QueryFilter[0];

			foreach (var categoryRefinement in categoryRefinements)
			{

				var categories = categoryRefinement.Values.Where(v => !string.IsNullOrWhiteSpace(v));
                if (!categories.Any())
                {
					continue;
                }

				var catTerms = categories.SelectMany(r => ContentSearchConvert.GetChildAndSelfCategoryIds(r)).ToArray();
				var catQuery = new BooleanQuery();

				foreach (var catId in catTerms)
				{
                    if (string.IsNullOrWhiteSpace(catId))
                    {

                    }
					var catIdClause = instructions.GetFieldTermQuery(Occur.SHOULD, "_CATS_ID", false, catId);
					catQuery.Add(catIdClause);
				}

				instructions.AddQuery(catQuery, Occur.MUST);
			}
			

			foreach (var refinement in refinements)
			{
				if (_SkippedRefinements.Contains(refinement.Name))
				{
					continue;
				}

				var fieldRefinementQuery = new BooleanQuery();
				foreach (var refinementValue in refinement.Values)
				{
					var refinementQuery = new TermQuery(new Term(refinement.Name, refinementValue));
                    fieldRefinementQuery.Add(refinementQuery, Occur.SHOULD);
				}
				instructions.AddQuery(fieldRefinementQuery, Occur.MUST);
			}

			if (refinements.Any(r => r.Name.Equals("type", StringComparison.InvariantCultureIgnoreCase)))
			{
				var pageTypes = refinements
					.Where(r => r.Name.Equals("type", StringComparison.InvariantCultureIgnoreCase))
					.SelectMany(r => r.Values);

				var pageTypeQuery = new BooleanQuery();

				//multiple of these will return nothing, but its how the logic should  be
				foreach (var type in pageTypes)
				{
					if (type.Any())
					{
						var pageTypeClause = instructions.GetTypeQuery(Occur.SHOULD, type);
						pageTypeQuery.Add(pageTypeClause);
						instructions.AddQuery(pageTypeQuery, Occur.MUST);
					}
				}
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
					var hierarchySort = new Lucene.Net.Search.SortField(HierarchyCompare.HIERARCHY_VALUE_NAME, new HierarchyCompareSource(), sortReverse);
					instructions.sorts.Add(hierarchySort);
				}
				else
				{
					instructions.AddSort(new Lucene.Net.Search.SortField(sortByFieldName, CultureInfo.InvariantCulture, sortReverse));
				}
			}

			var searchResults = search.QueryFinal(siteSearch, out c, instructions, page: page, size: size);

			var categoryStatsProviders = new CategoryStatsProviders(siteSearch);
			categoryStats = categoryStatsProviders.GetStats(searchResults).Where(p => p.Value > 0);


			ContentSearchResult results = ConvertContentSearchResults(searchResults, c, refinements, categoryStats, page);

			return results;
		}


		private static object _ApiCacheLock = new object();
		private const string API_FILE_CACHE_NAME = "_API_FILE_CACHE_NAME_";


		public static SearchContentResult GetPagesById(IEnumerable<string> pageIds)
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
			SearchContentResult result = ContentSearchConvert.ConvertContentResults(searchResults, searchResults.Count());

			return result;
		}
	}
}