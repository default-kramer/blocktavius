using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core.Generators.Hills;

public static class NewHill
{
	public sealed record Settings
	{
		public required PRNG PRNG { get; init; }
		public int MaxElevation { get; init; }
		public int MinElevation { get; init; }
	}

	const int emptyValue = -1;

	public static I2DSampler<HillItem> BuildNewHill(Settings settings, Shell shell)
	{
		var tier = Tier.CreateFirstTier(shell, settings);
		while (tier.MinElevation > settings.MinElevation)
		{
			tier = tier.CreateNextTier();
		}
		return tier.Array.Crop();
	}

	public record struct HillItem
	{
		public required int Elevation { get; init; }
		public required Slab? Slab { get; init; }
	}

	public sealed class Slab
	{
		public required IReadOnlyList<XZ> XZs { get; init; }
		public required IReadOnlyList<Slab> ParentSlabs { get; init; }

		/// <summary>
		/// FUTURE - We may allow elevation to vary within the slab, but not yet.
		/// </summary>
		public required int MinElevation { get; init; }

		/// <summary>
		/// If no parent slabs exist, set to 0.
		/// Else set to 1 + ParentSlabs.Max(x => x.AncestorCount).
		/// </summary>
		public required int AncestorCount { get; init; }
	}

	sealed class HillItemArray
	{
		private readonly MutableArray2D<HillItem> array;
		private readonly Rect.BoundsFinder boundsFinder = new();

		public HillItemArray(Rect bounds)
		{
			this.array = new MutableArray2D<HillItem>(bounds, new HillItem { Elevation = emptyValue, Slab = null });
		}

		public void Put(XZ xz, HillItem hillItem)
		{
			array.Put(xz, hillItem);
			boundsFinder.Include(xz);
		}

		public I2DSampler<HillItem> Uncropped => array;

		public I2DSampler<HillItem> Crop() => array.Crop(boundsFinder.CurrentBounds() ?? array.Bounds);

		public I2DSampler<bool> AsArea() => array.Project(item => item.Elevation > emptyValue);
	}

	/// <summary>
	/// Each tier is basically "add slabs until previous tier is fully covered."
	/// </summary>
	sealed class Tier
	{
		public required Tier? ParentTier { get; init; }
		public required Settings Settings { get; init; }
		public required IReadOnlyList<Slab> Slabs { get; init; }
		public required HillItemArray Array { get; init; }

		/// <summary>
		/// Must be set to Slabs.Min(slab => slab.MinElevation) when any slabs exist.
		/// </summary>
		public required int MinElevation { get; init; }

		public static Tier CreateFirstTier(Shell shell, Settings settings)
		{
			if (settings.MinElevation >= settings.MaxElevation)
			{
				throw new ArgumentException("MinElevation must be less than MaxElevation");
			}

			// Reserve space and just hope it's enough... TODO make this bulletproof!
			int expansion = settings.MaxElevation - settings.MinElevation;
			var array = new HillItemArray(shell.IslandArea.Bounds.Expand(expansion * 2));

			foreach (var xz in shell.IslandArea.Bounds.Enumerate())
			{
				if (shell.IslandArea.Sample(xz))
				{
					array.Put(xz, new HillItem { Elevation = settings.MaxElevation, Slab = null });
				}
			}

			return new Tier
			{
				ParentTier = null,
				Settings = settings,
				Slabs = [],
				Array = array,
				MinElevation = settings.MaxElevation,
			};
		}

