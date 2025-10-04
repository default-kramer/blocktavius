using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

/// <summary>
/// This should handle cornering logic correctly for any reasonable ICliffBuilder.
/// It is called "additive" because the resulting hill will be larger than the given region;
/// we put cliffs against the *outside* of all the region's edges.
/// </summary>
public abstract class AdditiveHillBuilder
{
	public int CornerDebug { get; set; }

	public interface ICliffBuilder
	{
		/// <summary>
		/// The main cliff will be placed along an <see cref="Edge"/> and will always be constructed.
		/// The returned sampler must orient the cliff such that North is tall and South is short.
		/// (Translation doesn't matter; it will be repositioned.)
		/// </summary>
		public I2DSampler<Elevation> BuildMainCliff(int length);

		/// <summary>
		/// Produces an extension of the main cliff to be used to construct a corner.
		/// Orientation of the returned sampler should follow the same rules as the main cliff.
		/// </summary>
		/// <remarks>
		/// For example, imagine that an edge runs south to a corner where it meets
		/// an edge that runs east. To construct a corner, we will call this
		/// on the south-running edge with left:false and on the east-running edge
		/// with left:true. We then use a math.min operation on the two
		/// cliffs to produce reasonable-looking outside corner.
		/// </remarks>
		public I2DSampler<Elevation> BuildCornerCliff(bool left, int length);
	}

	sealed class EdgeCliff
	{
		public required Edge Edge { get; init; }

		// We call CliffBuilder.BuildMainCliff(...) immediately and keep the result here
		public required I2DSampler<Elevation> MainCliff { get; init; }

		public required ICliffBuilder CliffBuilder { get; init; }
	}

	public I2DSampler<Elevation> BuildHill(Region region)
	{
		var corners = region.ComputeCorners();
		var outsideCorners = corners.Where(c => c.CornerType == CornerType.Outside).ToList();

		var edgeCliffs = region.Edges.Select(BuildMainCliff).ToList();

		// these list will contain all edge cliffs and corner cliffs, all translated
		List<I2DSampler<Elevation>> cliffs = edgeCliffs.Select(TransformMainCliff).ToList();

		foreach (var corner in outsideCorners)
		{
			var cliffEW = edgeCliffs.Single(x => x.Edge == corner.EastOrWestEdge);
			var cliffNS = edgeCliffs.Single(x => x.Edge == corner.NorthOrSouthEdge);
			cliffs.Add(BuildOutsideCorner(corner, cliffEW, cliffNS));
		}

		Rect fullBounds = Rect.Union([region.MaybeBetterBounds], cliffs.Select(s => s.Bounds));
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

		if (ShouldFillRegion(out var fillElevation))
		{
			// Backfill any gaps between cliffs and region interior
			BackfillCliffs(sampler, region, fillElevation);

			foreach (var xz in region.Bounds.Enumerate())
			{
				if (region.Contains(xz))
				{
					sampler.Put(xz, fillElevation);
				}
			}
		}

		return sampler;
	}

	protected abstract bool ShouldFillRegion(out Elevation elevation);

	protected abstract ICliffBuilder CreateCliffBuilder(Edge edge);

	/// <summary>
	/// Fills gaps between cliffs and the region interior to ensure cliffs reach the plateau.
	/// Called after building cliffs but before filling the region interior.
	/// Default implementation fills empty cells from the region boundary outward until
	/// hitting cliff data, similar to QuaintCliff's backfill approach.
	/// </summary>
	protected virtual void BackfillCliffs(MutableArray2D<Elevation> sampler, Region region, Elevation fillElevation)
	{
		// For each X,Z position in bounds, if it's empty, walk from the region edge outward
		// and fill until we hit cliff data
		foreach (var edge in region.Edges)
		{
			BackfillEdge(sampler, edge, fillElevation);
		}
	}

