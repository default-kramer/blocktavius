using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core.Generators.Hills;

public static class WinsomeHill
{
	public static I2DSampler<Elevation> BuildWinsomeHills(Region region, PRNG prng, int maxElevation, int steepness)
	{
		var builder = new WinsomeHillBuilder()
		{
			MinElevation = new Elevation(maxElevation - 30),
			MaxElevation = new Elevation(maxElevation),
			Steepness = steepness,
			Prng = prng.AdvanceAndClone(),
		};
		return builder.BuildHill(region);
	}

	sealed class WinsomeHillBuilder : AdditiveHillBuilder
	{
		public required Elevation MinElevation { get; init; }
		public required Elevation MaxElevation { get; init; }
		public required int Steepness { get; init; }
		public required PRNG Prng { get; init; }

		protected override bool ShouldFillRegion(out Elevation elevation)
		{
			elevation = MaxElevation;
			return true;
		}

		protected override ICliffBuilder CreateCliffBuilder(Edge edge)
		{
			const int cornerReservedSpace = 200; // TODO: we just hope it's enough!

			return new CliffBuilder(mainLength: edge.Length, cornerReservedSpace, MinElevation, MaxElevation, Prng)
			{
				steepness = Steepness,
			};
		}
	}
}
