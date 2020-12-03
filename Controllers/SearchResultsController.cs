using Ingeniux.Search;
using Lucene.Net.Search;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Ingeniux.Runtime.Controllers
{
	/// <summary>
	/// This is a sample controller for search results. 
	/// The purpose of this controller is to allow site developers to specify custom queries and facet logics.
	/// It uses the latest API method for QueryFinal that accept "SearcInstruction" object as parameter to
	/// description customized query for search.
	/// Please do not use this controller, unless you have to manipulate the query.
	/// Stick to DEX components when possible.
	/// </summary>
	/// <remarks>
	/// The controller assumes the search results page schema name is "SearchResults". It is a controller 
	/// just for this schema. It will also look for Views/SearchResults/SearchResults.cshtml as the specific view
	/// location.
	/// It is recommended to write schema specific custom controllers and views.
	/// IMPORTANT: This controller MUST be renamed before included in the project. This is the only way to prevent it from
	/// being overwritten during upgrade.
	/// Therefore, The schema name for the search result page shouldn't be actually called SearchResults.
	/// </remarks>
	public class SearchResultsController : CMSPageDefaultController
	{
		/// <summary>
		/// This is the only method that need overriding.
		/// The goal of override is to set the search results object as .Tag of the pageRequest.
		/// </summary>
		/// <param name="pageRequest"></param>
		/// <returns></returns>
		internal override ActionResult handleStandardCMSPageRequest(CMSPageRequest pageRequest)
		{
			_GetSearchResults(pageRequest);
			return base.handleStandardCMSPageRequest(pageRequest);
		}

		/// <summary>
		/// This is the method that put search pieces together, including constructing
		/// instructions, performing search, and attach search results on page request object.
		/// </summary>
		/// <param name="pageRequest"></param>
		protected void _GetSearchResults(CMSPageRequest pageRequest)
		{
			int pagesize, page;
			bool all;
			SiteSearch siteSearch;
			SearchInstruction instructions = getSearchInstructions(pageRequest,
				out page,
				out pagesize,
				out all);

			Ingeniux.Search.Search search = new Ingeniux.Search.Search(Request);

			int count = 0;

			//use the SearchInstruction based overload method to achieve maxt flexibility
			pageRequest.Tag = search.QueryFinal(SiteSearch, out count, instructions,
				200, all, page, pagesize);
		}

		/// <summary>
		/// This is the method that will construct the SearchInstruction object.
		/// This is the place to manipulate the SearchInstruction object in order to
		/// generate a query with custom logics
		/// </summary>
		/// <param name="pageRequest">CMS Page object</param>
		/// <param name="page">Page number, default to 1</param>
		/// <param name="pagesize">Page size, default to 10</param>
		/// <param name="all">All records or not</param>
		/// <returns>Customized search instructions</returns>
		private SearchInstruction getSearchInstructions(CMSPageRequest pageRequest,
			out int page,
			out int pagesize,
			out bool all)
		{
			string[] termsA = pageRequest.QueryString["terms"]
				.ToNullOrEmptyHelper()
				.Propagate(
					s => s.Split(','))
				.Return(new string[0]);

			//default to not categories filter
			string[] catsA = pageRequest.QueryString["catids"]
				.ToNullOrEmptyHelper()
				.Propagate(
					s => s.Split(','))
				.Return(new string[0]);

			bool categoryById = pageRequest.QueryString["catsbyid"]
				.ToNullOrEmptyHelper()
				.Propagate(
					sa => sa.ToBoolean())
				.Return(false);

			//default to no types filter
			string[] typesA = pageRequest.QueryString["types"]
				.ToNullOrEmptyHelper()
				.Propagate(
					s => s.Split(','))
				.Return(new string[0]);

			string[] localesA = pageRequest.QueryString["locales"]
				.ToNullOrEmptyHelper()
				.Propagate(
					s => s.Split(','))
				.Return(new string[0]);

			//default to first page instead of all records
			string pageStr = pageRequest.QueryString["page"]
				.ToNullOrEmptyHelper()
				.Return("1");

			//default page size to 10 records
			pagesize = pageRequest.QueryString["pagesize"]
				.ToNullOrEmptyHelper()
				.Propagate(
					ps => ps.ToInt())
				.Propagate(
					ps => ps.Value)
				.Return(10);

			string[] sourcesA = pageRequest.QueryString["sources"]
				.ToNullOrEmptyHelper()
				.Propagate(
					s => s.Split(','))
				.Return(new string[0]);

			string sortby = pageRequest.QueryString["sortby"] ?? string.Empty;
			bool sortAscending = pageRequest.QueryString["sortasc"]
				.ToNullOrEmptyHelper()
				.Propagate(
					sa => sa.ToBoolean())
				.Return(false);

			all = false;

			if (!int.TryParse(pageStr, out page))
				all = true;
			else if (page < 1)
				all = true;

			//use the hiliter in configuration, or default hiliter with strong tags
			QueryBuilder.CategoryFilterOperator = Ingeniux.Search.Search.GetCategoryFilterOperator();

			SearchInstruction instructions = new SearchInstruction(SiteSearch.DefaultQueryAnalyzer);
			instructions.AddQuery(
				instructions.GetFullTextTermQuery(Occur.MUST, true, termsA));

			if (typesA.Length > 0)
				instructions.AddQuery(
					instructions.GetTypeQuery(Occur.MUST, typesA));

			if (sourcesA.Length > 0)
			{
				instructions.AddQuery(
					instructions.GetSourceQuery(Occur.MUST, sourcesA));
			}

			if (!string.IsNullOrWhiteSpace(sortby))
			{
				instructions.AddSort(new SortField(sortby, CultureInfo.InvariantCulture,
					!sortAscending));
			}

			if (localesA.Length > 0)
			{
				instructions.AddQuery(
					instructions.GetLocaleQuery(Occur.MUST, localesA));
			}

			if (catsA.Length > 0)
				instructions.AddQuery(
					(!categoryById) ?
						instructions.GetCategoryQuery(Occur.MUST, QueryBuilder.CategoryFilterOperator,
							catsA) :
						instructions.GetCategoryIdQuery(Occur.MUST, QueryBuilder.CategoryFilterOperator,
							catsA));

			return instructions;
		}
	}
}