using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

/// <summary>
/// This should handle cornering logic correctly for any reasonable ICliffBuilder.
/// </summary>
sealed class AdditiveHillBuilder
{
	// Reserved space. We create the CliffBuilder wider than the edge itself needs
	// in case the edge ends up needing corners. We can draw from this reserved space
	// to create cliffs for the corners to use which should seamlessly match the edge's main cliff.
	const int CORNER_OVERLAP = 100;

	public static I2DSampler<Elevation> BuildHill(Region region, Elevation maxElevation, ICliffBuilder cliffBuilder)
	{
		var builder = new AdditiveHillBuilder()
		{
			MaxElevation = maxElevation,
			CliffBuilder = cliffBuilder,
		};
		return builder.BuildHill(region);
	}

	internal interface ICliffBuilder
	{
		public int Width { get; }
		public I2DSampler<Elevation> BuildCliff(Range slice);

		public ICliffBuilder AnotherOne(int width);
	}

	sealed class EdgeCliff
	{
		public required Edge Edge { get; init; }

		/// <summary>
		/// This slice will start at X = <see cref="CORNER_OVERLAP"/> and its length
		/// will match <see cref="Edge.Length"/>.
		/// </summary>
		public required I2DSampler<Elevation> Sampler { get; init; }

		public required ICliffBuilder CliffBuilder { get; init; }

		public I2DSampler<Elevation> TranslateSampler()
		{
			switch (Edge.InsideDirection)
			{
				case CardinalDirection.North:
					return Sampler.TranslateTo(Edge.Start);
				case CardinalDirection.West:
					return Sampler.Rotate(270).TranslateTo(Edge.Start);
				case CardinalDirection.South:
					return Sampler.Rotate(180)
						.TranslateTo(Edge.Start.Add(0, -Sampler.Bounds.Size.Z));
				case CardinalDirection.East:
					return Sampler.Rotate(90)
						.TranslateTo(Edge.Start.Add(-Sampler.Bounds.Size.Z, 0));
				default:
					throw new Exception($"Assert fail: {Edge.InsideDirection}");
			}
		}
	}

	public required ICliffBuilder CliffBuilder { get; init; }
	public required Elevation MaxElevation { get; init; }

	private I2DSampler<Elevation> BuildHill(Region region)
	{
		var corners = region.ComputeCorners();
		var outsideCorners = corners.Where(c => c.CornerType == CornerType.Outside).ToList();

		var edgeCliffs = region.Edges.Select(BuildCliff).ToList();

		// these list will contain all edge cliffs and corner cliffs, all translated
		List<I2DSampler<Elevation>> cliffs = edgeCliffs.Select(x => x.TranslateSampler()).ToList();

		foreach (var corner in outsideCorners)
		{
			var cliffEW = edgeCliffs.Single(x => x.Edge == corner.EastOrWestEdge);
			var cliffNS = edgeCliffs.Single(x => x.Edge == corner.NorthOrSouthEdge);
			cliffs.Add(BuildOutsideCorner(corner, cliffEW, cliffNS));
		}

		Rect fullBounds = Rect.Union([region.Bounds], cliffs.Select(s => s.Bounds));
		var sampler = new MutableArray2D<Elevation>(fullBounds, new Elevation(-1));

		foreach (var cliff in cliffs)
		{
			foreach (var xz in cliff.Bounds.Enumerate())
			{
				var sample = cliff.Sample(xz);
				var exist = sampler.Sample(xz);
				if (sample.Y > exist.Y)
				{
					sampler.Put(xz, sample);
				}
			}
		}
		foreach (var xz in region.Bounds.Enumerate())
		{
			if (region.Contains(xz))
			{
				sampler.Put(xz, MaxElevation);
			}
		}

		return sampler;
	}

	private EdgeCliff BuildCliff(Edge edge)
	{
		// Always allocate space for corners on both sides, regardless of whether they exist
		// This ensures all slicing ranges are always valid
		int totalWidth = edge.Length + CORNER_OVERLAP * 2;
		var cliffBuilder = this.CliffBuilder.AnotherOne(totalWidth);

		// Slice the main edge cliff from the center, excluding corner regions on both sides
		// Range is inclusive, so to get edge.Length values, we need [start, start + length - 1]
		var sampler = cliffBuilder.BuildCliff(new Range(CORNER_OVERLAP, CORNER_OVERLAP + edge.Length - 1));

		return new EdgeCliff()
		{
			CliffBuilder = cliffBuilder,
			Edge = edge,
			Sampler = sampler,
		};
	}

	private I2DSampler<Elevation> BuildOutsideCorner(Corner corner, EdgeCliff cliffEW, EdgeCliff cliffNS)
	{
		// Determine the corner size - should be large enough to cover both cliff depths.
		// We will build a square having this side length.
		int cornerSize = Math.Max(cliffEW.Sampler.Bounds.Size.Z, cliffNS.Sampler.Bounds.Size.Z);
		var theSquare = new Rect(XZ.Zero, new XZ(cornerSize, cornerSize));

		var meetingPoint = corner.MeetingPoint();

		// Determine which end of each edge this corner is at, and slice accordingly
		bool ewAtStart = cliffEW.Edge.Start == meetingPoint;
		bool nsAtStart = cliffNS.Edge.Start == meetingPoint;

		// Choose a slice range for the corner such that the corner slice will be
		// 1) directly adjacent to the main cliff slice and
		// 2) on the correct side of the main cliff slice
		Range ewRange = new Range(CORNER_OVERLAP, CORNER_OVERLAP + cornerSize - 1)
			.Shift(ewAtStart ? -cornerSize : cliffEW.Edge.Length);

		Range nsRange = new Range(CORNER_OVERLAP, CORNER_OVERLAP + cornerSize - 1)
			.Shift(nsAtStart ? -cornerSize : cliffNS.Edge.Length);

		var sliceEW = cliffEW.CliffBuilder.BuildCliff(ewRange)
			.TranslateTo(XZ.Zero)
			.Crop(theSquare);

		var sliceNS = cliffNS.CliffBuilder.BuildCliff(nsRange)
			.TranslateTo(XZ.Zero)
			.Crop(theSquare);

		if (sliceEW.Bounds != theSquare || sliceNS.Bounds != theSquare)
		{
			throw new Exception("assert fail - cropping not working as expected");
		}

		sliceEW = Rotate(cliffEW.Edge, sliceEW);
		sliceNS = Rotate(cliffNS.Edge, sliceNS);

		var result = new MutableArray2D<Elevation>(theSquare, new Elevation(-1));

		// Combine using min (lower elevation wins at outside corners)
		foreach (var xz in theSquare.Enumerate())
		{
			var elevEW = sliceEW.Sample(xz);
			var elevNS = sliceNS.Sample(xz);
			result.Put(xz, new Elevation(Math.Min(elevEW.Y, elevNS.Y)));
		}

		// Translate to final position
		int xFactor = nsAtStart ? -1 : 0;
		int zFactor = ewAtStart ? -1 : 0;
		var target = meetingPoint.Add(cornerSize * xFactor, cornerSize * zFactor);
		return result.TranslateTo(target);
	}

	private static I2DSampler<T> Rotate<T>(Edge edge, I2DSampler<T> sampler)
	{
		switch (edge.InsideDirection)
		{
			case CardinalDirection.North:
				return sampler;
			case CardinalDirection.South:
				return sampler.Rotate(180);
			case CardinalDirection.East:
				return sampler.Rotate(90);
			case CardinalDirection.West:
				return sampler.Rotate(270);
			default:
				throw new Exception($"Assert fail: {edge.InsideDirection}");
		}
	}
}
