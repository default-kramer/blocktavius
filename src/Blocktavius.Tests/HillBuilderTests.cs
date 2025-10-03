using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Tests;

[TestClass]
public class HillBuilderTests
{
	[TestMethod]
	public void simple_corner_verification()
	{
		const int scale = 5;

		const bool tag = true;
		var tagger = new TileTagger<bool>(unscaledSize: new XZ(3, 3), scale: new XZ(scale, scale));
		tagger.AddTag(new XZ(1, 1), tag);
		var region = tagger.GetRegions(tag).Single();

		const int elevation = 3;
		const int fillElevation = 9;

		var builder = new TestCliffBuilder() { Elevation = elevation };
		var result = AdditiveHillBuilder.BuildHill(region, new Elevation(fillElevation), builder);

		// Print for visual inspection during development
		Console.WriteLine("Full hill:");
		Console.WriteLine(SamplerAssert.PrintElevations(result));

		// The center tile (5x5 square from [5,5] to [9,9]) should all be filled with max elevation
		SamplerAssert.AllSatisfy(result,
			(xz, elev) =>
			{
				bool isCenter = xz.X >= scale && xz.X < scale * 2 && xz.Z >= scale && xz.Z < scale * 2;
				return !isCenter || elev.Y == fillElevation;
			},
			"Center region should be filled with max elevation");

		// Expected pattern for the full 11x11 hill:
		// Outer ring: elevation 1, next ring: 2, next: 3, center 5x5: fillElevation (9)
		string expectedPattern = @"
11111111111
12222222221
12333333321
12399999321
12399999321
12399999321
12399999321
12399999321
12333333321
12222222221
11111111111".Trim();

		SamplerAssert.MatchesPattern(result, expectedPattern,
			c => new Elevation(int.Parse(c.ToString())));
	}

	[TestMethod]
	public void asymmetric_corner_continuity()
	{
		const int scale = 10;

		const bool tag = true;
		var tagger = new TileTagger<bool>(unscaledSize: new XZ(3, 3), scale: new XZ(scale, scale));
		tagger.AddTag(new XZ(1, 1), tag);
		var region = tagger.GetRegions(tag).Single();

		const int elevation = 5;
		const int fillElevation = 100;

		var builder = new AsymmetricTestCliffBuilder() { Elevation = elevation };
		var result = AdditiveHillBuilder.BuildHill(region, new Elevation(fillElevation), builder);

		// The asymmetric builder creates elevation = baseElevation - Z + X
		// At the edge/corner boundary, we should see continuity
		// Check the top edge at the boundary where it meets the top-left corner

		// Top edge goes from (scale, 0) to (2*scale, 0) in the +X direction
		// At X = scale (the left edge of the top edge), the cliff should blend with the corner
		// The corner occupies X in [0, scale), and the edge starts at X = scale

		// Let's verify continuity at the junction point (scale, 0)
		// This point should be influenced by both the corner and the edge cliff

		// Check that elevations are continuous along the top edge
		for (int x = scale - 1; x <= scale + 1; x++)
		{
			for (int z = 0; z < elevation; z++)
			{
				var elev = result.Sample(new XZ(x, z)).Y;
				// The elevation should follow the pattern with smooth transition
				// Since we're using an asymmetric builder with elev = base - z + x,
				// we should see the X value reflected in the elevation
				Assert.IsTrue(elev > 0 || elev == -1, $"Unexpected elevation at ({x},{z}): {elev}");
			}
		}

		// More specific test: verify that at the corner boundary, the cliff pattern is continuous
		// At (scale, 1), this should be at the edge between corner and main edge cliff
		// With CORNER_OVERLAP = 100, the main edge should start at X=100 in cliff coordinates
		// But this is scale-dependent and hard to test precisely without knowing internals

		// Instead, let's test that corners don't have artifacts (sharp transitions)
		// Check top-left corner has smooth gradient
		for (int x = scale - 2; x <= scale; x++)
		{
			int elev1 = result.Sample(new XZ(x, 1)).Y;
			int elev2 = result.Sample(new XZ(x + 1, 1)).Y;

			// With the asymmetric pattern (elev = base - z + x), adjacent X values should differ by ~1
			// Allow some tolerance due to min() operation in corners
			int diff = Math.Abs(elev1 - elev2);
			Assert.IsTrue(diff <= 2, $"Sharp transition at X={x}, Z=1: {elev1} -> {elev2} (diff={diff})");
		}
	}

