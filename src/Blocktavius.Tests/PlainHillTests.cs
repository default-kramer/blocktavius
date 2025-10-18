using Blocktavius.Core;
using Blocktavius.Core.Generators.Hills;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Tests;

[TestClass]
public class PlainHillTests
{
	[TestMethod]
	public void plain_hill_tests()
	{
		const int scale = 5;

		const bool tag = true;
		var tagger = new TileTagger<bool>(unscaledSize: new XZ(3, 3), scale: new XZ(scale, scale));
		tagger.AddTag(new XZ(1, 1), tag);
		var region = tagger.GetRegions(tag, XZ.Zero).Single();

		var settings = new PlainHill.Settings
		{
			MinElevation = 1,
			MaxElevation = 4,
			Steepness = 1,
			CornerType = PlainHill.CornerType.Square,
		};
		var result = PlainHill.BuildPlainHill(region, settings);

		// Print for visual inspection during development
		Console.WriteLine("Full hill:");
		Console.WriteLine(SamplerAssert.PrintElevations(result));

		// This is an additive hill, so the region should be filled with the max elevation
		// and all other spaces should be lower.
		SamplerAssert.AllSatisfy(result,
			(xz, elev) =>
			{
				if (region.Contains(xz)) { return elev == settings.MaxElevation; }
				else { return elev < settings.MaxElevation; }
			},
			"Center region should be filled with max elevation");

		// Choose chars for visual clarity, then replace with numbers
		string expectedPattern = @"
-----------
-ooooooooo-
-o+++++++o-
-o+MMMMM+o-
-o+MMMMM+o-
-o+MMMMM+o-
-o+MMMMM+o-
-o+MMMMM+o-
-o+++++++o-
-ooooooooo-
-----------".Replace("M", "4").Replace("+", "3").Replace("o", "2").Replace("-", "1").Trim();

		SamplerAssert.MatchesPattern(result, expectedPattern, c => int.Parse(c.ToString()));


		// ---------------------------------------
		// Now do another one, with steepness > 1
		// ---------------------------------------
		settings = new PlainHill.Settings
		{
			MinElevation = 1,
			MaxElevation = 8,
			Steepness = 3,
			CornerType = PlainHill.CornerType.Square,
		};

		result = PlainHill.BuildPlainHill(region, settings);

		Console.WriteLine("Full hill 2:");
		Console.WriteLine(SamplerAssert.PrintElevations(result));

		// Choose chars for visual clarity, then replace with numbers
		expectedPattern = @"
ooooooooo
o+++++++o
o+MMMMM+o
o+MMMMM+o
o+MMMMM+o
o+MMMMM+o
o+MMMMM+o
o+++++++o
ooooooooo".Replace("M", "8").Replace("+", "5").Replace("o", "2").Trim();

		SamplerAssert.MatchesPattern(result, expectedPattern, c => int.Parse(c.ToString()));
	}

	[TestMethod]
	public void test_when_min_equals_max()
	{
		const int scale = 5;

		const bool tag = true;
		var tagger = new TileTagger<bool>(unscaledSize: new XZ(3, 3), scale: new XZ(scale, scale));
		tagger.AddTag(new XZ(1, 1), tag);
		var region = tagger.GetRegions(tag, XZ.Zero).Single();

		var start = new XZ(scale, scale);
		var end = new XZ(scale * 2, scale * 2);

		// This decision by the tile tagger is questionable...
		const int YUCK = 1;
		Assert.AreEqual(new Rect(start, end.Add(YUCK, YUCK)), region.Bounds,
			"If this assertion fails, the Tile Tagger has likely been improved! You can now remove the workaround in the Additive Hill!");

		// The additive hill works around the tile tagger's decision.
		// This isn't very visible unless minElevation=maxElevation (which is what I wanted to test anyway).
		const int elevation = 42;
		var settings = new PlainHill.Settings
		{
			MinElevation = elevation,
			MaxElevation = elevation,
			Steepness = 17, // won't matter
			CornerType = PlainHill.CornerType.Square,
		};

		var result = PlainHill.BuildPlainHill(region, settings);

		Console.WriteLine("Full hill:");
		Console.WriteLine(SamplerAssert.PrintElevations(result));

		Assert.AreEqual(new Rect(start, end), result.Bounds);
		SamplerAssert.AllSatisfy(result, (xz, elev) => elev == elevation);
	}
}
