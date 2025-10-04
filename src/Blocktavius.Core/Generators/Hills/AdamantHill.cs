using System;

namespace Blocktavius.Core.Generators.Hills;

/// <summary>
/// Hill generator using the Adamant cliff algorithm (Jaunt-based port of QuaintCliff).
/// </summary>
public static class AdamantHill
{
	public sealed record Settings
	{
		public required PRNG Prng { get; init; }
		public required int MaxElevation { get; init; }
		public required int MinElevation { get; init; }
		public int CornerDebug { get; set; } = 0;

		/// <summary>
		/// Configuration for the Adamant cliff algorithm.
		/// If null, uses default configuration which matches QuaintCliff settings:
		/// - MinSeparation = 1, MaxSeparation = 4
		/// - RunWidthMin = 2, RunWidthMax = 5
		/// - MaxLaneCount = 5
		/// - UnacceptableZFlatness = 10, ShimMinOffset = 2
		/// </summary>
		public AdamantCliffBuilder.Config? CliffConfig { get; init; } = null;
	}

	public static I2DSampler<Elevation> BuildAdamantHills(Region region, Settings settings)
	{
		var builder = new AdamantHillBuilder
		{
			MinElevation = new Elevation(settings.MinElevation),
			MaxElevation = new Elevation(settings.MaxElevation),
			Prng = settings.Prng.AdvanceAndClone(),
			CornerDebug = settings.CornerDebug,
			CliffConfig = settings.CliffConfig,
		};
		return builder.BuildHill(region);
	}

	sealed class AdamantHillBuilder : AdditiveHillBuilder
	{
		public required Elevation MinElevation { get; init; }
		public required Elevation MaxElevation { get; init; }
		public required PRNG Prng { get; init; }
		public AdamantCliffBuilder.Config? CliffConfig { get; init; }

		protected override bool ShouldFillRegion(out Elevation elevation)
		{
			elevation = MaxElevation;
			return true;
		}

		protected override ICliffBuilder CreateCliffBuilder(Edge edge)
		{
			const int cornerReservedSpace = 200;

			return new AdamantCliffBuilder(
				mainLength: edge.Length,
				reservedSpacePerCorner: cornerReservedSpace,
				min: MinElevation,
				max: MaxElevation,
				prng: Prng,
				config: CliffConfig
			);
		}
	}
}
