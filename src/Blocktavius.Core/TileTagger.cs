using Blocktavius.Core.Generators.Cliffs;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Blocktavius.Core;

public sealed record Edge
{
	public required XZ Start { get; init; }
	public required CardinalDirection StepDirection { get; init; }
	public required CardinalDirection InsideDirection { get; init; }
	public required int Length { get; init; }

	public XZ End => Start.Add(Direction.Parse(StepDirection).Step.Scale(Length));
	public XZ Max => Start.Add(Direction.Parse(StepDirection).Step.Scale(Length - 1));
	public XZ Min => Start;

	internal Edge Scale(XZ scale)
	{
		int lengthScale = 1;
		if (StepDirection == CardinalDirection.East || StepDirection == CardinalDirection.West)
		{
			lengthScale = scale.X;
		}
		else if (StepDirection == CardinalDirection.North || StepDirection == CardinalDirection.South)
		{
			lengthScale = scale.Z;
		}

		var scaledStart = this.Start.Scale(scale);

		// Move South/East edges inside the region
		// In unscaled space, South/East edges are positioned outside (exclusive)
		// In scaled space, we want all edges inside (inclusive at the last row/column)
		if (InsideDirection == CardinalDirection.North)
		{
			// South edge - move one step north to be inside
			scaledStart = scaledStart.Add(0, -1);
		}
		else if (InsideDirection == CardinalDirection.West)
		{
			// East edge - move one step west to be inside
			scaledStart = scaledStart.Add(-1, 0);
		}

		return new Edge()
		{
			Start = scaledStart,
			InsideDirection = this.InsideDirection,
			StepDirection = this.StepDirection,
			Length = this.Length * lengthScale,
		};
	}

	public IEnumerable<XZ> Walk()
	{
		var dir = Direction.Parse(StepDirection);
		var xz = Start;
		int steps = Length;
		while (steps > 0)
		{
			yield return xz;
			xz = xz.Step(dir);
			steps--;
		}
	}
}

enum CornerType { Inside, Outside };

sealed record Corner(Edge NorthOrSouthEdge, Edge EastOrWestEdge, CornerType CornerType, XZ OriginalMeetingPoint)
{
	public XZ MeetingPoint() => OriginalMeetingPoint;
}

public sealed record Region
{
	private readonly IReadOnlySet<XZ> unscaledTiles;
	private readonly XZ scale;

	public Region(IReadOnlySet<XZ> unscaledTiles, XZ scale)
	{
		this.unscaledTiles = unscaledTiles;
		this.scale = scale;
	}

	public required IReadOnlyList<Edge> Edges { get; init; }
	public required Rect Bounds { get; init; }

	public bool Contains(XZ xz) => unscaledTiles.Contains(xz.Unscale(scale));

