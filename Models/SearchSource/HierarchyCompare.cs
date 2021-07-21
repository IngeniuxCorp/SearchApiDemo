using Lucene.Net.Index;
using Lucene.Net.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ingeniux.Runtime.Models.SearchSource
{
	public class HierarchyCompareSource : FieldComparatorSource
	{
		public override FieldComparator NewComparator(string fieldname, int numHits, int sortPos, bool reversed)
		{
			return new HierarchyCompare(numHits, fieldname, reversed);
		}
	}

	public class HierarchyCompare : FieldComparator
	{
		public const string HIERARCHY_VALUE_NAME = "hierarchyValue";
		private string[] values;
		private string[] currentReaderValues;
		private string field;
		private string bottom; // Value of bottom of queue
		private int reversed;

		public HierarchyCompare(int numHits, string field, bool reversed)
		{
			values = new string[numHits];
			this.field = field;
			this.reversed = reversed ? -1 : 1;
		}

		public override int Compare(int slot1, int slot2)
		{
			string v1 = values[slot1];
			string v2 = values[slot2];

			return _CompareHierarchy(v1, v2);
		}

		private int _CompareHierarchy(string h1, string h2)
		{
			if (h1 == null)
			{
				h1 = string.Empty;
			}

			if (h2 == null)
			{
				h2 = string.Empty;
			}
			try
			{
				IEnumerable<int> h1Numbers = h1.Split('|').Select(i => int.Parse(i));
				IEnumerable<int> h2Numbers = h2.Split('|').Select(i => int.Parse(i));

				for (int i = 0; i < h1Numbers.Count(); i++)
				{
					var n1 = h1Numbers.ElementAt(i);
					var n2 = h2Numbers.ElementAtOrDefault(i);
					if (n1 > n2)
					{
						return 1;
					}
					if (n1 < n2)
					{
						return -1;
					}
				}
				return 0;
			}
			catch
			{
				return 0;
			}
		}

		public override int CompareBottom(int doc)
		{
			string v2 = currentReaderValues[doc];
			return _CompareHierarchy(bottom, v2);
		}

		public override void Copy(int slot, int doc)
		{
			values[slot] = currentReaderValues[doc];
		}

		public override void SetNextReader(IndexReader reader, int docBase)
		{
			currentReaderValues = FieldCache_Fields.DEFAULT.GetStrings(reader, $"_SF_{field}");
		}

		public override void SetBottom(int bottom)
		{
			this.bottom = values[bottom];
		}

		public override IComparable this[int slot] => values[slot];
	}
}