using System;
using System.Collections.Generic;
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

	public static I2DSampler<int> BuildNewHill(Settings settings, Shell shell)
	{
		if (settings.MinElevation >= settings.MaxElevation)
		{
			throw new ArgumentException("MinElevation must be less than MaxElevation");
		}

		int expansion = settings.MaxElevation - settings.MinElevation;
		var bounds = shell.IslandArea.Bounds.Expand(expansion * 2);
		var array = new MutableArray2D<HillItem>(bounds, new HillItem() { Elevation = emptyValue, Slab = null });
	}

	record struct HillItem
	{
		public required int Elevation { get; init; }
		public required Slab? Slab { get; init; }
	}

	sealed class Slab
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

	/// <summary>
	/// Each tier is basically "add slabs until previous tier is fully covered."
	/// </summary>
	sealed class Tier
	{
		public required Tier? ParentTier { get; init; }
		public required Settings Settings { get; init; }
		public required IReadOnlyList<Slab> Slabs { get; init; }
		public required MutableArray2D<HillItem> Array { get; init; }

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

			int expansion = settings.MaxElevation - settings.MinElevation;
			var bounds = shell.IslandArea.Bounds.Expand(expansion * 2);
			var array = new MutableArray2D<HillItem>(bounds, new HillItem() { Elevation = emptyValue, Slab = null });

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
			// It's possible we might create holes... let's ignore them for now until I can see an example.
			var shellToCover = ShellLogic.ComputeShells(Array.Project(x => x.Elevation > emptyValue))
				.Where(shell => !shell.IsHole)
				.Single();

			var currentShell = shellToCover;

			// While shellToCover is not fully covered by slabs:
			// * choose random run from currentShell such that it covers at least one item from shellToCover
			// * create slab for that run
			//   - must find all parent slabs
			//   - elevation for that slab should be 1 less than min from all parents (or 1 less than max elevation if no parents)
			// * add that slab to the mutable array
			// * recompute currentShell

			// == EXAMPLE ==
			// Very rough idea of how to create a slab:
			int length = 4 + Settings.PRNG.NextInt32(8);
			int start = Settings.PRNG.NextInt32(currentShell.ShellItems.Count - length);
			var exampleRun = currentShell.ShellItems.Skip(start).Take(length);
			// TODO must ensure that exampleRun includes at least one XZ from shellToCover...
			var parentSlabs = exampleRun.Select(item => Array.Sample(item.XZ.Step(item.InsideDirection)))
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
				slabElevation = Settings.MaxElevation - 1;
			}
			var slab = new Slab
			{
				AncestorCount = ancestorCount,
				MinElevation = slabElevation,
				ParentSlabs = parentSlabs,
				XZs = exampleRun.Select(x => x.XZ).ToList(),
			};
		}
	}
}
