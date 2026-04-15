using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core.Generators.Hills;

// Do the overhang (remember the inverted hill trick)
// Fencepost shifter for middle section??
// Sensible splitting

public static class FacileCliffBuilder
{
	public sealed record Config
	{
		public required int BaseHeight { get; init; }
	}

	public sealed class Result
	{

	}

	public static I2DSampler<int> TODO(Jaunt jaunt, Config config)
	{
		return GenerateBase(jaunt, config);
	}

	private static I2DSampler<int> GenerateBase(Jaunt jaunt, Config config)
	{
		int xStart = jaunt.Runs[0].start;
		int xEnd = jaunt.Runs.Last().end;
		int zStart = jaunt.Runs.Select(r => r.laneOffset).Min();
		int zEnd = jaunt.Runs.Select(r => r.laneOffset).Max() + 1;
		var bounds = new Rect(new XZ(xStart, zStart), new XZ(xEnd, zEnd));
		var array = new MutableArray2D<int>(bounds, -1);

		foreach (var run in jaunt.Runs)
		{
			for (int x = run.start; x < run.end; x++)
			{
				for (int z = run.laneOffset; z >= zStart; z--)
				{
					array.Put(new XZ(x, z), config.BaseHeight);
				}
			}
		}

		// Could probably use Fencepost Shifting here... but let's keep it very simple for now:
		var gaps = FindGaps(jaunt).OrderBy(x => x.LaneOffset).ToList();
		foreach (var gap in gaps)
		{
			var xs = gap.Range.xValues.ToList();
			foreach (var x in xs)
			{
				var backstop = array.Sample(new XZ(x, gap.LaneOffset - 1));
				array.Put(new XZ(x, gap.LaneOffset), backstop - 1);
			}
		}

		return array;
	}

	sealed record Gap
	{
		public required Jaunt.Run? LeftBookend { get; init; }
		public required Jaunt.Run? RightBookend { get; init; }
		public required Range Range { get; init; }
		public required int LaneOffset { get; init; }
	}

	private static IEnumerable<Gap> FindGaps(Jaunt jaunt)
	{
		int xMin = jaunt.Runs[0].start;
		Stack<Jaunt.Run> stack = new();

		foreach (var run in jaunt.Runs)
		{
			// Any run deeper than the current run (there should be at most 1) is unpaired
			while (stack.TryPeek(out var unpaired) && unpaired.laneOffset < run.laneOffset)
			{
				stack.Pop();
			}

			Gap? gap;
			if (stack.TryPeek(out var start))
			{
				if (start.laneOffset == run.laneOffset)
				{
					// start+end pair
					gap = new Gap
					{
						LeftBookend = start,
						RightBookend = run,
						LaneOffset = run.laneOffset,
						Range = new Range(start.end, run.start - 1), // TODO?
					};
					stack.Pop();
				}
				else
				{
					gap = null;
				}
			}
			else
			{
				// open to the left
				gap = new Gap
				{
					LeftBookend = null,
					RightBookend = run,
					LaneOffset = run.laneOffset,
					Range = new Range(xMin, run.start - 1), // TODO?
				};
			}

			// no matter what, this run could still pair to the right also
			stack.Push(run);

			if (gap != null && gap.Range.Width > 0)
			{
				yield return gap;
			}
		}

		// Resolve runs which are open to the right
		int xMax = jaunt.Runs.Last().end - 1;
		foreach (var run in stack)
		{
			var range = new Range(run.end, xMax);
			if (range.Width > 0)
			{
				yield return new Gap
				{
					LeftBookend = run,
					RightBookend = null,
					LaneOffset = run.laneOffset,
					Range = range,
				};
			}
		}
	}
}
