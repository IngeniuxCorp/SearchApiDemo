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
        public const string ROOT_CATEGORY_NODE_ID_VALUE = "CategoryNodes/root";
        public const string ROOT_CATEGORY_NODE_VALUE = "root";
        public const string ROOT_CATEGORY_NODE_NAME = "Root";
        private const string DEFAULT_CATEGORY_NAME = "Categories";
        private static object _TaxCacheLock = new object();


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
                                elementsById.Add(ROOT_CATEGORY_NODE_VALUE, xmlFile.Descendants("Tree").FirstOrDefault());

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

        public static Dictionary<string, int> CategoryCountsById
        {
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

                            var taxPath = Path.Combine(pageFilePath, TAXONOMY_ASSOC_NAME);
                            if (countsById == null && File.Exists(taxPath))
                            {
                                var xmlFile = XElement.Load(taxPath);

                                //strip off CategoryNodes/ becacuse search doesnt use it
                                countsById = xmlFile.Descendants("A")
                                    .GroupBy(e => e.GetAttributeValue("C", string.Empty))
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

            if (!searchItem.AdditionalFields.TryGetValue(fieldName, out value))
            {
                value = valueDefault;
            }

            return value;
        }

        public static ContentSearchResult ConvertContentSearchResults(IEnumerable<SearchResultItem> searchResults, int totalCount,
            IEnumerable<IEnumerable<QueryFilter>> filters, IEnumerable<KeyValuePair<string, int>> categoryStats, string baseUri, int startIndex = 1)
        {
            ContentSearchResult results = new ContentSearchResult();
            results.Results = _ConvertSearchResults(searchResults, baseUri);
            results.Total = totalCount;
            results.Count = searchResults.Count();
            results.StartIndex = startIndex;
            results.SelectedFilters = filters;
            results.Filters = ConvertContentSearchRefinements(categoryStats);

            return results;
        }

        public static SearchContentResult ConvertContentResults(IEnumerable<SearchResultItem> searchResults, int totalCount, string baseUri)
        {
            SearchContentResult results = new SearchContentResult();
            results.Results = _ConvertSearchResults(searchResults, baseUri);
            results.Total = totalCount;
            results.Count = searchResults.Count();

            return results;
        }

        private static IEnumerable<Content> _ConvertSearchResults(IEnumerable<SearchResultItem> searchResults, string baseUri)
        {
            return searchResults.Select(r => new Content(r, baseUri));
        }

        public static IEnumerable<ContentSearchRefinement> ConvertContentSearchRefinements(IEnumerable<KeyValuePair<string, int>> categoryStats)
        {
            Dictionary<string, ContentSearchRefinement> refinementsByCatId = new Dictionary<string, ContentSearchRefinement>();

            foreach (var stat in categoryStats)
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

                if (!refinementsByCatId.TryGetValue(parentId, out searchRefinement))
                {
                    searchRefinement = new ContentSearchRefinement();
                    searchRefinement.Id = "categorynodes";
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

        private static IEnumerable<IGrouping<string, KeyValuePair<string, string>>> _GetEmbeddedCompList(SearchResultItem result, string compListFullName, string compName)
        {
            Regex indexPattern = new Regex($"^{compListFullName}_(\\d+)__{compName}__");

            var embeddedCompList = result
                .AdditionalFields
                .Where(v => v.Key.StartsWith($"{compListFullName}_"))
                .GroupBy(v =>
                {
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


        private static string _GetEmbeddedFieldValue(IEnumerable<KeyValuePair<string, string>> embeddedItem, string fieldName, string defaultValue = "")
        {
            var fields = embeddedItem.Where(d => d.Key.EndsWith(fieldName));
            if (fields.Any())
            {
                var headingName = fields.First();
                return headingName.Value;
            }
            return defaultValue;
        }

        public static Category GetCategoryResults(string categoryId)
        {
            XElement taxonomyElement;
            Category category = new Category();
            if (CategoriesById.TryGetValue(categoryId, out taxonomyElement))
            {
                category.Id = taxonomyElement.GetAttributeValue("ID", string.Empty)?.SubstringAfter(CATEGORY_ID_PREFIX) ?? ROOT_CATEGORY_NODE_ID_VALUE;
                category.Name = taxonomyElement.GetAttributeValue("N", string.Empty) ?? ROOT_CATEGORY_NODE_NAME;
                category.Description = taxonomyElement.GetAttributeValue("D", string.Empty);

                var parentCategoryId = taxonomyElement.Parent?.GetAttributeValue("ID", string.Empty).SubstringAfter(CATEGORY_ID_PREFIX) ?? string.Empty;

                category.ParentId = parentCategoryId;

                category.Categories = taxonomyElement.Elements("C").Select(c =>
                {
                    var subCat = new Category()
                    {
                        Id = c.GetAttributeValue("ID", string.Empty).SubstringAfter(CATEGORY_ID_PREFIX),
                        Name = c.GetAttributeValue("N", string.Empty),
                        Description = c.GetAttributeValue("D", string.Empty),
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

            public Content(SearchResultItem r, string baseUri)
            {
                Id = r.UniqueID;
                Name = r.Name;
                Type = r.Type;
                Uri = $"{baseUri.TrimEnd('/')}{r.AdditionalFields["path"]}";
                Properties = r.AdditionalFields;
            }

            [JsonProperty(PropertyName = "uri")]
            public string Uri { get; set; }
            public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
        }

        public sealed class Category : BaseClass
        {
            public Category() { }

            [JsonProperty(PropertyName = "categories")]
            public IEnumerable<Category> Categories { get; set; }
            [JsonProperty(PropertyName = "description")]
            public string Description { get; set; }
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }
            [JsonProperty(PropertyName = "parent_category_id")]
            public string ParentId { get; set; }
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

            [JsonProperty(PropertyName = "result_count")]
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
            public virtual IEnumerable<T> Results { get; set; }
            [JsonProperty(PropertyName = "total", NullValueHandling = NullValueHandling.Ignore)]
            public int Total { get; set; }
        }

        public sealed class ContentSearchResult : BasePagedResult<Content>
        {
            public ContentSearchResult()
            {

            }

            [JsonProperty(PropertyName = "results")]
            public override IEnumerable<Content> Results { get; set; }
            [JsonProperty(PropertyName = "query")]
            public string Query { get; set; }
            [JsonProperty(PropertyName = "filters")]
            public IEnumerable<ContentSearchRefinement> Filters { get; set; }
            [JsonProperty(PropertyName = "selected_filters")]
            public IEnumerable<IEnumerable<QueryFilter>> SelectedFilters { get; set; }
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