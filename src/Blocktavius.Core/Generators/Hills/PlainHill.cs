using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core.Generators.Hills;

public static class PlainHill
{
	public enum CornerType
	{
		Bevel,
		Square,
	};

	public sealed record Settings
	{
		public required int MinElevation { get; init; }
		public required int MaxElevation { get; init; }
		public required int Steepness { get; init; }
		public required CornerType CornerType { get; init; }

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

	public static I2DSampler<int> BuildPlainHill(IArea area, Settings settings)
	{
		int expansion = settings.StepCount;
		var hillBounds = new Rect(area.Bounds.start.Add(-expansion, -expansion), area.Bounds.end.Add(expansion, expansion));
		var hill = new MutableArray2D<int>(hillBounds, -1);

		bool done;
		do
		{
			done = true;
			foreach (var xz in hillBounds.Enumerate())
			{
				int oldVal = hill.Sample(xz);
				int newVal;
				if (area.InArea(xz))
				{
					newVal = settings.MaxElevation;
				}
				else if (settings.CornerType == CornerType.Square)
				{
					newVal = xz.AllNeighbors().Select(hill.Sample).Max() - settings.Steepness;
				}
				else // Bevel
				{
					newVal = xz.CardinalNeighbors().Select(hill.Sample).Max() - settings.Steepness;
				}

				if (newVal > oldVal && newVal >= settings.MinElevation)
				{
					hill.Put(xz, newVal);
					done = false;
				}
			}
		}
		while (!done);

		return hill;
	}
}
