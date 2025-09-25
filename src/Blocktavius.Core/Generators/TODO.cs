using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core.Generators;

internal static class TODO
{
	public static I2DSampler<Elevation> BuildHill(Region region, int elevation, PRNG prng)
	{
		if (elevation < 1)
		{
			// or we could return an empty sampler...
			throw new ArgumentOutOfRangeException(nameof(elevation));
		}

		int expansion = 0;// SUBTRACTIVE! Math.Max(elevation, 4) / 2;
		var bounds = new Rect(region.Bounds.start.Add(-expansion, -expansion), region.Bounds.end.Add(expansion, expansion));

		var edgesNS = region.Edges.Where(e => e.InsideDirection == CardinalDirection.North || e.InsideDirection == CardinalDirection.South).ToList();
		var edgesEW = region.Edges.Where(e => e.InsideDirection == CardinalDirection.East || e.InsideDirection == CardinalDirection.West).ToList();

		var compressedRange = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

		var subtractorNS = BuildSubtractor(bounds, edgesNS, prng);
		var translatorNS = TranslateHistogram(subtractorNS, compressedRange);
		var subtractorEW = BuildSubtractor(bounds, edgesEW, prng);
		var translatorEW = TranslateHistogram(subtractorEW, compressedRange);

		var finalResult = new MutableArray2D<Elevation>(bounds, new Elevation(-1));
		foreach (var xz in bounds.Enumerate())
		{
			if (region.Contains(xz))
			{
				var sampleNS = subtractorNS.Sampler.Sample(xz);
				if (sampleNS > 0)
				{
					sampleNS = translatorNS[sampleNS];
				}
				var sampleEW = subtractorEW.Sampler.Sample(xz);
				if (sampleEW > 0)
				{
					sampleEW = translatorEW[sampleEW];
				}

				int subtraction = Math.Max(sampleNS, sampleEW);
				finalResult[xz] = new Elevation(elevation - subtraction);
			}
		}

		return finalResult;
	}

	sealed record Subtractor
	{
		/// <summary>
		/// Holds a positive value to be subtracted from the final hill,
		/// or zero to indicate nothing.
		/// </summary>
		public required I2DSampler<int> Sampler { get; init; }

		public required int MaxValue { get; init; }

		/// <summary>
		/// All the places where <see cref="Sampler"/> has a non-zero value.
		/// </summary>
		public required IReadOnlySet<XZ> Population { get; init; }

		public IReadOnlyList<int> BuildHistogram()
		{
			var histogram = new int[MaxValue + 1];
			histogram.AsSpan().Fill(0);
			foreach (var xz in Population)
			{
				var sample = Sampler.Sample(xz);
				histogram[sample]++;
			}

			if (histogram[0] != 0)
			{
				throw new Exception("assert fail");
			}

			return histogram;
		}
	}

	private static Subtractor BuildSubtractor(Rect bounds, IReadOnlyList<Edge> edges, PRNG prng)
	{
		var sampler = new MutableArray2D<int>(bounds, 0);

		const int iterationsPerEdge = 30;
		int maxVal = 0;

		HashSet<XZ> population = new();

		foreach (var edge in edges)
		{
			var stepDir = Direction.Parse(edge.StepDirection);
			var insideDir = Direction.Parse(edge.InsideDirection);

			for (int iteration = 0; iteration < iterationsPerEdge; iteration++)
			{
				var runStart = edge.Start;
				while (runStart != edge.End)
				{
					var (runEnd, runLength) = GetRandomRun(stepDir, runStart, edge.End, prng);
					var ledges = GetRandomLedges(prng);

					XZ stepper = runStart;
					for (int i = 0; i < runLength; i++)
					{
						XZ temp = stepper;
						foreach (var ledge in ledges)
						{
							int val = sampler[temp] + ledge;
							maxVal = Math.Max(val, maxVal);
							sampler.Put(temp, val);
							population.Add(temp);

							temp = temp.Step(insideDir);
						}

						stepper = stepper.Step(stepDir);
					}

					if (stepper != runEnd)
					{
						throw new Exception("Assert fail here");
					}
					runStart = stepper;
				}
			}
		}

		return new Subtractor()
		{
			Sampler = sampler,
			MaxValue = maxVal,
			Population = population,
		};
	}

	private static (XZ end, int length) GetRandomRun(Direction stepDir, XZ start, XZ limit, PRNG prng)
	{
		int wantLength = prng.RandomChoice(2, 3, 4, 4, 5, 5, 6, 7);

		XZ end = start;
		int actualLength = 0;
		while (wantLength > 0 && end != limit)
		{
			end = end.Step(stepDir);
			actualLength++;
			wantLength--;
		}

		return (end, actualLength);
	}

	private static int[] GetRandomLedges(PRNG prng)
	{
		var ledgeCount = prng.RandomChoice(1, 2, 2, 3, 3, 4);
		var ledges = new int[ledgeCount];

		int subtraction = 0;
		for (int i = ledgeCount - 1; i >= 0; i--)
		{
			subtraction += prng.RandomChoice(1, 2, 2, 3);
			ledges[i] = subtraction;
		}

		return ledges;
	}

