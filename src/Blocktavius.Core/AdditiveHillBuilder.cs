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
	// TODO we need to actually slice the main cliff from the *center* (not the left edge
	// as we are currently doing). Then depending on the type of corner we are building,
	// we need to slice either the left or right of that center slice.
	const int OUTSIDE_CORNER_FUDGE = 100;

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
		/// This sampler will be normalized from 0 to Edge.Length in the X direction,
		/// and from 0 to whatever is needed in the Z direction.
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
		var edgeCliffs = region.Edges.Select(BuildCliff).ToList();

		// these list will contain all edge cliffs and corner cliffs, all translated
		List<I2DSampler<Elevation>> cliffs = edgeCliffs.Select(x => x.TranslateSampler()).ToList();

		var corners = region.ComputeCorners().ToList();
		foreach (var corner in corners.Where(c => c.CornerType == CornerType.Outside))
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
		var cliffBuilder = this.CliffBuilder.AnotherOne(edge.Length + OUTSIDE_CORNER_FUDGE);
		var sampler = cliffBuilder.BuildCliff(new Range(0, edge.Length));
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

		// Build corner slice extensions starting at edge.Length
		var sliceEW = cliffEW.CliffBuilder.BuildCliff(new Range(cliffEW.Edge.Length, cliffEW.Edge.Length + cornerSize))
			.TranslateTo(XZ.Zero)
			.Crop(theSquare);

		var sliceNS = cliffNS.CliffBuilder.BuildCliff(new Range(cliffNS.Edge.Length, cliffNS.Edge.Length + cornerSize))
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

		var target = GetTargetLoc(corner, cornerSize);
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


	private static XZ GetTargetLoc(Corner corner, int cornerSize)
	{
		var meetingPoint = corner.MeetingPoint();
		int xFactor = corner.NorthOrSouthEdge.Start == meetingPoint ? -1 : 0;
		int zFactor = corner.EastOrWestEdge.Start == meetingPoint ? -1 : 0;
		return meetingPoint.Add(cornerSize * xFactor, cornerSize * zFactor);
	}
}
