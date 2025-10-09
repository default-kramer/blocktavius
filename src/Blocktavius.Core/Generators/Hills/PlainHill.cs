using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core.Generators.Hills;

public static class PlainHill
{
	public sealed record Settings
	{
		public required int MinElevation { get; init; }
		public required int MaxElevation { get; init; }
		public required int Steepness { get; init; }

		// Discard any remainder -- a remainder means that step would take us *below* min elevation
		internal int StepCount => (MaxElevation - MinElevation) / Steepness;

		public bool Validate(out Settings validSettings)
		{
			validSettings = this;
			bool valid = true;
			if (Steepness < 1)
			{
				valid = false;
				validSettings = validSettings with { Steepness = 1 };
			}
			if (MinElevation > MaxElevation)
			{
				valid = false;
				validSettings = validSettings with { MinElevation = MaxElevation };
			}
			return valid;
		}
	}

	public static I2DSampler<int> BuildPlainHill(Region region, Settings settings)
	{
		if (!settings.Validate(out _))
		{
			throw new ArgumentException(nameof(settings));
		}
		var builder = new PlainHillBuilder()
		{
			CliffBuilder = new PlainCliffBuilder { Settings = settings },
		};
		return builder.BuildHill(region);
	}

	sealed class PlainHillBuilder : AdditiveHillBuilder
	{
		public required PlainCliffBuilder CliffBuilder { get; init; }

		protected override ICliffBuilder CreateCliffBuilder(Edge edge) => CliffBuilder;

		protected override bool ShouldFillRegion(out int elevation)
		{
			elevation = CliffBuilder.Settings.MaxElevation;
			return true;
		}
	}

	sealed class PlainCliffBuilder : AdditiveHillBuilder.ICliffBuilder
	{
		public required Settings Settings { get; init; }

		public I2DSampler<int> BuildCornerCliff(bool left, int length) => BuildMainCliff(length);

		public I2DSampler<int> BuildMainCliff(int length)
		{
			return new PlainCliffSampler()
			{
				Length = length,
				Settings = Settings,
			};
		}
	}

	sealed class PlainCliffSampler : I2DSampler<int>
	{
		public required int Length { get; init; }
		public required Settings Settings { get; init; }

		public Rect Bounds => new Rect(XZ.Zero, new XZ(Length, Settings.StepCount));

		public int Sample(XZ xz)
		{
			// Max elevation is used for the plateau; the cliff should begin
			// one step down from that. So add 1 to Z here:
			int elevation = Settings.MaxElevation - (xz.Z + 1) * Settings.Steepness;
			return elevation;
		}
	}
}