	internal IReadOnlyList<Corner> ComputeCorners()
	{
		// Helper to get original (pre-scale-adjustment) position for corner detection
		XZ GetOriginalPos(Edge e, bool useEnd)
		{
			var pos = useEnd ? e.End : e.Start;
			// South/East edges were moved -1 during scaling, compensate back to original
			if (e.InsideDirection == CardinalDirection.North)
				pos = pos.Add(0, 1);
			else if (e.InsideDirection == CardinalDirection.West)
				pos = pos.Add(1, 0);
			return pos;
		}

		var startLookup = Edges.GroupBy(e => GetOriginalPos(e, false)).ToDictionary(g => g.Key, g => g.ToList());
		var endLookup = Edges.GroupBy(e => GetOriginalPos(e, true)).ToDictionary(g => g.Key, g => g.ToList());

		// add empty lists so we can always put Start or End into either dictionary safely
		foreach (var edge in Edges)
		{
			var origEnd = GetOriginalPos(edge, true);
			var origStart = GetOriginalPos(edge, false);
			if (!startLookup.ContainsKey(origEnd))
			{
				startLookup[origEnd] = new List<Edge>();
			}
			if (!endLookup.ContainsKey(origStart))
			{
				endLookup[origStart] = new List<Edge>();
			}
		}

		var corners = new List<Corner>();

		foreach (var edge in Edges)
		{
			var edgeOrigStart = GetOriginalPos(edge, false);
			var edgeOrigEnd = GetOriginalPos(edge, true);

			void maybeAdd(Edge? other, XZ meetingPoint, CornerType type)
			{
				if (other != null)
				{
					corners.Add(new Corner(edge, other, type, meetingPoint));
				}
			}

			if (edge.InsideDirection == CardinalDirection.North)
			{
				/*
				  xxxx|
				  xxxx|
				  ----O
				*/
				var found = endLookup[edgeOrigEnd].FirstOrDefault(e => e.InsideDirection == CardinalDirection.West);
				maybeAdd(found, edgeOrigEnd, CornerType.Outside);

				/*
				  |xxxx
				  |xxxx
				  O----
				 */
				found = endLookup[edgeOrigStart].FirstOrDefault(x => x.InsideDirection == CardinalDirection.East);
				maybeAdd(found, edgeOrigStart, CornerType.Outside);

				/*
				 xxxxxx
				 xxxxxx
				 ---Oxx
				    |xx
				    |xx
				 */
				found = startLookup[edgeOrigEnd].FirstOrDefault(x => x.InsideDirection == CardinalDirection.East);
				maybeAdd(found, edgeOrigEnd, CornerType.Inside);

				/*
				  xxxxxx
				  xxxxxx
				  xxO---
				  xx|
				  xx|
				 */
				found = startLookup[edgeOrigStart].FirstOrDefault(x => x.InsideDirection == CardinalDirection.West);
				maybeAdd(found, edgeOrigStart, CornerType.Inside);
			}
			else if (edge.InsideDirection == CardinalDirection.South)
			{
				/*
				  O----
				  |xxxx
				  |xxxx
				 */
				var found = startLookup[edgeOrigStart].FirstOrDefault(e => e.InsideDirection == CardinalDirection.East);
				maybeAdd(found, edgeOrigStart, CornerType.Outside);

				/*
				  ----O
				  xxxx|
				  xxxx|
				 */
				found = startLookup[edgeOrigEnd].FirstOrDefault(e => e.InsideDirection == CardinalDirection.West);
				maybeAdd(found, edgeOrigEnd, CornerType.Outside);

				/*
				     |xx
				     |xx
				  ---Oxx
				  xxxxxx
				  xxxxxx
				 */
				found = endLookup[edgeOrigEnd].FirstOrDefault(e => e.InsideDirection == CardinalDirection.East);
				maybeAdd(found, edgeOrigEnd, CornerType.Inside);

				/*
				  xx|
				  xx|
				  xxO---
				  xxxxxx
				  xxxxxx
				 */
				found = endLookup[edgeOrigStart].FirstOrDefault(e => e.InsideDirection == CardinalDirection.West);
				maybeAdd(found, edgeOrigStart, CornerType.Inside);
			}
		}

		return corners;
	}
}

public sealed class TileTagger<TTag> where TTag : notnull
{
	public XZ UnscaledSize { get; }
	public XZ Scale { get; }
	private MutableArray2D<IImmutableSet<TTag>> array;

	public TileTagger(XZ unscaledSize, XZ scale)
	{
		if (scale.X < 2 || scale.Z < 2)
		{
			throw new ArgumentException($"scale must be at least 2: {scale}");
		}
		UnscaledSize = unscaledSize;
		Scale = scale;
		array = new MutableArray2D<IImmutableSet<TTag>>(new Rect(XZ.Zero, UnscaledSize), ImmutableSortedSet<TTag>.Empty);
	}

	public void AddTag(XZ loc, TTag tag)
	{
		array[loc] = array[loc].Add(tag);
	}

	public IReadOnlyList<Region> GetRegions(TTag tag)
	{
		var regions = FindRegionTiles(array, tag);
		return regions.Select(r => BuildRegion(r, Scale)).ToList();
	}

	/// <summary>
	/// Output is unscaled.
	/// Each hashset contains the unscaled coordinates of tiles which belong in the same region.
	/// </summary>
	private static List<HashSet<XZ>> FindRegionTiles(I2DSampler<IImmutableSet<TTag>> sampler, TTag tag)
	{
		var bounds = sampler.Bounds;
		if (bounds.start != XZ.Zero)
		{
			throw new Exception("Assert fail");
		}

		var regions = new List<HashSet<XZ>>();
		var visited = new MutableArray2D<bool>(bounds, false);

		foreach (var xz in sampler.Bounds.Enumerate())
		{
			if (visited[xz])
			{
				continue;
			}
			visited[xz] = true;

			if (!sampler.Sample(xz).Contains(tag))
			{
				continue;
			}

			var queue = new Queue<XZ>();
			queue.Enqueue(xz);
			var regionTiles = new HashSet<XZ>();

			while (queue.Count > 0)
			{
				var loc = queue.Dequeue();
				regionTiles.Add(loc);

				foreach (var neighbor in loc.CardinalNeighbors())
				{
					if (!bounds.Contains(neighbor) || visited[neighbor])
					{
						continue;
					}
					visited[neighbor] = true;

					if (sampler.Sample(neighbor).Contains(tag))
					{
						queue.Enqueue(neighbor);
					}
				}
			}

			regions.Add(regionTiles);
		}

		return regions;
	}