		public Tier CreateNextTier()
		{
			// The shell of the parent tier defines what we need to cover in this tier.
			var shellToCover = ShellLogic.ComputeShells(Array.AsArea())
				.Where(shell => !shell.IsHole)
				.Single();

			// If there's no shell, there's nothing to do.
			if (shellToCover is null)
			{
				throw new Exception("Impossible");
			}

			var slabs = new List<Slab>();
			var uncoveredShellPoints = shellToCover.ShellItems.Select(item => item.XZ).ToHashSet();

			var currentShellItems = shellToCover.ShellItems.ToList();

			while (uncoveredShellPoints.Count > 0)
			{
				var shellItems = currentShellItems;

				// For quick lookups of a shell item's indices, handling corners correctly.
				var shellItemsIndexMap = new Dictionary<XZ, List<int>>();
				for (int i = 0; i < shellItems.Count; i++)
				{
					var xz = shellItems[i].XZ;
					if (!shellItemsIndexMap.TryGetValue(xz, out var indices))
					{
						indices = new List<int>();
						shellItemsIndexMap[xz] = indices;
					}
					indices.Add(i);
				}

				// Pick a random point from the original shell that we haven't covered yet.
				var seedXZ = uncoveredShellPoints.ElementAt(Settings.PRNG.NextInt32(uncoveredShellPoints.Count));

				// If this seed point is no longer on the current shell's boundary, it means an overlapping
				// slab has already covered it and made it an "internal" point. We can consider it covered.
				if (!shellItemsIndexMap.TryGetValue(seedXZ, out var seedIndices))
				{
					uncoveredShellPoints.Remove(seedXZ);
					continue;
				}

				// Determine the run shape based on unique XZ coordinates.
				int uniqueXzCount = 4 + Settings.PRNG.NextInt32(4);
				int seedPositionInRun = Settings.PRNG.NextInt32(uniqueXzCount);
				int uniqueXzsBeforeSeed = seedPositionInRun;

				// Find the start of the run by walking backwards from the seed.
				int seedShellIndex = seedIndices[0];
				int startShellIndex = seedShellIndex;
				var uniqueXzsFound = new HashSet<XZ> { seedXZ };

				for (int i = 0; i < shellItems.Count && uniqueXzsFound.Count <= uniqueXzsBeforeSeed; i++)
				{
					startShellIndex = (seedShellIndex - i + shellItems.Count) % shellItems.Count;
					uniqueXzsFound.Add(shellItems[startShellIndex].XZ);
				}

				// Collect the full run of shell items by walking forwards from the start.
				var slabRunItems = new List<ShellItem>();
				var uniqueXzsInRun = new HashSet<XZ>();
				for (int i = 0; i < shellItems.Count && uniqueXzsInRun.Count < uniqueXzCount; i++)
				{
					int currentIndex = (startShellIndex + i) % shellItems.Count;
					var currentItem = shellItems[currentIndex];
					slabRunItems.Add(currentItem);
					uniqueXzsInRun.Add(currentItem.XZ);
				}

				// Find parent slabs by looking at the neighbors of the run items.
				var parentSlabs = slabRunItems
					.Select(item => Array.Uncropped.Sample(item.XZ.Step(item.InsideDirection)))
					.Select(hillItem => hillItem.Slab)
					.WhereNotNull()
					.Distinct()
					.ToList();

				int ancestorCount;
				int slabElevation;
				if (parentSlabs.Any())
				{
					ancestorCount = parentSlabs.Max(x => x.AncestorCount) + 1;
					slabElevation = parentSlabs.Min(x => x.MinElevation) - 1;
				}
				else
				{
					ancestorCount = 0;
					slabElevation = this.MinElevation - 1;
				}

				var slabXZs = slabRunItems.Select(x => x.XZ).ToList();
				var newSlab = new Slab
				{
					AncestorCount = ancestorCount,
					MinElevation = slabElevation,
					ParentSlabs = parentSlabs,
					XZs = slabXZs,
				};
				slabs.Add(newSlab);

				// Apply the new slab to the work-in-progress array.
				var newHillItem = new HillItem { Elevation = slabElevation, Slab = newSlab };
				foreach (var xz in uniqueXzsInRun) // Use unique XZs for updating array and coverage
				{
					// It's possible for this to overwrite a HillItem from a previous slab in this same tier. This is intentional.
					Array.Put(xz, newHillItem);
					uncoveredShellPoints.Remove(xz);
				}

				if (uncoveredShellPoints.Count > 0)
				{
					currentShellItems = ShellLogic.WalkShellFromPoint(Array.AsArea(), uniqueXzsInRun.First());
				}
			}

			if (slabs.Count == 0)
			{
				throw new Exception("Assert fail - empty tier should be impossible");
			}

			return new Tier
			{
				ParentTier = this,
				Settings = Settings,
				Slabs = slabs,
				Array = Array,
				MinElevation = Math.Min(this.MinElevation, slabs.Min(slab => slab.MinElevation)),
			};
		}
	}
}
