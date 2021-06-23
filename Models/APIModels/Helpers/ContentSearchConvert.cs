using Ingeniux.Search;
using MoreLinq.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spatial4n.Core.Context;
using Spatial4n.Core.Distance;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Xml.Linq;

namespace Ingeniux.Runtime.Models.APIModels.Helpers
{
    using System.ComponentModel.DataAnnotations;
	public class ContentSearchConvert
	{
		private const string TAXONOMY_FILE_CACHE_NAME = "TAX_FILE";
		private const string TAXONOMY_FILE_NAME = "TaxonomyTree.xml";

		private const string TAXONOMY_ASSOC_CACHE_NAME = "TAX_ASSOC_FILE";
		private const string TAXONOMY_ASSOC_NAME = "TaxonomyAssociations.xml";
		private const string CATEGORY_ID_PREFIX = "CategoryNodes/";
		private const string ROOT_CATEGORY_NODE_VALUE = "CategoryNodes/root";
		private const string DEFAULT_CATEGORY_NAME = "Categories";
		private static object _TaxCacheLock = new object();

		public const string CUSTOM_PREFIX = "c_";

		public static Dictionary<string, XElement> CategoriesById
		{
			get
			{
				ObjectCache cache = MemoryCache.Default;
				Dictionary<string, XElement> elementsById = cache[TAXONOMY_FILE_CACHE_NAME] as Dictionary<string, XElement>;
				if (elementsById == null)
				{
					lock (_TaxCacheLock)
					{
						elementsById = cache[TAXONOMY_FILE_CACHE_NAME] as Dictionary<string, XElement>;
						if (elementsById == null)
						{
							var pageFilePath = ConfigurationManager.AppSettings["PageFilesLocation"];

							if (string.IsNullOrWhiteSpace(pageFilePath))
							{
								//wtf
							}

							var taxPath = Path.Combine(pageFilePath, TAXONOMY_FILE_NAME);
							if (elementsById == null && File.Exists(taxPath))
							{
								var xmlFile = XElement.Load(taxPath);

								//strip off CategoryNodes/ becacuse search doesnt use it
								elementsById = xmlFile.Descendants("C").DistinctBy(e => e.GetAttributeValue("ID", ""))
									.ToDictionary(
										e => e.GetAttributeValue("ID", string.Empty).Replace(CATEGORY_ID_PREFIX, string.Empty),
										e => e
									);
								CacheItemPolicy policy = new CacheItemPolicy();
								policy.ChangeMonitors.Add(new
								HostFileChangeMonitor(new[] { taxPath }));
								cache.Set(TAXONOMY_FILE_CACHE_NAME, elementsById, policy);
							}
						}
					}
				}
				return elementsById;
			}
		}

		public static Dictionary<string, int> CategoryCountsById {
			get
			{
				ObjectCache cache = MemoryCache.Default;
				Dictionary<string, int> countsById = cache[TAXONOMY_ASSOC_CACHE_NAME] as Dictionary<string, int>;
				if (countsById == null)
				{
					lock (_TaxCacheLock)
					{
						countsById = cache[TAXONOMY_ASSOC_CACHE_NAME] as Dictionary<string, int>;
						if (countsById == null)
						{
							var pageFilePath = ConfigurationManager.AppSettings["PageFilesLocation"];

							if (string.IsNullOrWhiteSpace(pageFilePath))
							{
								//wtf
							}

							var taxPath = Path.Combine(pageFilePath, TAXONOMY_ASSOC_NAME);
							if (countsById == null && File.Exists(taxPath))
							{
								var xmlFile = XElement.Load(taxPath);

								//strip off CategoryNodes/ becacuse search doesnt use it
								countsById = xmlFile.Descendants("A")
									.GroupBy(e => e.GetAttributeValue("C",string.Empty))
									.ToDictionary(
										e => e.Key.Replace(CATEGORY_ID_PREFIX, string.Empty),
										e => e.Count()
									);
								CacheItemPolicy policy = new CacheItemPolicy();
								policy.ChangeMonitors.Add(new
								HostFileChangeMonitor(new[] { taxPath }));
								cache.Set(TAXONOMY_ASSOC_NAME, countsById, policy);
							}
						}
					}
				}
				return countsById;
			}
		}

		public static string GetSearchItemField(SearchResultItem searchItem, string fieldName, string valueDefault = "")
		{
			string value;

			if(!searchItem.AdditionalFields.TryGetValue(fieldName,out value))
			{
				value = valueDefault;
			}

			return value;
		}