	/// <summary>
	/// Input is unscaled; output is scaled.
	/// </summary>
	private static Region BuildRegion(IReadOnlySet<XZ> unscaledTiles, XZ scale)
	{
		var unscaledSegments = GetEdgeSegments(unscaledTiles);
		var scaledEdges = CombineEdges(unscaledSegments)
			.Select(edge => edge.Scale(scale))
			.ToList();

		// All edges are now inside the region, so use Min/Max for inclusive bounds
		var allPoints = scaledEdges.Select(e => e.Min).Concat(scaledEdges.Select(e => e.Max));
		int minX = allPoints.Min(p => p.X);
		int minZ = allPoints.Min(p => p.Z);
		int maxX = allPoints.Max(p => p.X);
		int maxZ = allPoints.Max(p => p.Z);
		// Rect end is exclusive, so add 1 to max values
		var scaledBounds = new Rect(new XZ(minX, minZ), new XZ(maxX + 1, maxZ + 1));

		return new Region(unscaledTiles, scale)
		{
			Bounds = scaledBounds,
			Edges = scaledEdges,
		};
	}

	/// <summary>
	/// Output is unscaled.
	/// Returns one "edge segment" for each side of each tile
	/// when the tile lacks a neighbor on that side.
	/// </summary>
	private static HashSet<Edge> GetEdgeSegments(IReadOnlySet<XZ> unscaledTiles)
	{
		var segments = new HashSet<Edge>();
		foreach (var tile in unscaledTiles)
		{
			void TestAndAdd(Direction direction, Edge edge)
			{
				if (!unscaledTiles.Contains(tile.Add(direction.Step)))
				{
					segments.Add(edge);
				}
			}

			var cornerNW = tile;

			// North empty? Then go NW -> NE
			TestAndAdd(Direction.North, new Edge()
			{
				InsideDirection = CardinalDirection.South,
				Start = cornerNW,
				StepDirection = CardinalDirection.East,
				Length = 1,
			});

			// South empty? Then go SW -> SE (positioned outside in unscaled space)
			TestAndAdd(Direction.South, new Edge()
			{
				InsideDirection = CardinalDirection.North,
				Start = cornerNW.Step(Direction.South),
				StepDirection = CardinalDirection.East,
				Length = 1,
			});

			// West empty? Then go NW -> SW
			TestAndAdd(Direction.West, new Edge()
			{
				InsideDirection = CardinalDirection.East,
				Start = cornerNW,
				StepDirection = CardinalDirection.South,
				Length = 1,
			});

			// East empty? Then go NE -> SE (positioned outside in unscaled space)
			TestAndAdd(Direction.East, new Edge()
			{
				InsideDirection = CardinalDirection.West,
				Start = cornerNW.Step(Direction.East),
				StepDirection = CardinalDirection.South,
				Length = 1,
			});
		}

		return segments;
	}

	/// <summary>
	/// This method should work correctly whether or not the input is scaled.
	///
	/// Combines edge segments into edges.
	/// For example, if one segment goes from (0,0) to (10,0) and another segment
	/// goes from (10,0) to (20,0) they would get combined into a
	/// single edge that goes from (0,0) to (20,0)
	///
	/// WARNING - The implementation assumes a "normalized" <see cref="Edge.StepDirection"/>.
	/// The input can use North or South but not both (same for East/West).
	/// </summary>
	private static List<Edge> CombineEdges(HashSet<Edge> segments)
	{
		var edges = new List<Edge>();
		while (segments.Count > 0)
		{
			var seg = segments.First();
			segments.Remove(seg);

			var start = seg.Start;
			var end = seg.End;
			int totalLength = seg.Length;

			bool done = false;
			while (!done)
			{
				var connection = segments
					.Where(s => s.StepDirection == seg.StepDirection)
					.Where(s => s.Start == end || s.End == start)
					.FirstOrDefault();

				if (connection == null)
				{
					done = true;
					break;
				}

				segments.Remove(connection);
				totalLength += connection.Length;

				if (connection.Start == end)
				{
					end = connection.End;
				}
				else if (connection.End == start)
				{
					start = connection.Start;
				}
				else
				{
					throw new Exception("assert fail");
				}
			}

			edges.Add(new Edge()
			{
				InsideDirection = seg.InsideDirection,
				StepDirection = seg.StepDirection,
				Start = start,
				Length = totalLength,
			});
		}

		return edges;
	}
}