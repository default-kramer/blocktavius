using Blocktavius.Core;

namespace Blocktavius.Tests;

[TestClass]
public class TileTaggerTests
{
	[TestMethod]
	public void single_tile_produces_four_edges()
	{
		var tagger = new TileTagger<bool>(unscaledSize: new XZ(3, 3), scale: new XZ(5, 5));
		tagger.AddTag(new XZ(1, 1), true);
		var region = tagger.GetRegions(true).Single();

		Assert.AreEqual(4, region.Edges.Count);
	}

	[TestMethod]
	public void single_tile_edges_are_inclusive()
	{
		var tagger = new TileTagger<bool>(unscaledSize: new XZ(3, 3), scale: new XZ(5, 5));
		tagger.AddTag(new XZ(1, 1), true);
		var region = tagger.GetRegions(true).Single();

		// Region should be (5,5) to (10,10) - a 5x5 square
		Assert.AreEqual(new Rect(new XZ(5, 5), new XZ(10, 10)), region.Bounds);

		// All edges should be inside the region
		foreach (var edge in region.Edges)
		{
			foreach (var point in edge.Walk())
			{
				Assert.IsTrue(region.Contains(point),
					$"Edge point {point} should be inside region bounds {region.Bounds}");
			}
		}
	}

	[TestMethod]
	public void single_tile_edge_properties()
	{
		const int scale = 5;
		var tagger = new TileTagger<bool>(unscaledSize: new XZ(3, 3), scale: new XZ(scale, scale));
		tagger.AddTag(new XZ(1, 1), true);
		var region = tagger.GetRegions(true).Single();

		// Find each edge by its inside direction
		var northEdge = region.Edges.Single(e => e.InsideDirection == CardinalDirection.South);
		var southEdge = region.Edges.Single(e => e.InsideDirection == CardinalDirection.North);
		var westEdge = region.Edges.Single(e => e.InsideDirection == CardinalDirection.East);
		var eastEdge = region.Edges.Single(e => e.InsideDirection == CardinalDirection.West);

		// North edge (top of region, inside=south)
		Assert.AreEqual(new XZ(5, 5), northEdge.Start);
		Assert.AreEqual(new XZ(10, 5), northEdge.End);
		Assert.AreEqual(CardinalDirection.East, northEdge.StepDirection);
		Assert.AreEqual(5, northEdge.Length);

		// South edge (bottom of region, inside=north)
		Assert.AreEqual(new XZ(5, 9), southEdge.Start);
		Assert.AreEqual(new XZ(10, 9), southEdge.End);
		Assert.AreEqual(CardinalDirection.East, southEdge.StepDirection);
		Assert.AreEqual(5, southEdge.Length);

		// West edge (left of region, inside=east)
		Assert.AreEqual(new XZ(5, 5), westEdge.Start);
		Assert.AreEqual(new XZ(5, 10), westEdge.End);
		Assert.AreEqual(CardinalDirection.South, westEdge.StepDirection);
		Assert.AreEqual(5, westEdge.Length);

		// East edge (right of region, inside=west)
		Assert.AreEqual(new XZ(9, 5), eastEdge.Start);
		Assert.AreEqual(new XZ(9, 10), eastEdge.End);
		Assert.AreEqual(CardinalDirection.South, eastEdge.StepDirection);
		Assert.AreEqual(5, eastEdge.Length);
	}

	[TestMethod]
	public void stepping_outside_from_edge_leaves_region()
	{
		var tagger = new TileTagger<bool>(unscaledSize: new XZ(3, 3), scale: new XZ(5, 5));
		tagger.AddTag(new XZ(1, 1), true);
		var region = tagger.GetRegions(true).Single();

		foreach (var edge in region.Edges)
		{
			var oppositeDirection = edge.InsideDirection switch
			{
				CardinalDirection.North => Direction.South,
				CardinalDirection.South => Direction.North,
				CardinalDirection.East => Direction.West,
				CardinalDirection.West => Direction.East,
				_ => throw new Exception("Invalid direction")
			};

			foreach (var point in edge.Walk())
			{
				var outsidePoint = point.Step(oppositeDirection);
				Assert.IsFalse(region.Contains(outsidePoint),
					$"Point {outsidePoint} (one step {oppositeDirection} from edge point {point}) should be outside region");
			}
		}
	}