		public static ContentSearchResult ConvertContentSearchResults(IEnumerable<SearchResultItem> searchResults, int totalCount,
			IEnumerable<KeyValuePair<string, string>> refinements, IEnumerable<KeyValuePair<string, int>> categoryStats, int startIndex = 0)
		{
			ContentSearchResult results = new ContentSearchResult();
			results.Data = _ConvertSearchResults(searchResults, refinements);
			//results.Query = request.Url.Query;
			results.Total = totalCount;
			results.Count = searchResults.Count();
			results.StartIndex = startIndex;
			results.SelectedRefinements = refinements.DistinctBy(p => p.Key).ToDictionary();
			results.Refinements = ConvertContentSearchRefinements(categoryStats);

			return results;
		}

		public static SearchContentResult ConvertContentResults(IEnumerable<SearchResultItem> searchResults, int totalCount)
		{
			SearchContentResult results = new SearchContentResult();
			results.Data = _ConvertSearchResults(searchResults);
			results.Total = totalCount;
			results.Count = searchResults.Count();

			return results;
		}

		private static IEnumerable<Content> _ConvertSearchResults(IEnumerable<SearchResultItem> searchResults, IEnumerable<KeyValuePair<string, string>> refinements = null)
		{
			return searchResults.Select(r =>
			{
				switch (r.Type)
				{
					case "article":
						return ConvertArticleSearchResultToContent(r);
                    default:
						return new Content()
						{
							Id = r.UniqueID,
							Name = r.Name,
							Type = r.Type
						};
				}
			});
		}

		public static Dictionary<string, string> PROPERTY_MAPS = new Dictionary<string, string>()
		{
			{"id","xID"},
			{"title","Title"},
			{"content","EscapedContent"},
			{"contentType","_TYPE_O_"},
			{"cmshierarchy","HierarchyValue"}
        };




        public static IEnumerable<string> FieldsMapedToProperty(string propertyName)
		{
			HashSet<string> fieldNames = new HashSet<string>();
			string value;
			if(PROPERTY_MAPS.TryGetValue(propertyName,out value))
			{
				fieldNames.Add(value);
			}
		
		

			return fieldNames;
		}

		public static void SetPropValue(Content src, string propName, object value)
		{
			src.GetType().GetProperty(propName).SetValue(src, value);
		}

		public static Content ConvertMappedResultToContent(SearchResultItem searchResult, Dictionary<string,string> map)
		{
			var contentObject = new JObject();

			foreach (var fieldMap in map)
			{
				var searchResultValue = GetSearchItemField(searchResult, fieldMap.Value);
				contentObject.Add(fieldMap.Key, new JValue(searchResultValue));
			}

			Content content = contentObject.ToObject<Content>();

			return content;
		}

        public static Content ConvertArticleSearchResultToContent(SearchResultItem articleResult)
		{
			var content = ConvertMappedResultToContent(articleResult, PROPERTY_MAPS);

			content.Type = articleResult.Type;
			content.Name = articleResult.Name;
;
			//var c_pageurl = _GetFullUrl(request, articleResult.URL);
			//content.CustomAttributes.Add("pageurl", c_pageurl);

			Dictionary<string, ContentSearchRefinement> refinementsById = new Dictionary<string, ContentSearchRefinement>();

			var cats = GetSearchItemField(articleResult, "_CATS_PATH_ID");

			var categoryIds = GetSearchItemField(articleResult, "_CATS_ID")
				.Split(' ');


			foreach (var startNode in categoryIds)
			{
				XElement taxElement;
				if (!CategoriesById.TryGetValue(startNode, out taxElement))
				{
					continue;
				}

				var refinement = ContentSearchRefinementFromTaxonomyElement(taxElement);
				ContentSearchRefinement temp;

				if (refinementsById.TryGetValue(refinement.Id, out temp))
				{
					temp.Values = temp.Values.Concat(refinement.Values).DistinctBy(v => v.Value);
				}
				else
				{
					refinementsById.Add(refinement.Id, refinement);
				}
			}

			content.CustomAttributes.Add("cmscategories", refinementsById.Values);


			return content;
		}


        public static IEnumerable<ContentSearchRefinement> ConvertContentSearchRefinements(IEnumerable<KeyValuePair<string, int>> categoryStats)
		{
			Dictionary<string, ContentSearchRefinement> refinementsByCatId = new Dictionary<string, ContentSearchRefinement>();
			
			foreach(var stat in categoryStats)
			{
				XElement taxElement;
				if (!CategoriesById.TryGetValue(stat.Key, out taxElement))
				{
					continue;
				}

				ContentSearchRefinementValue value = new ContentSearchRefinementValue();
				var parentId = taxElement.Parent?.GetAttributeValue("ID", ROOT_CATEGORY_NODE_VALUE) ?? ROOT_CATEGORY_NODE_VALUE;
				var parentName = taxElement.Parent?.GetAttributeValue("N", DEFAULT_CATEGORY_NAME) ?? DEFAULT_CATEGORY_NAME;

				ContentSearchRefinement searchRefinement;

				if(!refinementsByCatId.TryGetValue(parentId, out searchRefinement))
				{
					searchRefinement = new ContentSearchRefinement();
					searchRefinement.Id = parentId;
					searchRefinement.Label = parentName;
					searchRefinement.Values = new List<ContentSearchRefinementValue>();
					refinementsByCatId.Add(parentId, searchRefinement);
				}

				value.Label = taxElement.GetAttributeValue("N", "default");
				value.Value = stat.Key;
				value.Count = stat.Value;

				searchRefinement.Values = searchRefinement.Values.Concat(new[] { value });
			}

			return refinementsByCatId.Values;
		}

