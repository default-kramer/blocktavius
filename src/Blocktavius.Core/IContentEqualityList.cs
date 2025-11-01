using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

public interface IContentEqualityList<T> : IReadOnlyList<T> { }

public static class ContentEqualityListUtil
{
	/// <summary>
	/// Sorting is the responsibility of the caller.
	/// </summary>
	public static IContentEqualityList<T> ToContentEqualityList<T>(this IEnumerable<T> sequence)
	{
		return sequence as ContentEqualityList<T>
			?? new ContentEqualityList<T>(sequence.ToList());
	}

	sealed class ContentEqualityList<T> : IContentEqualityList<T>
	{
		private readonly IReadOnlyList<T> list;
		private readonly int hashCode;

		public ContentEqualityList(IReadOnlyList<T> list)
		{
			this.list = list;

			hashCode = 0;
			foreach (var item in list)
			{
				hashCode = HashCode.Combine(hashCode, item?.GetHashCode() ?? 0);
			}
		}

		public T this[int index] => list[index];
		public int Count => list.Count;
		public IEnumerator<T> GetEnumerator() => list.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => list.GetEnumerator();

		public override int GetHashCode() => hashCode;
		public override bool Equals(object? obj)
		{
			return obj is ContentEqualityList<T> other
				&& this.hashCode == other.hashCode
				&& this.list.SequenceEqual(other.list);
		}
	}
}
