using Ingeniux.Runtime.Search;
using Ingeniux.Search;
using Ingeniux.Search.Configuration;
using Ingeniux.Search.Indexing;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Spatial.Prefix;
using Lucene.Net.Spatial.Prefix.Tree;
using MoreLinq;
using Newtonsoft.Json;
using NLog;
using Spatial4n.Core.Context;
using Spatial4n.Core.Shapes;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;

namespace Ingeniux.Runtime.Models.SearchSource
{
	public class HierarchySearchSource : DssContentSearchSource
	{
		private HashSet<SearchItem> _SearchItems = new HashSet<SearchItem>();
		public HierarchySearchSource(IndexingSourceEntryConfig entryConfig, SiteSearch siteSearch, Logger logger) : base(entryConfig, siteSearch, logger)
		{
			SiteSearch.Indexer.OnBeforeAddingDocument += HierarchySearchSource_OnBeforeDocAdd;
		}

		private void HierarchySearchSource_OnBeforeDocAdd(object sender, BeforeDocAddEventArgs e)
		{

			var doc = e.Document;
			_AddHierarchyToDocument(doc);
			_AddParentPathToDocument(doc);

		}

		private void _AddHierarchyToDocument(Document doc)
		{
			var pageId = doc.Get("xID");
			if (string.IsNullOrWhiteSpace(pageId))
			{
				Logger.Info($"could not add HIERARCHY");
			}
			
			doc.RemoveField(HIERARCHY_VALUE_NAME);

			if (PageHierarchyById.TryGetValue(pageId, out string hierarchyValue))
			{
				doc.Add(new Field(HIERARCHY_VALUE_NAME,hierarchyValue,Field.Store.YES,Field.Index.NOT_ANALYZED));
			}
			else
			{
				Logger.Info($"could not find HIERARCHY for {pageId}");
			}
		}

		private void _AddParentPathToDocument(Document doc)
		{
			try
			{
				var ancestryValue = doc.Get("_ANCESTRY_");
				if (string.IsNullOrWhiteSpace(ancestryValue))
				{
					Logger.Debug($"could not add parent");
					return;
				}

				doc.RemoveField(PARENT_PATH);
				doc.RemoveField(PARENT_ID);

				var ancestryPaths = ancestryValue.Split('|');
				string parentAncestry;
				string parentId;
				if (ancestryPaths.Length < 2)
				{
					parentAncestry = string.Empty;
					parentId = string.Empty;
				}
				else
				{
					parentAncestry = ancestryPaths.Slice(0, ancestryPaths.Length - 1).Aggregate((c, n) => $"{c}|{n}");
					parentId = ancestryPaths[ancestryPaths.Length - 2];
				}

				doc.Add(new Field(PARENT_PATH, parentAncestry, Field.Store.YES, Field.Index.NOT_ANALYZED));
				doc.Add(new Field(PARENT_ID, parentId, Field.Store.YES, Field.Index.NOT_ANALYZED));
			}catch(Exception e)
            {
				Logger.Error($"Error calculating parent");
			}

		}

		protected override void parseXmlNodeForFields(XElement element, SearchItem doc, Dictionary<string, string> urls, SearchType typeEntry, string ancestorPrefix, int listItemIndex, HashSet<string> ancestorCompIds, CmsIndexingLogs indexLogs, AssetMap assetMap)
		{
			base.parseXmlNodeForFields(element, doc, urls, typeEntry, ancestorPrefix, listItemIndex, ancestorCompIds, indexLogs, assetMap);


			if (!doc.Keys.Any(k => k.Equals(ESCAPED_CONTENT_VALUE_NAME, StringComparison.InvariantCultureIgnoreCase)))
			{
				var rawAbstractElement = element.Document.Descendants(CONTENT_VALUE_NAME).FirstOrDefault();
				if (rawAbstractElement != null)
				{
					var escapedValue = JsonConvert.SerializeObject(rawAbstractElement.Value);
					doc[ESCAPED_CONTENT_VALUE_NAME] = new SearchField(escapedValue, 1, Field.Index.NO);
				}
			}
		}
		private const string HIERARCHY_BY_ID_CACHE_NAME = "HIERARCHY_BY_ID_CACHE";
		private const string REFERNCE_FILE_NAME = "Reference.xml";
		private const string CATEGORY_ID_PREFIX = "CategoryNodes/";
		private const string HIERARCHY_VALUE_NAME = "HierarchyValue";
		private const string PARENT_VALUE_NAME = "ParentName";
		private const string PARENT_PATH = "ParentPath";
		private const string PARENT_ID = "ParentId";
		private const string CONTENT_VALUE_NAME = "Content";
		private const string PREP_INSTRUCTIONS_VALUE_NAME = "PreparationInstructions";
		private const string ESCAPED_CONTENT_VALUE_NAME = "EscapedContent";
		private const string ESCAPED_PREP_INSTRUCTIONS_VALUE_NAME = "EscapedPreparationInstructions";
		private static object _HierarchyCacheLock = new object();

