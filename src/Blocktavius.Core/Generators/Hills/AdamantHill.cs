using System;

namespace Blocktavius.Core.Generators.Hills;

public static class AdamantHill
{
	public sealed record Settings
	{
		public required PRNG Prng { get; init; }
		public required int MaxElevation { get; init; }
		public int CornerDebug { get; set; } = 0;
		public required AdamantCliffBuilder.Config CliffConfig { get; init; }
	}

	public static I2DSampler<Elevation> BuildAdamantHills(Region region, Settings settings)
	{
		var builder = new AdamantHillBuilder
		{
			Settings = settings,
			CornerDebug = settings.CornerDebug,
		};
		return builder.BuildHill(region);
	}

	sealed class AdamantHillBuilder : AdditiveHillBuilder
	{
		public required Settings Settings { get; init; }

		protected override bool ShouldFillRegion(out Elevation elevation)
		{
			elevation = new Elevation(Settings.MaxElevation);
			return true;
		}

		protected override ICliffBuilder CreateCliffBuilder(Edge edge)
		{
			const int cornerReservedSpace = 200;

			return new AdamantCliffBuilder(
				mainLength: edge.Length,
				reservedSpacePerCorner: cornerReservedSpace,
				max: new Elevation(Settings.MaxElevation),
				prng: Settings.Prng,
				config: Settings.CliffConfig
			);
		}
	}
}
