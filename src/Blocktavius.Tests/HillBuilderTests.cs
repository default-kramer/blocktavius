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
		const int fillElevation = 44;

		var builder = new TestCliffBuilder() { Elevation = elevation };
		var result = AdditiveHillBuilder.BuildHill(region, new Elevation(fillElevation), builder);
		Assert.IsNotNull(result);

		// The center tile should all be filled
		for (int x = scale; x < scale * 2; x++)
		{
			for (int z = scale; z < scale * 2; z++)
			{
				Assert.AreEqual(fillElevation, result.Sample(new XZ(x, z)).Y);
			}
		}

		// check top left corner
		for (int z = 0; z < scale; z++)
		{
			for (int x = 0; x < scale; x++)
			{
				int actual = result.Sample(new XZ(x, z)).Y;

				int distance = Math.Max(4 - x, 4 - z);
				if (distance < elevation)
				{
					Assert.AreEqual(elevation - distance, actual);
				}
				else
				{
					Assert.AreEqual(-1, actual);
				}
			}
		}
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
}