	[TestMethod]
	public void two_horizontal_tiles_produce_shared_edges()
	{
		var tagger = new TileTagger<bool>(unscaledSize: new XZ(4, 3), scale: new XZ(5, 5));
		tagger.AddTag(new XZ(1, 1), true);
		tagger.AddTag(new XZ(2, 1), true);
		var region = tagger.GetRegions(true).Single();

		// Should have 4 edges (shared internal edge gets combined)
		Assert.AreEqual(4, region.Edges.Count);

		// North and south edges should span both tiles
		var northEdge = region.Edges.Single(e => e.InsideDirection == CardinalDirection.South);
		var southEdge = region.Edges.Single(e => e.InsideDirection == CardinalDirection.North);

		Assert.AreEqual(10, northEdge.Length, "North edge should span 2 tiles");
		Assert.AreEqual(10, southEdge.Length, "South edge should span 2 tiles");
	}

	[TestMethod]
	public void two_vertical_tiles_produce_shared_edges()
	{
		var tagger = new TileTagger<bool>(unscaledSize: new XZ(3, 4), scale: new XZ(5, 5));
		tagger.AddTag(new XZ(1, 1), true);
		tagger.AddTag(new XZ(1, 2), true);
		var region = tagger.GetRegions(true).Single();

		// Should have 4 edges (shared internal edge gets combined)
		Assert.AreEqual(4, region.Edges.Count);

		// West and east edges should span both tiles
		var westEdge = region.Edges.Single(e => e.InsideDirection == CardinalDirection.East);
		var eastEdge = region.Edges.Single(e => e.InsideDirection == CardinalDirection.West);

		Assert.AreEqual(10, westEdge.Length, "West edge should span 2 tiles");
		Assert.AreEqual(10, eastEdge.Length, "East edge should span 2 tiles");
	}

	[TestMethod]
	public void l_shape_produces_correct_edge_count()
	{
		var tagger = new TileTagger<bool>(unscaledSize: new XZ(4, 4), scale: new XZ(5, 5));
		// Create an L shape:
		//  X
		//  XX
		tagger.AddTag(new XZ(1, 1), true);
		tagger.AddTag(new XZ(1, 2), true);
		tagger.AddTag(new XZ(2, 2), true);
		var region = tagger.GetRegions(true).Single();

		// L shape has 10 edge segments that combine into 6 edges
		// (shared edges along west and south get combined)
		Assert.AreEqual(6, region.Edges.Count);

		// Verify we have both inside and outside corners
		var corners = region.ComputeCorners();
		var outsideCorners = corners.Where(c => c.CornerType == CornerType.Outside).ToList();
		var insideCorners = corners.Where(c => c.CornerType == CornerType.Inside).ToList();

		Assert.IsTrue(outsideCorners.Count > 0, "L-shape should have outside corners");
		Assert.IsTrue(insideCorners.Count > 0, "L-shape should have inside corners");
	}

	[TestMethod]
	public void edge_min_max_properties()
	{
		var tagger = new TileTagger<bool>(unscaledSize: new XZ(3, 3), scale: new XZ(5, 5));
		tagger.AddTag(new XZ(1, 1), true);
		var region = tagger.GetRegions(true).Single();

		foreach (var edge in region.Edges)
		{
			// Min should be the smaller coordinate
			Assert.IsTrue(edge.Min.X <= edge.Max.X);
			Assert.IsTrue(edge.Min.Z <= edge.Max.Z);

			// End should be one step beyond Max (exclusive)
			if (edge.StepDirection == CardinalDirection.East || edge.StepDirection == CardinalDirection.West)
			{
				Assert.AreEqual(edge.Max.X + 1, edge.End.X);
			}
			else
			{
				Assert.AreEqual(edge.Max.Z + 1, edge.End.Z);
			}

			// Length should match the distance
			if (edge.StepDirection == CardinalDirection.East || edge.StepDirection == CardinalDirection.West)
			{
				Assert.AreEqual(edge.Length, edge.End.X - edge.Start.X);
			}
			else
			{
				Assert.AreEqual(edge.Length, edge.End.Z - edge.Start.Z);
			}
		}
	}