		private static IEnumerable<KeyValuePair<string,string>> _GetHierarchies(XElement xElement, string currentHierarchy = "1")
		{
			var children = xElement.Elements().Where(e => e.Name.LocalName == "Page");
			int i = 1;
			List<KeyValuePair<string, string>> childHierarchies = new List<KeyValuePair<string, string>>();
			var pageId = xElement.GetAttributeValue("ID", string.Empty);

			//dont add anything if there is no Id
			if (string.IsNullOrWhiteSpace(pageId))
			{
				return childHierarchies;
			}
			childHierarchies.Add(new KeyValuePair<string, string>(pageId, currentHierarchy));
			foreach(var child in children)
			{
				var newHierarchy = $"{currentHierarchy}|{i}";
				childHierarchies.AddRange(_GetHierarchies(child, newHierarchy));
				i++;
			}
			return childHierarchies;

		}

		public static Dictionary<string, string> PageHierarchyById
		{
			get
			{
				ObjectCache cache = MemoryCache.Default;
				Dictionary<string, string> hierarchyById = cache[HIERARCHY_BY_ID_CACHE_NAME] as Dictionary<string, string>;
				if (hierarchyById == null || hierarchyById.Count < 1)
				{
					lock (_HierarchyCacheLock)
					{
						hierarchyById = cache[HIERARCHY_BY_ID_CACHE_NAME] as Dictionary<string, string>;
						if (hierarchyById == null || hierarchyById.Count < 1)
						{
							var pageFilePath = ConfigurationManager.AppSettings["PageFilesLocation"];

							if (string.IsNullOrWhiteSpace(pageFilePath))
							{
								//wtf
							}

							var refPath = Path.Combine(pageFilePath, REFERNCE_FILE_NAME);
							if (hierarchyById == null && File.Exists(refPath))
							{
								var xmlFile = XElement.Load(refPath);

								//strip off CategoryNodes/ becacuse search doesnt use it
								hierarchyById = _GetHierarchies(xmlFile).DistinctBy(v => v.Key).ToDictionary(
									v => v.Key, 
									v => v.Value
								);

								CacheItemPolicy policy = new CacheItemPolicy();
								policy.ChangeMonitors.Add(
									new HostFileChangeMonitor(new[] { refPath })
								);
								cache.Set(HIERARCHY_BY_ID_CACHE_NAME, hierarchyById, policy);
							}
						}
					}
				}
				return hierarchyById;
			}
		}

		private const string PARENT_NAME_BY_ID_CACHE_NAME = "PARENT_NAME_BY_ID_CACHE";

		public static Dictionary<string, string> ParentNameById
		{
			get
			{
				ObjectCache cache = MemoryCache.Default;
				Dictionary<string, string> parentNamesById = cache[PARENT_NAME_BY_ID_CACHE_NAME] as Dictionary<string, string>;
				if (parentNamesById == null || parentNamesById.Count < 1)
				{
					lock (_HierarchyCacheLock)
					{
						parentNamesById = cache[PARENT_NAME_BY_ID_CACHE_NAME] as Dictionary<string, string>;
						if (parentNamesById == null || parentNamesById.Count < 1)
						{
							var pageFilePath = ConfigurationManager.AppSettings["PageFilesLocation"];

							if (string.IsNullOrWhiteSpace(pageFilePath))
							{
								//wtf
							}

							var refPath = Path.Combine(pageFilePath, REFERNCE_FILE_NAME);
							if (parentNamesById == null && File.Exists(refPath))
							{
								var xmlFile = XElement.Load(refPath);

								//strip off CategoryNodes/ becacuse search doesnt use it
								parentNamesById = xmlFile
									.Descendants(xmlFile.GetDefaultNamespace() + "Page")
									.ToDictionary(
										p => p.GetAttributeValue("ID", string.Empty),
										p => p.Parent.GetAttributeValue("Name", string.Empty)
									);

								CacheItemPolicy policy = new CacheItemPolicy();
								policy.ChangeMonitors.Add(
									new HostFileChangeMonitor(new[] { refPath })
								);
								cache.Set(PARENT_NAME_BY_ID_CACHE_NAME, parentNamesById, policy);
							}
						}
					}
				}
				return parentNamesById;
			}
		}

        protected override XMLProcessResult ProcessTaxonomyNavigationXML(XElement element, string fieldName, string value)
		{
			var result = new XMLProcessResult();

			if (element.Ancestors().Select(e => e.Name).Contains("IGX_Presentations")
				|| element.Ancestors().Select(e => e.Name).Contains("SiteControl"))
			{
				result.SkipElement = true;
				return result;
			}

			var startNodesValue = element.Attribute("StartNodes")?.Value;

			if (!string.IsNullOrWhiteSpace(startNodesValue))
			{
				result.AddSupplimentField($"{fieldName}__StartNodes", startNodesValue, Field.Index.NO);
			}

			return result;
		}

	}
}