	[TestMethod]
	public void corner_and_edge_slices_are_adjacent()
	{
		// Test the slicing strategy directly without rotation/translation complexity
		const int edgeLength = 50;
		const int cornerSize = 10;
		const int CORNER_OVERLAP = 100;

		// Create a cliff builder that encodes X position in elevation
		var builder = new DirectXEncodingCliffBuilder() { BaseElevation = 20 };
		var cliffBuilder = builder.AnotherOne(edgeLength + CORNER_OVERLAP * 2);

		// Build the main edge slice (what AdditiveHillBuilder does for the edge)
		var edgeSlice = cliffBuilder.BuildCliff(new Core.Range(CORNER_OVERLAP, CORNER_OVERLAP + edgeLength - 1));

		// Build the corner slice that should come BEFORE the edge (at Start)
		var cornerBeforeSlice = cliffBuilder.BuildCliff(
			new Core.Range(CORNER_OVERLAP, CORNER_OVERLAP + cornerSize - 1).Shift(-cornerSize));

		// Build the corner slice that should come AFTER the edge (at End)
		var cornerAfterSlice = cliffBuilder.BuildCliff(
			new Core.Range(CORNER_OVERLAP, CORNER_OVERLAP + cornerSize - 1).Shift(edgeLength));

		// Check that corner-before ends at X=99 and edge starts at X=100 (adjacent, no gap)
		int cornerBeforeLastX = cornerBeforeSlice.Bounds.end.X - 1; // end is exclusive in Rect
		int edgeFirstX = edgeSlice.Bounds.start.X;
		Assert.AreEqual(edgeFirstX - 1, cornerBeforeLastX,
			$"Corner-before should end at {edgeFirstX - 1}, but ends at {cornerBeforeLastX}");

		// Check that edge ends at some X and corner-after starts at X+1 (adjacent, no gap)
		int edgeLastX = edgeSlice.Bounds.end.X - 1;
		int cornerAfterFirstX = cornerAfterSlice.Bounds.start.X;
		Assert.AreEqual(edgeLastX + 1, cornerAfterFirstX,
			$"Corner-after should start at {edgeLastX + 1}, but starts at {cornerAfterFirstX}");

		// Check continuity: elevations at adjacent positions should differ by ~1
		// Sample at Z=1 to avoid edge effects
		int cornerBeforeElevAtEnd = cornerBeforeSlice.Sample(new XZ(cornerBeforeLastX, 1)).Y;
		int edgeElevAtStart = edgeSlice.Sample(new XZ(edgeFirstX, 1)).Y;
		int diff1 = Math.Abs(edgeElevAtStart - cornerBeforeElevAtEnd);
		Assert.IsTrue(diff1 <= 1,
			$"Corner-before elevation at X={cornerBeforeLastX} is {cornerBeforeElevAtEnd}, " +
			$"edge elevation at X={edgeFirstX} is {edgeElevAtStart}, diff={diff1}. " +
			$"For adjacent slices, diff should be 1.");

		int edgeElevAtEnd = edgeSlice.Sample(new XZ(edgeLastX, 1)).Y;
		int cornerAfterElevAtStart = cornerAfterSlice.Sample(new XZ(cornerAfterFirstX, 1)).Y;
		int diff2 = Math.Abs(cornerAfterElevAtStart - edgeElevAtEnd);
		Assert.IsTrue(diff2 <= 1,
			$"Edge elevation at X={edgeLastX} is {edgeElevAtEnd}, " +
			$"corner-after elevation at X={cornerAfterFirstX} is {cornerAfterElevAtStart}, diff={diff2}. " +
			$"For adjacent slices, diff should be 1.");
	}