	private void BackfillEdge(MutableArray2D<Elevation> sampler, Edge edge, Elevation fillElevation)
	{
		// Walk along the edge, and for each position, fill outward from region toward cliff
		var alongEdge = Direction.Parse(edge.StepDirection).Step;
		var outsideDirection = edge.InsideDirection switch
		{
			CardinalDirection.North => CardinalDirection.South,
			CardinalDirection.South => CardinalDirection.North,
			CardinalDirection.East => CardinalDirection.West,
			CardinalDirection.West => CardinalDirection.East,
			_ => throw new Exception($"Invalid inside direction: {edge.InsideDirection}")
		};
		var outward = Direction.Parse(outsideDirection).Step;

		for (int i = 0; i < edge.Length; i++)
		{
			var edgePos = edge.Start.Add(alongEdge.X * i, alongEdge.Z * i);
			var current = edgePos.Add(outward);

			// Fill outward from region boundary until we hit existing cliff data
			while (sampler.Bounds.Contains(current))
			{
				var elevation = sampler.Sample(current);

				// Only fill truly empty cells (no cliff data)
				if (elevation.Y < 0)
				{
					sampler.Put(current, fillElevation);
					current = current.Add(outward);
				}
				else
				{
					// Hit cliff data - stop here, don't overwrite it
					break;
				}
			}
		}
	}

	private EdgeCliff BuildMainCliff(Edge edge)
	{
		var cliffBuilder = CreateCliffBuilder(edge);

		var sampler = cliffBuilder.BuildMainCliff(edge.Length);

		return new EdgeCliff()
		{
			CliffBuilder = cliffBuilder,
			Edge = edge,
			MainCliff = sampler,
		};
	}

	private I2DSampler<Elevation> BuildOutsideCorner(Corner corner, EdgeCliff ewItem, EdgeCliff nsItem)
	{
		// Determine the corner size - should be large enough to cover both cliff depths.
		// We will build a square having this side length.
		int cornerSize = Math.Max(ewItem.MainCliff.Bounds.Size.Z, nsItem.MainCliff.Bounds.Size.Z);
		var theSquare = new Rect(XZ.Zero, new XZ(cornerSize, cornerSize));

		var meetingPoint = corner.MeetingPoint();

		// Determine which end of each edge this corner is at, and slice accordingly
		bool ewAtStart = ewItem.Edge.Start == meetingPoint;
		bool nsAtStart = nsItem.Edge.Start == meetingPoint;

		bool ewLeft = ewItem.Edge.InsideDirection == CardinalDirection.East ? ewAtStart : !ewAtStart;
		bool nsLeft = nsItem.Edge.InsideDirection == CardinalDirection.North ? nsAtStart : !nsAtStart;

		var sliceEW = ewItem.CliffBuilder.BuildCornerCliff(ewLeft, cornerSize)
			.TranslateTo(XZ.Zero)
			.Crop(theSquare);

		var sliceNS = nsItem.CliffBuilder.BuildCornerCliff(nsLeft, cornerSize)
			.TranslateTo(XZ.Zero)
			.Crop(theSquare);

		if (sliceEW.Bounds != theSquare || sliceNS.Bounds != theSquare)
		{
			throw new Exception("assert fail - cropping not working as expected");
		}

		sliceEW = Rotate(ewItem.Edge, sliceEW);
		sliceNS = Rotate(nsItem.Edge, sliceNS);

		if (CornerDebug == 0) { } // no debug
		else if (CornerDebug % 2 == 0)
		{
			sliceEW = new ConstantSampler<Elevation>
			{
				Bounds = sliceEW.Bounds,
				Value = new Elevation(int.MaxValue)
			};
		}
		else
		{
			sliceNS = new ConstantSampler<Elevation>
			{
				Bounds = sliceNS.Bounds,
				Value = new Elevation(int.MaxValue)
			};
		}

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

	private static I2DSampler<Elevation> TransformMainCliff(EdgeCliff ec)
	{
		var (edge, mainCliff) = (ec.Edge, ec.MainCliff);
		switch (edge.InsideDirection)
		{
			case CardinalDirection.North:
				return mainCliff.TranslateTo(edge.Start);
			case CardinalDirection.South:
				return mainCliff.Rotate(180)
					.TranslateTo(edge.Start.Add(0, -mainCliff.Bounds.Size.Z));
			case CardinalDirection.East:
				return mainCliff.Rotate(90)
					.TranslateTo(edge.Start.Add(-mainCliff.Bounds.Size.Z, 0));
			case CardinalDirection.West:
				return mainCliff.Rotate(270).TranslateTo(edge.Start);
			default:
				throw new Exception($"Assert fail: {edge.InsideDirection}");
		}
	}
}
