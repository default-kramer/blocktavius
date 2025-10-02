using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

// OKAY, can we make a generic hill builder that accepts:
// * a Region (or list of Edges)
// * a function `CliffBuilder CreateCliffBuilder(int width)`
//
// The AdditiveHillBuilder would
// * put cliffs on the outside of each edge
// * fabricate outside corners by extending both cliffs and combining using min
// * handle inside corners naturally, by taking max
//
// The SubtractiveHillBuilder would be similar maybe... but I feel like that one is
// more complicated in ways I can't precisely describe yet.

sealed class AdditiveHillBuilder
{
	const int OUTSIDE_CORNER_FUDGE = 100; // TODO

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
		// TODO need to use the cliffbuilder to build cliffs for each edge.
		// Get a slice of the cliff starting at Edge.Length and going far enough to overlap.
		// Then rotate/translate and combine using min value
		return new MutableArray2D<Elevation>(new Rect(XZ.Zero, XZ.Zero), new Elevation(-1));
	}
}
