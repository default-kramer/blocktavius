using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

sealed class Region2
{
	sealed class EdgeInfo
	{
		public required XZ TileMin { get; init; }
		public required XZ TileMax { get; init; }
		public required Direction InsideDirection { get; init; }

		// Mutation! The following are computed after we find all the edges first.
		// We only care about inside corners; outside corners just naturally work.
		public EdgeInfo? InsideCornerMin { get; set; } // The edge with which TileMin forms an inside corner
		public EdgeInfo? InsideCornerMax { get; set; } // The edge with which TileMax forms an inside corner
		public EdgeInfo? OutsideCornerMin { get; set; } // The edge with which TileMin forms an outside corner
		public EdgeInfo? OutsideCornerMax { get; set; } // The edge with which TileMax forms an outside corner
	}

	I2DSampler<bool> tileGrid = null!; // TODO pass it in
	List<EdgeInfo> collectedEdges = new();

	sealed class Check
	{
		public readonly HashSet<XZ> Visited = new();
		public required Direction WalkDir { get; init; }
		public required Direction InsideDir { get; init; }
	}

	public void BuildRegion()
	{
		// First, find all the edges.
		// We walk south and east, which matches the direction of the iteration.
		// So as soon as we find the min (north or west) point of an edge, we immedately
		// walk as far as we can go, build the complete edge, and add all the tiles involved
		// to the hash set so we don't create shorter versions of the same edge in the future.
		var checkW = new Check() { InsideDir = Direction.West, WalkDir = Direction.South };
		var checkE = new Check() { InsideDir = Direction.East, WalkDir = Direction.South };
		var checkN = new Check() { InsideDir = Direction.North, WalkDir = Direction.East };
		var checkS = new Check() { InsideDir = Direction.South, WalkDir = Direction.East };

		foreach (var xz in tileGrid.Bounds.Enumerate())
		{
			DoCheck(checkW, xz);
			DoCheck(checkE, xz);
			DoCheck(checkN, xz);
			DoCheck(checkS, xz);
		}

		// Second, find the inside corners.
		// This method mutates the items in the list we pass here:
		FindCorners(collectedEdges);
	}

	private void DoCheck(Check check, XZ start)
	{
		var (visited, walkDir, insideDir) = (check.Visited, check.WalkDir, check.InsideDir);
		var outsideStep = insideDir.Step.Scale(-1);

		if (visited.Contains(start))
		{
			return;
		}

		var tiles = start.Walk(walkDir, int.MaxValue)
			.TakeWhile(xz => tileGrid.Sample(xz) && !tileGrid.Sample(xz.Add(outsideStep)))
			.ToList();

		if (tiles.Count > 0)
		{
			var edge = new EdgeInfo()
			{
				TileMin = tiles.First(),
				TileMax = tiles.Last(),
				InsideDirection = insideDir,
			};
			collectedEdges.Add(edge);
			foreach (var xz in tiles)
			{
				visited.Add(xz);
			}
		}
	}

	private static void FindCorners(IReadOnlyList<EdgeInfo> edges)
	{
		// We will check each vertical edge, looking for the possible horizontal partners it might have.
		// This means we don't have to check horizontal edges explicitly.
		var horizontalEdges = edges.Where(e => e.InsideDirection.Step.X == 0);
		var verticalEdges = edges.Where(e => e.InsideDirection.Step.Z == 0);

		// The combination of (TileMin, InsideDirection) uniquely identifies an edge.
		// The same is true of (TileMax, InsideDirection).
		var minLookup = horizontalEdges.ToDictionary(e => (e.TileMin, e.InsideDirection));
		var maxLookup = horizontalEdges.ToDictionary(e => (e.TileMax, e.InsideDirection));

		foreach (var vert in verticalEdges)
		{
			var top = vert.TileMin;
			var bottom = vert.TileMax;

			if (vert.InsideDirection == Direction.West)
			{
				// Find inside corners, horizontal edges that start at vert.top or vert.bottom (adjusted)
				if (minLookup.TryGetValue((top.Add(1, -1), Direction.North), out var topRight))
				{
					vert.InsideCornerMin = topRight;
					topRight.InsideCornerMin = vert;
				}
				if (minLookup.TryGetValue((bottom.Add(1, 1), Direction.South), out var bottomRight))
				{
					vert.InsideCornerMax = bottomRight;
					bottomRight.InsideCornerMin = vert;
				}

				// Find outside corners, horizontal edges that start at vert.top or vert.bottom
				if (maxLookup.TryGetValue((top, Direction.South), out var topLeft))
				{
					vert.OutsideCornerMin = topLeft;
					topLeft.OutsideCornerMax = vert;
				}
				if (maxLookup.TryGetValue((bottom, Direction.North), out var bottomLeft))
				{
					vert.OutsideCornerMax = bottomLeft;
					bottomLeft.OutsideCornerMax = vert;
				}
			}
			if (vert.InsideDirection == Direction.East)
			{
				// Find inside corners, horiztonal edges that end at vert.top or vert.bottom (adjusted)
				if (maxLookup.TryGetValue((top.Add(-1, -1), Direction.North), out var topLeft))
				{
					vert.InsideCornerMin = topLeft;
					topLeft.InsideCornerMax = vert;
				}
				if (maxLookup.TryGetValue((bottom.Add(-1, 1), Direction.South), out var bottomLeft))
				{
					vert.InsideCornerMax = bottomLeft;
					bottomLeft.InsideCornerMax = vert;
				}

				// Find outside corners, horizontal edges that start at vert.top or vert.bottom
				if (minLookup.TryGetValue((top, Direction.South), out var topRight))
				{
					vert.OutsideCornerMin = topRight;
					topRight.OutsideCornerMin = vert;
				}
				if (minLookup.TryGetValue((bottom, Direction.North), out var bottomRight))
				{
					vert.OutsideCornerMax = bottomRight;
					bottomRight.OutsideCornerMin = vert;
				}
			}
		}
	}
}
