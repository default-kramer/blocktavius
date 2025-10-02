using Blocktavius.Core.Generators.Cliffs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core.Generators.Hills;

public static class QuaintHill
{
	public static I2DSampler<Elevation> BuildQuaintHills(IReadOnlyList<Region> regions, PRNG prng, int maxElevation)
	{
		const int FUDGE = 12; // TODO

		var cliffData = new List<(I2DSampler<QuaintCliff.Item> sampler, I2DSampler<QuaintCliff.Item> biggerSampler, Edge edge)>();

		var allCorners = new List<Corner>();

		foreach (var region in regions)
		{
			allCorners.AddRange(region.ComputeCorners());

			foreach (var edge in region.Edges)
			{
				var bigCliff = QuaintCliff.Generate(prng, edge.Length + FUDGE * 2, maxElevation);
				var cliff = bigCliff.Crop(new Rect(bigCliff.Bounds.start.Add(FUDGE, 0), bigCliff.Bounds.end.Add(-FUDGE, 0)))
					.TranslateTo(XZ.Zero);
				var thickness = cliff.Bounds.Size.Z;

				if (edge.InsideDirection == CardinalDirection.North)
				{
					var translateTo = edge.Start;
					cliff = cliff.Rotate(0).Translate(translateTo);
					bigCliff = bigCliff.Rotate(0).Translate(translateTo.Add(-FUDGE, 0));
				}
				else if (edge.InsideDirection == CardinalDirection.South)
				{
					var translateTo = edge.Start.Add(0, -thickness);
					cliff = cliff.Rotate(180).Translate(translateTo);
					bigCliff = bigCliff.Rotate(180).Translate(translateTo.Add(-FUDGE, 0));
				}
				else if (edge.InsideDirection == CardinalDirection.East)
				{
					var translateTo = edge.Start.Add(-thickness, 0);
					cliff = cliff.Rotate(90).Translate(translateTo);
					bigCliff = bigCliff.Rotate(90).Translate(translateTo.Add(0, -FUDGE));
				}
				else if (edge.InsideDirection == CardinalDirection.West)
				{
					var translateTo = edge.Start;
					cliff = cliff.Rotate(270).Translate(translateTo);
					bigCliff = bigCliff.Rotate(270).Translate(translateTo.Add(0, -FUDGE));
				}

				cliffData.Add((cliff, bigCliff, edge));
			}
		}

		var bounds = Rect.Union(regions.Select(r => r.Bounds), cliffData.Select(c => c.biggerSampler.Bounds));

		var elevations = new MutableArray2D<Elevation>(bounds, new Elevation(-1));

		foreach (var cliff in cliffData.Select(x => x.sampler))
		{
			foreach (var xz in cliff.Bounds.Enumerate())
			{
				// Inside corners will naturally overlap.
				// Using `max` isn't perfect here, but it could be good enough.
				var exist = elevations.Sample(xz).Y;
				var sample = cliff.Sample(xz).y;
				elevations.Put(xz, new Elevation(Math.Max(exist, sample)));
			}
		}

		foreach (var corner in allCorners.Where(c => c.CornerType == CornerType.Outside))
		{
			var cliff1 = cliffData.Single(c => c.edge == corner.EastOrWestEdge).biggerSampler;
			var cliff2 = cliffData.Single(c => c.edge == corner.NorthOrSouthEdge).biggerSampler;

			foreach (var xz in cliff1.Bounds.Enumerate().Intersect(cliff2.Bounds.Enumerate()))
			{
				var sample1 = cliff1.Sample(xz);
				var sample2 = cliff2.Sample(xz);
				elevations.Put(xz, new Elevation(Math.Min(sample1.y, sample2.y)));
			}
		}

		foreach (var region in regions)
		{
			foreach (var xz in region.Bounds.Enumerate())
			{
				if (region.Contains(xz))
				{
					elevations.Put(xz, new Elevation(maxElevation));
				}
			}
		}

		return elevations;
	}
}