	/// <summary>
	/// Returns a list the same size as the given <paramref name="histogram"/>
	/// such that (for example) if the <paramref name="desiredValues"/> has 5 elements,
	/// the bottom 20% of the histogram will be assigned to `desiredValues[0]`
	/// and the next 20% will be assigned to `desiredValues[1]` and so on.
	/// </summary>
	private static IReadOnlyList<int> TranslateHistogram(Subtractor subtractor, IReadOnlyList<int> desiredValues)
	{
		decimal populationPerBand = (decimal)subtractor.Population.Count / (decimal)desiredValues.Count;
		var histogram = subtractor.BuildHistogram();
		if (histogram.Sum() != subtractor.Population.Count)
		{
			throw new Exception("Assert fail");
		}

		var translation = new int[histogram.Count];

		(int index, decimal popTarget) currentBand = (0, populationPerBand);

		int popAccum = 0;
		for (int i = 0; i < translation.Length; i++)
		{
			popAccum += histogram[i];

			if (popAccum > currentBand.popTarget && currentBand.index < desiredValues.Count - 1)
			{
				int nextIndex = currentBand.index + 1;
				currentBand = (nextIndex, populationPerBand * (nextIndex + 1));
			}

			translation[i] = desiredValues[currentBand.index];
		}

		return translation;
	}

	public static I2DSampler<Elevation> SimpleHill(Region region, int elevation, PRNG prng, int thickness, int steepness)
	{
		var sampler = new MutableArray2D<Elevation>(region.Bounds, new Elevation(-1));

		foreach (var xz in region.Bounds.Enumerate())
		{
			if (region.Contains(xz))
			{
				sampler[xz] = new Elevation(elevation);
			}
		}

		foreach (var edge in region.Edges)
		{
			var start = edge.Start;
			var insideDir = Direction.Parse(edge.InsideDirection);
			var stepDir = Direction.Parse(edge.StepDirection);

			// TODO edges are defined questionably...
			if (edge.InsideDirection == CardinalDirection.North)
			{
				start = start.Step(Direction.North);
			}
			if (edge.InsideDirection == CardinalDirection.West)
			{
				start = start.Step(Direction.West);
			}

			foreach (var anchor in start.Walk(stepDir, edge.Length))
			{
				XZ curr = anchor;
				int thick = thickness;
				while (thick > 0)
				{
					int drop = elevation - thick * steepness;
					int existing = sampler[curr].Y;
					sampler[curr] = new Elevation(Math.Min(drop, existing));

					thick--;
					curr = curr.Step(insideDir);
				}
			}
		}

		return sampler;
	}

	public static I2DSampler<Elevation> OtherHill(Region region, int elevation, PRNG prng)
	{
		var subtractor = OtherHill2(region, elevation, prng);

		var sampler = new MutableArray2D<Elevation>(region.Bounds, new Elevation(-1));
		var compressedRange = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };//, 11, 12, 13, 14, 15 };
		var translation = TranslateHistogram(subtractor, compressedRange);

		foreach (var xz in region.Bounds.Enumerate())
		{
			if (region.Contains(xz))
			{
				int sub = subtractor.Sampler.Sample(xz);
				sub = translation[sub];
				sampler.Put(xz, new Elevation(elevation - sub));
			}
		}

		return sampler;
	}

	private static Subtractor OtherHill2(Region region, int elevation, PRNG prng)
	{
		var edgeCoords = region.Edges.SelectMany(edge => edge.Walk().Select(xz => (edge, xz)))
			.DistinctBy(x => x.xz)
			.OrderBy(x => Guid.NewGuid())
			.ToList();

		int activePoolSize = edgeCoords.Count / 5 + 2;
		int standbyQueueSize = edgeCoords.Count - activePoolSize;
		var standbyQueue = new Queue<int>(Enumerable.Range(activePoolSize, standbyQueueSize));

		var subtraction = new MutableArray2D<int>(region.Bounds, 0);
		int max = 0;
		HashSet<XZ> population = new();

		int counter = edgeCoords.Count * 20;
		while (counter-- > 0)
		{
			int index = prng.NextInt32(activePoolSize);
			var target = edgeCoords[index];

			Bomb(subtraction, target, prng, ref max, population);

			int swapIndex = standbyQueue.Dequeue();
			edgeCoords[index] = edgeCoords[swapIndex];
			edgeCoords[swapIndex] = target;
			standbyQueue.Enqueue(swapIndex);
		}

		return new Subtractor()
		{
			Sampler = subtraction,
			MaxValue = max,
			Population = population,
		};
	}

	private static void Bomb(MutableArray2D<int> sampler, (Edge edge, XZ xz) target, PRNG prng, ref int max, HashSet<XZ> population)
	{
		var insideDir = Direction.Parse(target.edge.InsideDirection);
		var stepDir = Direction.Parse(target.edge.StepDirection);

		int stepsIn = prng.RandomChoice(0, 0, 0, 1, 1, 2);
		int length = prng.RandomChoice(1, 1, 2, 2, 3, 4);

		var range = Enumerable.Range(0, length + 1);
		var steps = range.Concat(range.Skip(1).Select(x => -x)).ToList(); // length==2 should produce [0, 1, 2, -1, 2]

		foreach (int i in steps)
		{
			var anchor = target.xz.Step(stepDir, i);
			if (sampler.Bounds.Contains(anchor))
			{
				foreach (var xz in anchor.Walk(insideDir, stepsIn + 1))
				{
					int val = sampler[xz] + 1;
					max = Math.Max(max, val);
					sampler.Put(xz, val);
					population.Add(xz);
				}
			}
		}
	}
}