	[TestMethod]
	public void multiple_separate_regions()
	{
		var tagger = new TileTagger<bool>(unscaledSize: new XZ(5, 5), scale: new XZ(3, 3));
		// Create two separate regions:
		// X...X
		// .....
		// .....
		tagger.AddTag(new XZ(0, 0), true);
		tagger.AddTag(new XZ(4, 0), true);

		var regions = tagger.GetRegions(true);
		Assert.AreEqual(2, regions.Count);

		// Each region should be a single tile with 4 edges
		foreach (var region in regions)
		{
			Assert.AreEqual(4, region.Edges.Count);
		}
	}

	[TestMethod]
	public void edge_walk_covers_all_points()
	{
		var tagger = new TileTagger<bool>(unscaledSize: new XZ(3, 3), scale: new XZ(7, 7));
		tagger.AddTag(new XZ(1, 1), true);
		var region = tagger.GetRegions(true).Single();

		foreach (var edge in region.Edges)
		{
			var points = edge.Walk().ToList();

			// Should have exactly Length points
			Assert.AreEqual(edge.Length, points.Count);

			// First point should be Start
			Assert.AreEqual(edge.Start, points[0]);

			// Last point should be Max (one before End)
			Assert.AreEqual(edge.Max, points[^1]);

			// All points should be consecutive
			for (int i = 1; i < points.Count; i++)
			{
				var step = points[i].Subtract(points[i - 1]);
				var expectedStep = Direction.Parse(edge.StepDirection).Step;
				Assert.AreEqual(expectedStep, step);
			}
		}
	}

	[TestMethod]
	public void l_shape_inside_corner_gap_analysis()
	{
		var tagger = new TileTagger<bool>(unscaledSize: new XZ(4, 4), scale: new XZ(5, 5));
		// Create an L shape:
		//  X
		//  XX
		tagger.AddTag(new XZ(1, 1), true);
		tagger.AddTag(new XZ(1, 2), true);
		tagger.AddTag(new XZ(2, 2), true);
		var region = tagger.GetRegions(true).Single();

		// Find the edges that meet at the inside corner
		// The inside corner is where tile (2,2) meets tiles (1,1) and (1,2)
		// This happens at the original meeting point (10,10)

		// East edge of top tile (inside=West): should end near (10,10)
		var eastEdge = region.Edges.Where(e => e.InsideDirection == CardinalDirection.West && e.Start.Z == 5).Single();

		// North edge of bottom-right tile (inside=South): should start near (10,10)
		var northEdge = region.Edges.Where(e => e.InsideDirection == CardinalDirection.South && e.Start.Z == 10).Single();

		Console.WriteLine($"East edge: Start={eastEdge.Start}, Max={eastEdge.Max}, End={eastEdge.End}");
		Console.WriteLine($"North edge: Start={northEdge.Start}, Max={northEdge.Max}, End={northEdge.End}");

		// The issue: East edge Max should connect to North edge Start, but there's a gap
		// East edge Max: (9,9)
		// North edge Start: (10,10)
		Assert.AreEqual(new XZ(9, 9), eastEdge.Max);
		Assert.AreEqual(new XZ(10, 10), northEdge.Start);

		// There's a 1-unit diagonal gap between them!
		// This is the fundamental problem with inclusive edges at inside corners
		var gap = northEdge.Start.Subtract(eastEdge.Max);
		Console.WriteLine($"Gap between edges: {gap}");
		Assert.AreEqual(new XZ(1, 1), gap); // Diagonal gap of (1,1)
	}
}
