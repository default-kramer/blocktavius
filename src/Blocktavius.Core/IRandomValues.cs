using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

public interface IRandomValues<out T>
{
	T NextValue(PRNG prng);
}

public interface IBoundedRandomValues<out T> : IRandomValues<T>
{
	T MinValue { get; }
	T MaxValue { get; }
}

public static class RandomValues
{
	public static IRandomValues<T> FromList<T>(IEnumerable<T> values) => new ListValues<T> { values = values.ToList() };

	public static IRandomValues<T> FromValues<T>(params T[] values) => FromList(values);

	public static IRandomValues<T> InfiniteDeck<T>(params T[] values) => new InfiniteDeckValues<T> { values = values.ToList() };

	public static IBoundedRandomValues<int> BoundedInfiniteDeck(params int[] values) => new BoundedInfiniteDeckValues { values = values.ToList() };

	public static IBoundedRandomValues<int> FromRange(int min, int max) => new RangeValues() { MinValue = min, MaxValue = max };

	private sealed class RangeValues : IBoundedRandomValues<int>
	{
		public required int MinValue { get; init; }
		public required int MaxValue { get; init; }
		public int NextValue(PRNG prng) => prng.NextInt32(MinValue, MaxValue + 1);
	}

	private sealed class ListValues<T> : IRandomValues<T>
	{
		public required IReadOnlyList<T> values;

		public T NextValue(PRNG prng)
		{
			int index = prng.NextInt32(values.Count);
			return values[index];
		}
	}

	private class InfiniteDeckValues<T> : IRandomValues<T>
	{
		public required List<T> values;
		private int index = -1;

		public T NextValue(PRNG prng)
		{
			index++;
			if (index >= values.Count)
			{
				prng.Shuffle(values);
				index = 0;
			}
			return values[index];
		}
	}

	private sealed class BoundedInfiniteDeckValues : InfiniteDeckValues<int>, IBoundedRandomValues<int>
	{
		public int MinValue => values.Min();
		public int MaxValue => values.Max();
	}
}
