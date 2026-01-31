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
		public int MaxConsecutiveMisses { get; init; } = 11;
		public int DiscardLayers { get; init; } = 0;
	}

	public static I2DSampler<int> BuildHill(Settings settings, Shell shell)
	{
		if (shell.IsHole)
		{
			throw new ArgumentException("Shell must not be a hole");
		}
		return BuildHill(settings, Layer.FirstLayer(shell, settings.MaxElevation, settings), shell.IslandArea.AsArea());
	}

	private static I2DSampler<int> BuildHill(Settings settings, Layer firstLayer, IArea origArea)
	{
		var layers = new Stack<Layer>();
		layers.Push(firstLayer);

		int discardLayers = settings.DiscardLayers;
		int needLayers = settings.MaxElevation - settings.MinElevation + 1;
		needLayers += discardLayers;

		while (layers.Count < needLayers)
		{
			layers.Push(layers.Peek().NextLayer(settings.Prng));
		}

		var finalLayer = layers.First();
		var area = finalLayer.area;
		var finalExpansion = finalLayer.pendingExpansion.Select(kvp => (kvp.Key, kvp.Value.Elevation)).ToList();
		area.Expand(finalExpansion);

		return area.GetSampler(ExpansionId.MaxValue, settings.MaxElevation)
			.Project(tuple => tuple.Item1 ? Math.Min(settings.MaxElevation, tuple.Item2 + discardLayers) : -1);
	}

	sealed class Layer
	{
		public sealed class PendingShellItem
		{
			public int MissCount { get; set; } // mutable

			/// <summary>
			/// We retain the elevation this item was *introduced* at to maintain
			/// compatability with a previous version of this algorithm.
			/// (Also because I can't figure out why it gets more jagged when we break compatability.)
			/// </summary>
			public int Elevation { get; init; }
		}

		/// <summary>
		/// A "miss" occurs whenever we decide not to push a shell item out for the next layer.
		/// This dictionary tracks consecutive miss counts; it resets to 0 when we push that XZ.
		/// Permitting a larger number of consecutive misses creates a more vertical (steeper) hill.
		/// </summary>
		public readonly Dictionary<XZ, PendingShellItem> pendingExpansion;

		public readonly ExpandableArea<int> area;
		public readonly ShellItemRing shellItems;
		public readonly int elevation;
		public readonly Settings settings;

		private static void UpdateMisses(Dictionary<XZ, PendingShellItem> pendingItems, IEnumerable<ShellItem> shellItems, int elevation)
		{
			// reduce inside corners weight from 3:1 down to 2:1
			foreach (var item in shellItems.Where(i => i.CornerType != CornerType.Inside))
			{
				if (pendingItems.TryGetValue(item.XZ, out var pendingItem))
				{
					pendingItem.MissCount++;
				}
				else
				{
					pendingItems[item.XZ] = new PendingShellItem { MissCount = 1, Elevation = elevation };
				}
			}
		}

		private Layer(Layer prev)
		{
			this.area = prev.area;
			this.pendingExpansion = prev.pendingExpansion;
			this.shellItems = new ShellItemRing(area.CurrentShell());
			this.elevation = prev.elevation - 1;
			this.settings = prev.settings;

			UpdateMisses(pendingExpansion, shellItems, elevation);
		}

		private Layer(Shell shell, int elevation, Settings settings)
		{
			this.area = new ExpandableArea<int>(shell);
			this.shellItems = new ShellItemRing(shell.ShellItems);
			this.pendingExpansion = new();
			this.elevation = elevation;
			this.settings = settings;

			UpdateMisses(pendingExpansion, shellItems, elevation);
		}

		public static Layer FirstLayer(Shell shell, int elevation, Settings settings)
		{
			return new Layer(shell, elevation, settings);
		}

		public Layer NextLayer(PRNG prng)
		{
			var prevLayer = this.shellItems;

			int maxConsecutiveMisses = settings.MaxConsecutiveMisses;

			List<(XZ, int)> expansion = new();

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

					int mustTakeNow = prevLayer.OneLapFrom(loopingIndex)
						.TakeWhile(i => pendingExpansion[i.XZ].MissCount >= maxConsecutiveMisses)
						.Take(maxRunLength)
						.Count();

					int runLength = prng.NextInt32(Math.Max(mustTakeNow, minRunLength), 1 + Math.Max(mustTakeNow, maxRunLength));
					for (int i = 0; i < runLength; i++)
					{
						var prevItem = prevLayer[loopingIndex % prevLayer.Count];
						loopingIndex++;
						int elev = pendingExpansion[prevItem.XZ].Elevation;
						expansion.Add((prevItem.XZ, elev));
					}
				}
				else
				{
					const int minRunLength = 2;
					//const int maxRunLength = 50;
					int maxRunLength = Math.Min(prevLayer.Count / 2, 50);

					int mustStopAt = prevLayer.OneLapFrom(loopingIndex)
						.TakeWhile(i => pendingExpansion[i.XZ].MissCount < maxConsecutiveMisses)
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

			foreach (var item in expansion)
			{
				pendingExpansion.Remove(item.Item1);
			}
			area.Expand(expansion);

			return new Layer(this);
		}
	}
}
