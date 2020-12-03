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

		private static IEnumerable<SearchResultItem> _GetSkipTakeSearchResults(int skip, int take, Ingeniux.Search.Search search,
			SiteSearch siteSearch, SearchInstruction instructions, out int count, out IEnumerable<KeyValuePair<string, int>> categoryStats)
		{
			int c;
			int page = 1;
			int size = 10;
			int xOffset = 0;
			int yOffset = 0;
			int baseCount = take;
			int baseSkip = skip;

			if (skip < take)
			{
				take = take + skip;
				xOffset = -(skip);
				skip = 0;

			}
			else if (skip % take != 0)
			{
				yOffset = 1;
				//x should be negative
				xOffset = ((take + yOffset) - skip) % (take + yOffset);
				skip = skip + xOffset;
				take = take + yOffset - xOffset;
			}

			decimal pageNum = (((decimal)skip + take) / take);
			page = Convert.ToInt32(Math.Ceiling(pageNum));
			//page = ((skip + take) / take);
			size = take;

			var searchResults = search.QueryFinal(siteSearch, out c, instructions, page: page, size: size);
			
			var categoryStatsProviders = new Ingeniux.Search.StatsProviders.CategoryStatsProviders(siteSearch);
			categoryStats = categoryStatsProviders.GetStats(searchResults).Where(p => p.Value > 0);

			//JsonSerializer serializer = new JsonSerializer();
			//using (StreamWriter sw = new StreamWriter(@"c:\test\catStatsTraeger.json"))
			//using (JsonWriter writer = new JsonTextWriter(sw))
			//{
			//	writer.Formatting = Formatting.Indented;
			//	serializer.Serialize(writer, categoryStats);
			//}

			count = searchResults.TotalCount;

			var skipCount = ((page - 1) * size) - baseSkip;

			IEnumerable<SearchResultItem> offsetResults = searchResults.Skip(-(skipCount)).Take(baseCount);
			return offsetResults;
		}

		private static IEnumerable<KeyValuePair<string, string>> _GetSelectedRefinements(IEnumerable<string> refinementRequests)
		{
			List<KeyValuePair<string, string>> refinements = new List<KeyValuePair<string, string>>();

			foreach (var refinement in refinementRequests)
			{
				var key = refinement.SubstringBefore("=");
				var value = refinement.SubstringAfter("=");

				//clean out lucene control chars
				value = _SearchCleaner.Replace(value, " ");

				if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
				{
					refinements.Add(new KeyValuePair<string, string>(key, value));
				}
			}

			return refinements;
		}

		private readonly static Regex _REFINEMENT_PATTERN = new Regex(@"^Refine_\d$", RegexOptions.IgnoreCase);
		private const char _QUERY_LIST_SEPARATOR = '|';

		//refinements that need to be handled differntly
		private static readonly HashSet<string> _SkippedRefinements = new HashSet<string>()
		{
			"type",
			"fdid",
			"CategoryNodes"
		};

		public static ContentSearchResult GetSearchResults(HttpRequestBase request, string sort = "", int start = 0, int count = 10, string query = "")
		{
			int c;
			IEnumerable<KeyValuePair<string, int>> categoryStats;

			Ingeniux.Search.Search search = new Ingeniux.Search.Search(request);
			var siteSearch = CMSPageDefaultController.GetSiteSearch();
			SearchInstruction instructions = new SearchInstruction(siteSearch.DefaultQueryAnalyzer);
			var apiSourceName = ConfigurationManager.AppSettings["APISourceName"] ?? string.Empty;
			if (string.IsNullOrWhiteSpace(apiSourceName))
			{
				instructions.AddQuery(new TermQuery(new Term("_SOURCENAME_", apiSourceName)), Occur.MUST);
			}
			

			var refinementsKeys = request.QueryString.AllKeys.Where(k => _REFINEMENT_PATTERN.IsMatch(k));

			var refinementRequests = refinementsKeys
				.Select(k => request[k])
				.Where(v => !string.IsNullOrWhiteSpace(v));


			var refinements = _GetSelectedRefinements(refinementRequests);


			List<Task> queryTasks = new List<Task>();

			if (!string.IsNullOrWhiteSpace(query))
			{
				queryTasks.Add(Task.Factory.StartNew(() =>
				{
					string[] termsA = query?.Split(',')
						.Select(t => $"*{t}*").ToArray() ?? new string[0];

					IEnumerable<string> fieldNames = _GetTerms(siteSearch);

					BooleanQuery termQuery = new BooleanQuery();

					foreach (var fieldName in fieldNames)
					{
						termQuery.Add(instructions.GetFieldTermQuery(
							Occur.SHOULD,
							fieldName,
							true,
							termsA
						));
					}

					instructions.AddQuery(termQuery, Occur.MUST);

					//instructions.AddQuery(
					//	instructions.GetFullTextTermQuery(Occur.MUST, false, termsA));
				}));
			}



			if (refinements.Any(r => r.Key.StartsWith("CategoryNodes/", StringComparison.InvariantCultureIgnoreCase)
			|| r.Key.Equals("CategoryNodes", StringComparison.InvariantCultureIgnoreCase)))
			{
				//get everything with a category node
				//they are all OR's
				//if they want ANDS they should use two refinements
				//multi queries are a  pipe separator
				var categoryRefinements = refinements
					.Where(r => r.Key.StartsWith("CategoryNodes/", StringComparison.InvariantCultureIgnoreCase)
					|| r.Key.Equals("CategoryNodes", StringComparison.InvariantCultureIgnoreCase) || r.Key.Equals("cmscategories", StringComparison.InvariantCultureIgnoreCase));

				foreach (var categoryRefinement in categoryRefinements)
				{
					var c_categories = categoryRefinement.Value.Split(_QUERY_LIST_SEPARATOR);

					var catTerms = c_categories.SelectMany(r => ContentSearchConvert.GetChildAndSelfCategoryIds(r)).ToArray();

					var catQuery = new BooleanQuery();

					foreach (var catId in catTerms)
					{
						var catIdClause = instructions.GetFieldTermQuery(Occur.SHOULD, "_CATS_ID", false, catId);
						catQuery.Add(catIdClause);
					}

					instructions.AddQuery(catQuery, Occur.MUST);
				}
			}

			if (refinements.Any(r => r.Key.Equals("fdid", StringComparison.InvariantCultureIgnoreCase)))
			{
				string fdid = refinements.FirstOrDefault(r => r.Key.Equals("fdid", StringComparison.InvariantCultureIgnoreCase)).Value;
				if (!string.IsNullOrWhiteSpace(fdid))
				{
					var sitePath = ConfigurationManager.AppSettings["PageFilesLocation"];
					var cmsRefernce = Reference.Reference.Get(sitePath);

					var childIds = cmsRefernce.GetChildren(fdid).Select(r => r.ID).ToArray();

					var childQuery = new BooleanQuery();

					foreach (var childId in childIds)
					{
						var childIdClause = instructions.GetFieldTermQuery(Occur.SHOULD, "xID", false, childId);
						childQuery.Add(childIdClause);
					}

					instructions.AddQuery(childQuery, Occur.MUST);
				}
			}

			foreach (var refinement in refinements)
			{
				if (_SkippedRefinements.Contains(refinement.Key))
				{
					continue;
				}
				var mappedFields = ContentSearchConvert.FieldsMapedToProperty(refinement.Key);
				if (mappedFields.Any())
				{
					var mappedFieldQuery = new BooleanQuery();
					foreach (var fieldName in mappedFields)
					{
						var fieldValues = refinement.Value.Split(_QUERY_LIST_SEPARATOR);
						var fieldRefinementQuery = new BooleanQuery();

						foreach (var fieldValue in fieldValues)
						{
                            if (refinement.Key.Equals("c_cats", StringComparison.InvariantCultureIgnoreCase)) {
                                var categoryFieldClause = instructions.GetCategoryQuery(Occur.MUST, Occur.SHOULD, fieldValue);
                                fieldRefinementQuery.Add(categoryFieldClause);
                            }
                            else {
                                var fieldClause = instructions.GetFieldTermQuery(Occur.SHOULD, fieldName, false, fieldValue);
                                fieldRefinementQuery.Add(fieldClause);
                            }
						}

						instructions.AddQuery(fieldRefinementQuery, Occur.MUST);
					}
				}
			}

			if (refinements.Any(r => r.Key.Equals("type", StringComparison.InvariantCultureIgnoreCase)))
			{
				var pageTypes = refinements
					.Where(r => r.Key.Equals("type", StringComparison.InvariantCultureIgnoreCase))
					.Select(r => r.Value.Split(_QUERY_LIST_SEPARATOR));

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
					var fieldNames = ContentSearchConvert.FieldsMapedToProperty(sortByFieldName);
					var fieldName = fieldNames.FirstOrDefault();
					if (string.IsNullOrWhiteSpace(fieldName))
					{
						//just pass it through, lets you query by cms fields if you want
						//its a feature
						fieldName = sortByFieldName;
					}
					instructions.AddSort(new Lucene.Net.Search.SortField(fieldName, CultureInfo.InvariantCulture, sortReverse));
				}
			}
			Task.WaitAll(queryTasks.ToArray());
			var searchResults = _GetSkipTakeSearchResults(start, count, search, siteSearch, instructions, out c, out categoryStats);

			ContentSearchResult results = ContentSearchConvert.ConvertContentSearchResults(searchResults, c, request, refinements, categoryStats, start);

			return results;
		}

		public static string[] GetLocationValues(IEnumerable<KeyValuePair<string, string>> refinements)
		{
			string locationValues = refinements.FirstOrDefault(r => r.Key.Equals("location", StringComparison.InvariantCultureIgnoreCase)).Value;
			if (string.IsNullOrWhiteSpace(locationValues))
			{
				return new string[0];
			}
			string[] args = locationValues.Split(',');
			return args;
		}

		private static object _ApiCacheLock = new object();
		private const string API_FILE_CACHE_NAME = "_API_FILE_CACHE_NAME_";

		private static XElement _ApiTermsXML
		{
			get
			{
				ObjectCache cache = MemoryCache.Default;
				XElement apiConfigFile = cache[API_FILE_CACHE_NAME] as XElement;
				if (apiConfigFile == null)
				{
					lock (_ApiCacheLock)
					{
						apiConfigFile = cache[API_FILE_CACHE_NAME] as XElement;
						if (apiConfigFile == null)
						{
							var apiConfigPath = _GetApiPath();
							if (apiConfigFile == null && System.IO.File.Exists(apiConfigPath))
							{
								apiConfigFile = Xtensions.SafeLoad(apiConfigPath);
								CacheItemPolicy policy = new CacheItemPolicy();
								policy.ChangeMonitors.Add(new
								HostFileChangeMonitor(new[] { apiConfigPath }));
								cache.Set(API_FILE_CACHE_NAME, apiConfigFile, policy);
							}
						}
					}
				}
				return apiConfigFile;
			}
		}

		private static string _GetApiPath()
		{
			Uri codeBaseUri = new Uri(Assembly.GetExecutingAssembly().CodeBase);
			FileInfo fi = new FileInfo(codeBaseUri.LocalPath);
			Uri root = new Uri(fi.Directory.FullName);
			var apiFieldFilePath = HttpUtility.UrlDecode(new Uri(root, @"Config\ApiFields.xml").AbsolutePath);
			return apiFieldFilePath;
		}

		private static IEnumerable<string> _GetTerms(SiteSearch siteSearch)
		{
			var publicResults = siteSearch.Indexer.GetPubliclySearchableResults(false, true);

			var fields = publicResults.PubliclySearchableFieldNames;

			var fieldNames = _ApiTermsXML.Descendants("field").Select(e => e.Value);

			var results = fields.Where(f =>
			{
				foreach(var qf in fieldNames)
				{
					if (f.Equals(qf,StringComparison.InvariantCultureIgnoreCase) || f.EndsWith($"__{qf}"))
					{
						return true;
					}
				}
				return false;
			});

			return results;

		}

		public static SearchContentResult GetPagesById(HttpRequestBase request, IEnumerable<string> pageIds)
		{
			int c;
			
			Ingeniux.Search.Search search = new Ingeniux.Search.Search(request);
			var siteSearch = CMSPageDefaultController.GetSiteSearch(); 
			SearchInstruction instructions = new SearchInstruction(siteSearch.DefaultQueryAnalyzer);
			var apiSourceName = ConfigurationManager.AppSettings["APISourceName"] ?? string.Empty;
			if (string.IsNullOrWhiteSpace(apiSourceName))
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
			
			//MAX size of DW was 25, keeping this limit
			var searchResults = search.QueryFinal(siteSearch, out c, instructions, page: 1, size: 25);



			SearchContentResult result = ContentSearchConvert.ConvertContentResults(searchResults, searchResults.Count(), request);

			return result;
		}


		public static BooleanQuery GetLocationQuery(Query query, double latitude, double longitude,  double searchRadiusMi, int maxHits = 10)
		{
			var spatialContext = SpatialContext.GEO;

			var maxLevels = 11;
			SpatialPrefixTree grid = new GeohashPrefixTree(spatialContext, maxLevels);
			var _strategy = new RecursivePrefixTreeStrategy(grid, "location");
			var distance = DistanceUtils.Dist2Degrees(searchRadiusMi, DistanceUtils.EARTH_MEAN_RADIUS_MI);
            //lay is Y value in ESRI
            var searchArea = spatialContext.MakeCircle(longitude, latitude, distance);

			var spatialArgs = new SpatialArgs(SpatialOperation.Intersects, searchArea);
			var spatialQuery = _strategy.MakeQuery(spatialArgs);
			var valueSource = _strategy.MakeRecipDistanceValueSource(searchArea);
			var valueSourceFilter = new ValueSourceFilter(new QueryWrapperFilter(spatialQuery), valueSource, 0, 1);

			var filteredSpatial = new FilteredQuery(query, valueSourceFilter);
			var spatialRankingQuery = new FunctionQuery(valueSource);

			var locationQuery = new BooleanQuery();
			locationQuery.Add(filteredSpatial, Occur.MUST);
			locationQuery.Add(spatialRankingQuery, Occur.MUST);

			return locationQuery;
		}


	}


}