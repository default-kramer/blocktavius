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
	public int CornerDebug { get; set; } = 0;

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

		if (ShouldFillRegion(out var fillElevation))
		{
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

		// The meeting point is where edges originally met (and where the corner cliff goes)
		var meetingPoint = corner.MeetingPoint();

		// Edges may have been moved, so check against original meeting point with compensation
		var ewEdge = ewItem.Edge;
		var nsEdge = nsItem.Edge;

		// Determine which end of each edge is at the corner (accounting for movement)
		var ewComp = ewEdge.InsideDirection switch
		{
			CardinalDirection.North => new XZ(0, 1),
			CardinalDirection.West => new XZ(1, 0),
			_ => XZ.Zero
		};
		var nsComp = nsEdge.InsideDirection switch
		{
			CardinalDirection.North => new XZ(0, 1),
			CardinalDirection.West => new XZ(1, 0),
			_ => XZ.Zero
		};

		bool ewAtStart = ewEdge.Start.Add(ewComp) == meetingPoint;
		bool nsAtStart = nsEdge.Start.Add(nsComp) == meetingPoint;

		bool ewLeft = ewEdge.InsideDirection == CardinalDirection.East ? ewAtStart : !ewAtStart;
		bool nsLeft = nsEdge.InsideDirection == CardinalDirection.North ? nsAtStart : !nsAtStart;

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
			sliceEW = new MutableArray2D<Elevation>(sliceEW.Bounds, new Elevation(int.MaxValue));
		}
		else
		{
			sliceNS = new MutableArray2D<Elevation>(sliceNS.Bounds, new Elevation(int.MaxValue));
		}

		var result = new MutableArray2D<Elevation>(theSquare, new Elevation(-1));

		// Combine using min (lower elevation wins at outside corners)
		foreach (var xz in theSquare.Enumerate())
		{
			var elevEW = sliceEW.Sample(xz).Y;
			var elevNS = sliceNS.Sample(xz).Y;
			result.Put(xz, new Elevation(Math.Min(elevEW, elevNS)));
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
		// Edges are now inside the region. Cliffs need to be placed 1 step outside (opposite to InsideDirection).

		// Step 1: Rotate the cliff to face the correct direction
		// Step 2: Move it 1 step opposite to InsideDirection to place it outside
		// Step 3: Adjust for the cliff's depth (rotated bounds may have shifted origin)

		XZ outsideOffset = edge.InsideDirection switch
		{
			CardinalDirection.North => new XZ(0, 1),   // Move south (opposite of north)
			CardinalDirection.South => new XZ(0, -1),  // Move north (opposite of south)
			CardinalDirection.East => new XZ(-1, 0),   // Move west (opposite of east)
			CardinalDirection.West => new XZ(1, 0),    // Move east (opposite of west)
			_ => throw new Exception($"Assert fail: {edge.InsideDirection}")
		};

		switch (edge.InsideDirection)
		{
			case CardinalDirection.South:
			case CardinalDirection.North:
				// For N/S edges, cliff extends in Z direction
				// Inside=South (North edge): cliff extends north, needs 180° rotation and depth adjustment
				// Inside=North (South edge): cliff extends south, no rotation needed
				var rotation = edge.InsideDirection == CardinalDirection.South ? 180 : 0;
				var zAdjust = rotation == 180 ? -(mainCliff.Bounds.Size.Z - 1) : 0;
				var rotated = mainCliff.Rotate(rotation);
				var target = edge.Start.Add(outsideOffset).Add(0, zAdjust);
				return rotated.TranslateTo(target);
			case CardinalDirection.East:
			case CardinalDirection.West:
				// For E/W edges, cliff extends in X direction
				// Inside=East (West edge): cliff extends west, needs 90° rotation and depth adjustment
				// Inside=West (East edge): cliff extends east, needs 270° rotation
				rotation = edge.InsideDirection == CardinalDirection.East ? 90 : 270;
				var xAdjust = rotation == 90 ? -(mainCliff.Bounds.Size.Z - 1) : 0;
				rotated = mainCliff.Rotate(rotation);
				target = edge.Start.Add(outsideOffset).Add(xAdjust, 0);
				return rotated.TranslateTo(target);
			default:
				throw new Exception($"Assert fail: {edge.InsideDirection}");
		}
	}
}
