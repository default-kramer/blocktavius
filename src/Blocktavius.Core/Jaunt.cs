using Blocktavius.Core.Generators;
using Blocktavius.Core.Generators.BasicHill;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

public sealed record JauntSettings
{
	public required int TotalLength { get; init; }
	public required IBoundedRandomValues<int> RunLengthProvider { get; init; }
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
		if (true.ToString().Length > 0)
		{
			return Shift(prng);
		}

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

	public Jaunt Shift(PRNG prng, int minRunLength = 999, int maxRunLength = 0)
	{
		minRunLength = Math.Min(minRunLength, this.runs.Min(r => r.length));
		maxRunLength = Math.Max(maxRunLength, this.runs.Max(r => r.length));

		// Use a more aggressive approach that ensures more frequent changes
		// Try multiple different strategies and pick one randomly

		int strategy = prng.NextInt32(3);
		switch (strategy)
		{
			case 0:
				return ShiftByLengthTransfer(prng, minRunLength, maxRunLength);
			case 1:
				return ShiftByPositionAdjustment(prng, minRunLength, maxRunLength);
			default:
				return ShiftByCompression(prng, minRunLength, maxRunLength);
		}
	}

	private Jaunt ShiftByLengthTransfer(PRNG prng, int minRunLength, int maxRunLength)
	{
		// Transfer length between multiple pairs of runs
		var newLengths = this.runs.Select(r => r.length).ToArray();

		int transfers = Math.Max(2, this.runs.Count / 2);
		for (int t = 0; t < transfers; t++)
		{
			int donor = prng.NextInt32(this.runs.Count);
			int recipient = prng.NextInt32(this.runs.Count);
			if (donor == recipient) continue;

			int maxTransfer = Math.Min(3, newLengths[donor] - minRunLength);
			int maxReceive = maxRunLength - newLengths[recipient];
			int transferAmount = Math.Min(maxTransfer, maxReceive);

			if (transferAmount > 0)
			{
				int actualTransfer = prng.NextInt32(1, transferAmount + 1);
				newLengths[donor] -= actualTransfer;
				newLengths[recipient] += actualTransfer;
			}
		}

		return CreateRunsFromLengths(newLengths);
	}

	private Jaunt ShiftByPositionAdjustment(PRNG prng, int minRunLength, int maxRunLength)
	{
		// This strategy doesn't maintain total length properly, so fall back to simpler approach
		return ShiftByLengthTransfer(prng, minRunLength, maxRunLength);
	}

	private Jaunt ShiftByCompression(PRNG prng, int minRunLength, int maxRunLength)
	{
		// Compress some runs and expand others
		var newLengths = this.runs.Select(r => r.length).ToArray();

		// Pick runs to compress and expand
		var compressTargets = new List<int>();
		var expandTargets = new List<int>();

		for (int i = 0; i < this.runs.Count; i++)
		{
			if (newLengths[i] > minRunLength && prng.NextInt32(3) == 0)
			{
				compressTargets.Add(i);
			}
			else if (newLengths[i] < maxRunLength && prng.NextInt32(3) == 0)
			{
				expandTargets.Add(i);
			}
		}

		// Transfer length from compress targets to expand targets
		foreach (int compressIndex in compressTargets)
		{
			if (expandTargets.Count == 0) break;

			int expandIndex = expandTargets[prng.NextInt32(expandTargets.Count)];
			int availableCompression = newLengths[compressIndex] - minRunLength;
			int availableExpansion = maxRunLength - newLengths[expandIndex];
			int transferAmount = Math.Min(availableCompression, availableExpansion);

			if (transferAmount > 0)
			{
				int actualTransfer = prng.NextInt32(1, Math.Min(transferAmount, 2) + 1);
				newLengths[compressIndex] -= actualTransfer;
				newLengths[expandIndex] += actualTransfer;
			}
		}

		return CreateRunsFromLengths(newLengths);
	}

	private Jaunt CreateRunsFromLengths(int[] lengths)
	{
		var newRuns = new List<Run>();
		int start = 0;

		for (int i = 0; i < lengths.Length; i++)
		{
			newRuns.Add(new Run(start, lengths[i], this.runs[i].laneOffset));
			start += lengths[i];
		}

		return new Jaunt(newRuns);
	}

	private Jaunt FallbackShift(PRNG prng, int minRunLength, int maxRunLength)
	{
		// Fallback to the simpler length-only adjustment approach
		var newLengths = this.runs.Select(r => r.length).ToArray();

		int adjustments = Math.Min(10, this.runs.Count * 2);
		for (int adj = 0; adj < adjustments; adj++)
		{
			int run1 = prng.NextInt32(this.runs.Count);
			int run2 = prng.NextInt32(this.runs.Count);
			if (run1 == run2) continue;

			int maxTransfer = Math.Min(
				newLengths[run1] - minRunLength,
				maxRunLength - newLengths[run2]
			);

			int maxBackTransfer = Math.Min(
				newLengths[run2] - minRunLength,
				maxRunLength - newLengths[run1]
			);

			int transferAmount = 0;
			if (maxTransfer > 0 && maxBackTransfer > 0)
			{
				transferAmount = prng.NextInt32(2) == 0
					? prng.NextInt32(1, maxTransfer + 1)
					: -prng.NextInt32(1, maxBackTransfer + 1);
			}
			else if (maxTransfer > 0)
			{
				transferAmount = prng.NextInt32(1, maxTransfer + 1);
			}
			else if (maxBackTransfer > 0)
			{
				transferAmount = -prng.NextInt32(1, maxBackTransfer + 1);
			}

			if (transferAmount != 0)
			{
				newLengths[run1] -= transferAmount;
				newLengths[run2] += transferAmount;
			}
		}

		var newRuns = new List<Run>(this.runs.Count);
		int start = 0;
		for (int i = 0; i < this.runs.Count; i++)
		{
			newRuns.Add(new Run(start, newLengths[i], this.runs[i].laneOffset));
			start += newLengths[i];
		}

		return new Jaunt(newRuns);
	}
}