		public static ContentSearchRefinement ContentSearchRefinementFromTaxonomyElement(XElement taxonomyElement, bool includeSiblings = false)
		{
			ContentSearchRefinementValue value = new ContentSearchRefinementValue();
			var parentId = taxonomyElement.Parent?.GetAttributeValue("ID", ROOT_CATEGORY_NODE_VALUE) ?? ROOT_CATEGORY_NODE_VALUE;
			var parentName = taxonomyElement.Parent?.GetAttributeValue("N", DEFAULT_CATEGORY_NAME) ?? DEFAULT_CATEGORY_NAME;

			IEnumerable<XElement> childNodes;

			if (includeSiblings)
			{
				childNodes = taxonomyElement.Parent.Elements("C");
			}
			else
			{
				childNodes = new[] { taxonomyElement };
			}

			var searchRefinement = new ContentSearchRefinement();
			searchRefinement.Id = parentId;
			searchRefinement.Label = parentName;
			searchRefinement.Values = new List<ContentSearchRefinementValue>();

			value.Label = taxonomyElement.GetAttributeValue("N", "default");
			value.Value = taxonomyElement.GetAttributeValue("ID", ROOT_CATEGORY_NODE_VALUE).SubstringAfter(CATEGORY_ID_PREFIX);

			int count;
			if (CategoryCountsById.TryGetValue(value.Value,out count))
			{
				value.Count = count;
			}
			else
			{
				value.Count = 0;
			}

			searchRefinement.Values = searchRefinement.Values.Concat(new[] { value });

			return searchRefinement;
		}

		private static IEnumerable<IGrouping<string,KeyValuePair<string,string>>> _GetEmbeddedCompList(SearchResultItem result, string compListFullName, string compName)
		{
			Regex indexPattern = new Regex($"^{compListFullName}_(\\d+)__{compName}__");

			var embeddedCompList = result
				.AdditionalFields
				.Where(v => v.Key.StartsWith($"{compListFullName}_"))
				.GroupBy(v => {
					string i = "-1";
					var match = indexPattern.Match(v.Key);
					if (match.Success)
					{
						return match.Groups[1].Value;
					}
					return i;
				});

			return embeddedCompList;
		}




		private static string _GetEmbeddedFieldValue(IEnumerable<KeyValuePair<string,string>> embeddedItem, string fieldName, string defaultValue = "")
		{
			var fields = embeddedItem.Where(d => d.Key.EndsWith(fieldName));
			if (fields.Any())
			{
				var headingName = fields.First();
				return headingName.Value;
			}
			return defaultValue;
		}

		private static string _GetFullUrl(HttpRequestBase request, string relativeUrl)
		{
			relativeUrl = relativeUrl.TrimStart('/');

			var urlHelper = new UrlHelper(request.RequestContext);

			var appPath = urlHelper.Content("~");

			var baseUrl = string.Format($"{request.Url.Scheme}://{request.Url.Authority}{appPath}");

			return $"{baseUrl}{relativeUrl}";
		}

		public static Category GetCategoryResults(string categoryId)
		{
			List<Category> categories = new List<Category>();

			XElement taxonomyElement;
			Category category = new Category();

			if (CategoriesById.TryGetValue(categoryId, out taxonomyElement))
			{
				categories.Add(category);
				category.Id = taxonomyElement.GetAttributeValue("ID", string.Empty).SubstringAfter(CATEGORY_ID_PREFIX);
				category.Name = taxonomyElement.GetAttributeValue("N", string.Empty);

				var parentCategoryId = taxonomyElement.Parent?.GetAttributeValue("ID", string.Empty).SubstringAfter(CATEGORY_ID_PREFIX) ?? string.Empty;

				category.ParentId = parentCategoryId;

				category.Categories = taxonomyElement.Descendants("C").Select(c => {
					var subCat = new Category()
					{
						Id = c.GetAttributeValue("ID", string.Empty).SubstringAfter(CATEGORY_ID_PREFIX),
						Name = c.GetAttributeValue("N", string.Empty),
						ParentId = c.Parent?.GetAttributeValue("ID", string.Empty).SubstringAfter(CATEGORY_ID_PREFIX) ?? string.Empty
					};
					
					return subCat;
				});
			}



			return category;
		}

