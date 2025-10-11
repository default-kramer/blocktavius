using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core.Generators.Hills;

public static class CornerPusherHill
{
	public sealed record Settings
	{
		public required PRNG Prng { get; init; }
		public required int MinElevation { get; init; }
		public required int MaxElevation { get; init; }
	}

	public static I2DSampler<int> BuildHill(Settings settings, IArea origArea)
	{
		var layers = new Stack<Layer>();
		layers.Push(Layer.FirstLayer(origArea));

		int needLayers = settings.MaxElevation - settings.MinElevation + 1;
		while (layers.Count < needLayers)
		{
			layers.Push(layers.Peek().NextLayer(settings.Prng));
		}

		var sampler = new MutableArray2D<int>(layers.Peek().area.Bounds, -1);
		int elevation = settings.MinElevation;
		foreach (var layer in layers)
		{
			foreach (var xz in layer.missCounts.Keys) // this is actually the shell
			{
				sampler.Put(xz, elevation);
			}
			elevation++;
		}
		foreach (var xz in origArea.Bounds.Enumerate())
		{
			if (origArea.InArea(xz))
			{
				sampler.Put(xz, settings.MaxElevation);
			}
		}
		return sampler;
	}

	sealed class Layer
	{
		/// <summary>
		/// A "miss" occurs whenever we decide not to push a shell item out for the next layer.
		/// This dictionary tracks consecutive miss counts; it resets to 0 when we push that XZ.
		/// Permitting a larger number of consecutive misses creates a more vertical (steeper) hill.
		/// </summary>
		public readonly IReadOnlyDictionary<XZ, int> missCounts;

		public readonly Shell shell;
		public readonly IArea area;
		public readonly IImmutableSet<XZ> population;

		private Layer(IArea area, IReadOnlyDictionary<XZ, int> prevMissCounts, IImmutableSet<XZ>? population)
		{
			this.area = area;
			// TODO need to handle multiple shells.
			// (Also, why does a tile-based region have more than 1 shell? Bug?)
			this.shell = ShellLogic.ComputeShells(area).Where(a => !a.IsHole)
				.OrderByDescending(s => s.ShellItems.Count)
				.First();
			this.missCounts = shell.ShellItems
				.Where(si => si.CornerType != CornerType.Inside) // reduce inside corners weight from 3:1 down to 2:1
				.GroupBy(si => si.XZ)
				.ToDictionary(grp => grp.Key, grp => grp.Count() + prevMissCounts.GetValueOrDefault(grp.Key, 0));
			this.population = population
				?? area.Bounds.Enumerate().Where(area.InArea).ToImmutableHashSet();
		}

		public static Layer FirstLayer(IArea area)
		{
			var population = area.Bounds.Enumerate().Where(area.InArea).ToImmutableHashSet();
			return new Layer(area, new Dictionary<XZ, int>(), population);
		}

		public Layer NextLayer(PRNG prng)
		{
			var prevLayer = this.shell.ShellItems;
			var prevArea = this.area;
			IReadOnlyDictionary<XZ, int> prevMissCounts = this.missCounts;

			const int maxConsecutiveMisses = 11;
			var newPopulation = this.population;

			// Starting from a random point on the shell, do one lap.
			// While one lap not completed:
			// * choose random run length, subject to constraints
			// * do or don't push out those shell items for next layer
			// * flip the "do or don't push" flag for the next run
			//
			// To implement the "start from a random point on the shell" we
			// * choose a random start index
			// * loop from startIndex to startIndex + Count
			// * use modulo during list indexing since our loopingIndex will (very likely) go past the end of the list
			int startIndex = prng.NextInt32(prevLayer.Count);
			int endIndex = startIndex + prevLayer.Count;
			int loopingIndex = startIndex;
			bool push = false;
			do
			{
				push = !push;
				if (push)
				{

					const int minRunLength = 2;
					const int maxRunLength = 7;

					int mustTakeNow = OneLapFrom(prevLayer, loopingIndex)
						.TakeWhile(i => prevMissCounts.GetValueOrDefault(i.XZ, 0) >= maxConsecutiveMisses)
						.Take(maxRunLength)
						.Count();

					int runLength = prng.NextInt32(Math.Max(mustTakeNow, minRunLength), 1 + Math.Max(mustTakeNow, maxRunLength));
					for (int i = 0; i < runLength; i++)
					{
						var prevItem = prevLayer[loopingIndex % prevLayer.Count];
						loopingIndex++;
						newPopulation = newPopulation.Add(prevItem.XZ);
					}
				}
				else
				{
					const int minRunLength = 2;
					//const int maxRunLength = 50;
					int maxRunLength = Math.Min(prevLayer.Count / 2, 50);

					int mustStopAt = OneLapFrom(prevLayer, loopingIndex)
						.TakeWhile(i => prevMissCounts.GetValueOrDefault(i.XZ, 0) < maxConsecutiveMisses)
						.Take(maxRunLength)
						.Count();

					int runLength;
					if (mustStopAt < minRunLength)
					{
						runLength = mustStopAt;
					}
					else
					{
						runLength = prng.NextInt32(minRunLength, 1 + Math.Min(mustStopAt, maxRunLength));
					}
					loopingIndex += runLength;
				}
			}
			while (loopingIndex < endIndex);


			var newArea = new Area
			{
				Bounds = new Rect(prevArea.Bounds.start.Add(-1, -1), prevArea.Bounds.end.Add(1, 1)),
				Population = newPopulation,
			};
			return new Layer(newArea, prevMissCounts, newPopulation);
		}
	}

	sealed class Area : IArea
	{
		public required IImmutableSet<XZ> Population { get; init; }
		public required Rect Bounds { get; init; }
		public bool InArea(XZ xz) => Population.Contains(xz);
	}

	private static IEnumerable<T> OneLapFrom<T>(IReadOnlyList<T> list, int start)
	{
		start = start % list.Count;
		int index = start;
		do
		{
			yield return list[index];
			index = (index + 1) % list.Count;
		}
		while (index != start);
	}
}