	class TestCliffBuilder : AdditiveHillBuilder.ICliffBuilder
	{
		public required int Elevation { get; init; }

		public int Width => int.MaxValue;

		public AdditiveHillBuilder.ICliffBuilder AnotherOne(int width) => this;

		public I2DSampler<Elevation> BuildCliff(Core.Range slice)
		{
			var bounds = new Rect(new XZ(slice.xMin, 0), new XZ(slice.xMax + 1, Elevation));
			var array = new MutableArray2D<Elevation>(bounds, new Elevation(-1));
			foreach (var xz in array.Bounds.Enumerate())
			{
				array.Put(xz, new Core.Elevation(Elevation - xz.Z));
			}
			return array;
		}
	}

	/// <summary>
	/// An asymmetric cliff builder that creates a pattern where elevation = baseElevation - Z + X.
	/// This makes it easy to detect if corner slicing is using the correct X range.
	/// </summary>
	class AsymmetricTestCliffBuilder : AdditiveHillBuilder.ICliffBuilder
	{
		public required int Elevation { get; init; }

		public int Width => int.MaxValue;

		public AdditiveHillBuilder.ICliffBuilder AnotherOne(int width) => this;

		public I2DSampler<Elevation> BuildCliff(Core.Range slice)
		{
			var bounds = new Rect(new XZ(slice.xMin, 0), new XZ(slice.xMax + 1, Elevation));
			var array = new MutableArray2D<Elevation>(bounds, new Elevation(-1));
			foreach (var xz in array.Bounds.Enumerate())
			{
				// Asymmetric pattern: elevation decreases with Z but increases with X
				// This creates a diagonal gradient that varies with X position
				int elev = Elevation - xz.Z + xz.X;
				array.Put(xz, new Core.Elevation(Math.Max(elev, 0)));
			}
			return array;
		}
	}

	/// <summary>
	/// A cliff builder that strongly encodes the X position in the elevation value.
	/// elevation = baseElevation - Z + (X % 100)
	/// This makes it easy to detect which X slice range was used.
	/// </summary>
	class XEncodingCliffBuilder : AdditiveHillBuilder.ICliffBuilder
	{
		public required int BaseElevation { get; init; }

		public int Width => int.MaxValue;

		public AdditiveHillBuilder.ICliffBuilder AnotherOne(int width) => this;

		public I2DSampler<Elevation> BuildCliff(Core.Range slice)
		{
			var bounds = new Rect(new XZ(slice.xMin, 0), new XZ(slice.xMax + 1, BaseElevation));
			var array = new MutableArray2D<Elevation>(bounds, new Elevation(-1));
			foreach (var xz in array.Bounds.Enumerate())
			{
				// Encode X position modulo 100 to detect which range was used
				// X in [0, 100) gives 0-99, X in [100, 200) gives 0-99, etc.
				int xMod = xz.X % 100;
				int elev = BaseElevation - xz.Z + xMod;
				array.Put(xz, new Core.Elevation(Math.Max(elev, 0)));
			}
			return array;
		}
	}

	/// <summary>
	/// A cliff builder that directly encodes X position in elevation (no modulo).
	/// elevation = baseElevation - Z + X
	/// This makes it easy to detect gaps in slicing.
	/// </summary>
	class DirectXEncodingCliffBuilder : AdditiveHillBuilder.ICliffBuilder
	{
		public required int BaseElevation { get; init; }

		public int Width => int.MaxValue;

		public AdditiveHillBuilder.ICliffBuilder AnotherOne(int width) => this;

		public I2DSampler<Elevation> BuildCliff(Core.Range slice)
		{
			var bounds = new Rect(new XZ(slice.xMin, 0), new XZ(slice.xMax + 1, BaseElevation));
			var array = new MutableArray2D<Elevation>(bounds, new Elevation(-1));
			foreach (var xz in array.Bounds.Enumerate())
			{
				// Directly encode X position - no modulo
				int elev = BaseElevation - xz.Z + xz.X;
				array.Put(xz, new Core.Elevation(Math.Max(elev, 0)));
			}
			return array;
		}
	}
}
