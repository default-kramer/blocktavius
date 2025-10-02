using Blocktavius.Core.Generators;
using Blocktavius.Core.Generators.BasicHill;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

public sealed record JauntSettings
{
	public required int TotalLength { get; init; }
	public required IBoundedRandomValues<int> RunLengthProvider { get; init; }

	/// <summary>
	/// We could actually use this to "steer" the Jaunt e.g. if we want it to
	/// trend north or south near the start... Interesting.
	/// </summary>
	public required IRandomValues<bool> LaneChangeDirectionProvider { get; init; }

	public required int MaxLaneCount { get; init; }
}

/// <summary>
/// A Jaunt is like traveling a straight highway making occasional lane changes.
/// More precisely, a Jaunt is a list of Runs.
/// Each Run has a RunLength and a LaneOffset.
/// The LaneOffset of neighboring Runs always changes by exactly +1 or -1.
///
/// A common convention is to defer any necessary translation/rotation for later and assume that:
/// * The Jaunt starts at X=0 and travels in the positive direction (East).
/// * The LaneOffset of each Run is a non-negative value defining the Z coordinate for that Run.
/// </summary>
public sealed class Jaunt
{
	public readonly record struct Run(int start, int length, int laneOffset)
	{
		public int end => start + length;
	}

	private readonly IReadOnlyList<Run> runs;
	public readonly int TotalLength;
	public int NumRuns => runs.Count;
	public IReadOnlyList<Run> Runs => runs;

	private Jaunt(IReadOnlyList<Run> runs)
	{
		this.runs = runs;
		this.TotalLength = runs.Sum(r => r.length);
		if (runs.Min(x => x.laneOffset) != 0)
		{
			throw new Exception("Assert fail - min lane offset should always be 0");
		}
	}

	public IEnumerable<XZ> ToCoords(XZ start)
	{
		foreach (var run in runs)
		{
			for (int i = 0; i < run.length; i++)
			{
				yield return start.Add(0, run.laneOffset);
				start = start.Add(1, 0);
			}
		}
	}

	public static Jaunt Create(PRNG prng, JauntSettings settings)
	{
		List<Run> runs = new();
		int laneOffset = 0;
		int minLaneOffset = laneOffset;
		int maxLaneOffset = laneOffset;
		int laneCount() => 1 + maxLaneOffset - minLaneOffset;

		int start = 0;
		while (start < settings.TotalLength)
		{
			int runLength = RandomRunLength(prng, settings.TotalLength - start, settings.RunLengthProvider);
			runs.Add(new Run(start, runLength, laneOffset));
			start += runLength;
			if (start == settings.TotalLength)
			{
				break; // We've reached the end, don't calculate next lane offset
			}
			else if (start > settings.TotalLength)
			{
				throw new Exception("Assert fail - final run was too long");
			}

			// adjust laneOffset by +1 or -1 for next run
			int laneShift;
			bool limited = laneCount() == settings.MaxLaneCount;
			if (limited && laneOffset == minLaneOffset)
			{
				laneShift = 1;
			}
			else if (limited && laneOffset == maxLaneOffset)
			{
				laneShift = -1;
			}
			else
			{
				laneShift = settings.LaneChangeDirectionProvider.NextValue(prng) ? -1 : 1;
			}
			laneOffset += laneShift;
			minLaneOffset = Math.Min(minLaneOffset, laneOffset);
			maxLaneOffset = Math.Max(maxLaneOffset, laneOffset);
		}

		// Normalize lane offsets so the min is remapped to 0
		for (int i = 0; i < runs.Count; i++)
		{
			var run = runs[i];
			runs[i] = run with { laneOffset = run.laneOffset - minLaneOffset };
		}

		return new Jaunt(runs);
	}

