using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core.Generators.Hills;

public static class WinsomeHill
{
	public sealed record Settings
	{
		public required PRNG Prng { get; init; }
		public required int MaxElevation { get; init; }
		public required int MinElevation { get; init; }
		public required int Steepness { get; init; }
		public int CornerDebug { get; set; } = 0;
	}

	public static I2DSampler<int> BuildWinsomeHills(Region region, Settings settings)
	{
		var builder = new WinsomeHillBuilder()
		{
			MinElevation = settings.MinElevation,
			MaxElevation = settings.MaxElevation,
			Steepness = settings.Steepness,
			Prng = settings.Prng.AdvanceAndClone(),
			CornerDebug = settings.CornerDebug,
		};
		return builder.BuildHill(region);
	}

	sealed class WinsomeHillBuilder : AdditiveHillBuilder
	{
		public required int MinElevation { get; init; }
		public required int MaxElevation { get; init; }
		public required int Steepness { get; init; }
		public required PRNG Prng { get; init; }

		protected override bool ShouldFillRegion(out int elevation)
		{
			elevation = MaxElevation;
			return true;
		}

		protected override ICliffBuilder CreateCliffBuilder(Edge edge)
		{
			const int cornerReservedSpace = 200; // TODO: we just hope it's enough!

			return new WinsomeCliffBuilder(mainLength: edge.Length, cornerReservedSpace, MinElevation, MaxElevation, Prng)
			{
				steepness = Steepness,
			};
		}
	}
}