		public static IEnumerable<string> GetChildAndSelfCategoryIds(string categoryId)
		{
			XElement taxonomyElement;
			if (CategoriesById.TryGetValue(categoryId, out taxonomyElement))
			{
				return taxonomyElement.DescendantsAndSelf("C").Select(c =>
					c.GetAttributeValue("ID", string.Empty).SubstringAfter(CATEGORY_ID_PREFIX)
				);
			}
			return new string[0];
		}

		public abstract class BaseClass
		{
			protected BaseClass()
			{

			}

			[JsonExtensionData]
			public IDictionary<string, object> CustomAttributes { get; set; }
			[JsonProperty(PropertyName = "id", Required = Required.DisallowNull)]
			[StringLength(100, MinimumLength = 1)]
			public virtual string Id { get; set; }
			[JsonProperty(PropertyName = "type", NullValueHandling = NullValueHandling.Ignore)]
			public virtual string Type { get; set; }
		}

		public abstract class BasePageItem : BaseClass
		{
			protected BasePageItem()
			{

			}
			[JsonProperty(PropertyName = "name")]
			public string Name { get; set; }
		}

		public sealed class Content : BasePageItem
		{
			public Content()
			{

			}
		}

		public sealed class Category : BaseClass
		{
			public Category() { }

			[JsonProperty(PropertyName = "categories")]
			public IEnumerable<Category> Categories { get; set; }
			[JsonProperty(PropertyName = "description")]
			public string Description { get; set; }
			[JsonProperty(PropertyName = "image")]
			public string Image { get; set; }
			[JsonProperty(PropertyName = "name")]
			public string Name { get; set; }
			[JsonProperty(PropertyName = "page_description")]
			public string PageDescription { get; set; }
			[JsonProperty(PropertyName = "page_keywords")]
			public string PageKeywords { get; set; }
			[JsonProperty(PropertyName = "page_title")]
			public string PageTitle { get; set; }
			[JsonProperty(PropertyName = "parent_category_id")]
			public string ParentId { get; set; }
			[JsonProperty(PropertyName = "thumbnail")]
			public string Thumbnail { get; set; }
		}

		public sealed class ContentSearchRefinement : BaseClass
		{
			public ContentSearchRefinement()
			{

			}

			[JsonProperty(PropertyName = "attribute_id")]
			public override string Id { get; set; }
			[JsonProperty(PropertyName = "label")]
			public string Label { get; set; }
			[JsonProperty(PropertyName = "values")]
			public IEnumerable<ContentSearchRefinementValue> Values { get; set; }
		}

		public sealed class ContentSearchRefinementValue
		{
			public ContentSearchRefinementValue()
			{

			}

			[JsonProperty(PropertyName = "hit_count")]
			public int Count { get; set; }
			[JsonProperty(PropertyName = "description")]
			public string Description { get; set; }
			[JsonProperty(PropertyName = "label")]
			public string Label { get; set; }
			[JsonProperty(PropertyName = "value")]
			public string Value { get; set; }
		}

		public sealed class SearchContentResult : BaseResult<Content>
		{
			public SearchContentResult()
			{

			}
		}

		public abstract class BaseResult<T>
		{
			protected BaseResult()
			{

			}

			[JsonProperty(PropertyName = "count", NullValueHandling = NullValueHandling.Ignore)]
			public int Count { get; set; }
			[JsonProperty(PropertyName = "data")]
			public virtual IEnumerable<T> Data { get; set; }
			[JsonProperty(PropertyName = "total", NullValueHandling = NullValueHandling.Ignore)]
			public int Total { get; set; }
		}

		public sealed class ContentSearchResult : BasePagedResult<Content>
		{
			public ContentSearchResult()
			{

			}

			[JsonProperty(PropertyName = "hits")]
			public override IEnumerable<Content> Data { get; set; }
			[JsonProperty(PropertyName = "query")]
			public string Query { get; set; }
			[JsonProperty(PropertyName = "refinements")]
			public IEnumerable<ContentSearchRefinement> Refinements { get; set; }
			[JsonProperty(PropertyName = "selected_refinements")]
			public IDictionary<string, string> SelectedRefinements { get; set; }
		}

		public abstract class BasePagedResult<T> : BaseResult<T>
		{
			protected BasePagedResult() { }

			[JsonProperty(PropertyName = "next")]
			public string Next { get; set; }
			[JsonProperty(PropertyName = "previous")]
			public string Previous { get; set; }
			[JsonProperty(PropertyName = "start")]
			public int StartIndex { get; set; }
		}
	}
}