	private static int RandomRunLength(PRNG prng, int remainingLength, IBoundedRandomValues<int> runLengthRand)
	{
		int runLength = runLengthRand.NextValue(prng);
		int nextRemain = remainingLength - runLength;
		if (nextRemain < 0)
		{
			return remainingLength;
		}
		else if (nextRemain < runLengthRand.MinValue)
		{
			// expand current run to consume all remaining length
			int growOption = remainingLength;
			bool canGrow = growOption <= runLengthRand.MaxValue;

			// shrink current run to allow next run to fit
			int shrinkOption = remainingLength - runLengthRand.MinValue;
			bool canShrink = shrinkOption >= runLengthRand.MinValue;

			if (canGrow && canShrink)
			{
				return prng.RandomChoice(growOption, shrinkOption);
			}
			else if (canGrow)
			{
				return growOption;
			}
			else if (canShrink)
			{
				return shrinkOption;
			}
			else
			{
				// This will violate max run length, but it will never create a run
				// having length > 2*MinRunLength so it shouldn't look too bad unless
				// you use strange values for min+max run lengths.
				// (In fact, I think this violation can only happen when MaxRunLength < MinRunLength*2
				//  which seems unlikely in real applications.)
				return growOption;
			}
		}
		else
		{
			return runLength;
		}
	}

	private static IReadOnlyList<XZ> BuildCoords(IReadOnlyList<Run> runs, int totalLength)
	{
		var coords = GC.AllocateUninitializedArray<XZ>(totalLength);
		int x = 0;
		foreach (var run in runs)
		{
			for (int i = 0; i < run.length; i++)
			{
				coords[x] = new XZ(x, run.laneOffset);
				x++;
			}
		}
		return coords;
	}

	private CornerShifter.Contour ToContour()
	{
		var jaunt = this;
		var coords = this.ToCoords(XZ.Zero).ToList();

		var corners = new List<CornerShifter.Corner>(jaunt.NumRuns - 1);

		int prevZ = coords[0].Z;
		foreach (var coord in coords)
		{
			int dz = coord.Z - prevZ;
			prevZ = coord.Z;
			if (dz == -1)
			{
				corners.Add(new CornerShifter.Corner() { X = coord.X - 1, Dir = Direction.North });
			}
			else if (dz == 1)
			{
				corners.Add(new CornerShifter.Corner() { X = coord.X - 1, Dir = Direction.South });
			}
		}

		if (corners.Count != jaunt.NumRuns - 1)
		{
			throw new Exception("Assert fail!");
		}
		return new CornerShifter.Contour(corners, jaunt.TotalLength);
	}

	public Jaunt NextLayer(PRNG prng)
	{
		var oldContour = this.ToContour();

		// TODO store settings in the Jaunt?
		var newContour = oldContour.Shift(prng, new CornerShifter.Settings()
		{
			CanRelaxMaxRunLength = false,
			CanRelaxMinRunLength = false,
			MaxDepth = 99,
			MaxMatchingDirections = 99,
			MinRunLength = 1,
			MaxRunLength = 99,
			MaxShift = 99,
			Width = this.TotalLength,
		});

		var runs = new List<Run>();
		int prevRunEnd = -1; // if there is a corner at X=0 its run length needs to be 1
		int i = 0;
		const int laneOffsetIncrease = 1;

		foreach (var corner in newContour.Corners)
		{
			int runLength = corner.X - prevRunEnd;
			runs.Add(new Run(prevRunEnd + 1, runLength, this.runs[i].laneOffset));

			prevRunEnd = corner.X;
			i++;
		}

		// NumRuns should be 1+NumCorners
		if (i != this.runs.Count - 1)
		{
			throw new Exception("Assert fail");
		}
		runs.Add(new Run(prevRunEnd + 1, TotalLength - (prevRunEnd + 1), this.runs[i].laneOffset + laneOffsetIncrease));

		var retval = new Jaunt(runs);
		if (runs.Count != this.runs.Count || retval.TotalLength != this.TotalLength)
		{
			throw new Exception("Assert fail");
		}
		return retval;
	}

	internal Jaunt ShiftByFencepost(PRNG prng, FencepostShifter.Settings settings)
	{
		var origPosts = this.runs.Skip(1).Select(r => r.start).ToList();
		var shifter = new FencepostShifter(origPosts, settings);
		var newPosts = shifter.Shift(prng);

		var runs = new List<Run>();
		for (int i = 0; i < this.runs.Count; i++)
		{
			int runStart = i == 0 ? 0 : newPosts[i - 1];
			int runEnd = (i == this.runs.Count - 1) ? settings.TotalLength : newPosts[i];
			runs.Add(new Run()
			{
				laneOffset = this.runs[i].laneOffset,
				start = runStart,
				length = runEnd - runStart,
			});
		}

		return new Jaunt(runs);
	}
}