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
	private IReadOnlyList<XZ>? __coords = null;
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

	/// <summary>
	/// Returns the runs as a list of XZ coordinates starting from X=0
	/// and using LaneOffset as the Z coordinate.
	/// </summary>
	public IReadOnlyList<XZ> Coords
	{
		get
		{
			__coords = __coords ?? BuildCoords(runs, TotalLength);
			return __coords;
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
		var coords = this.Coords;

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

		// Calculate original run positions for overlap constraint checking
		var originalPositions = new List<(int start, int end)>();
		int x = 0;
		foreach (var run in this.runs)
		{
			originalPositions.Add((x, x + run.length));
			x += run.length;
		}

		// Generate new run lengths first, adjusting to maintain total length
		var newLengths = new int[this.runs.Count];

		// Start with small random adjustments to original lengths
		for (int i = 0; i < this.runs.Count; i++)
		{
			var origLength = this.runs[i].length;
			int minAdjustment = Math.Max(minRunLength - origLength, -2);
			int maxAdjustment = Math.Min(maxRunLength - origLength, 2);

			if (minAdjustment <= maxAdjustment)
			{
				int adjustment = prng.NextInt32(minAdjustment, maxAdjustment + 1);
				newLengths[i] = origLength + adjustment;
			}
			else
			{
				newLengths[i] = Math.Max(minRunLength, Math.Min(maxRunLength, origLength));
			}
		}

		// Adjust to maintain exact total length
		int totalDiff = newLengths.Sum() - this.TotalLength;
		while (totalDiff != 0 && this.runs.Count > 1)
		{
			// Find two runs we can adjust to balance the difference
			int run1 = prng.NextInt32(this.runs.Count);
			int run2 = prng.NextInt32(this.runs.Count);
			if (run1 == run2) continue;

			if (totalDiff > 0) // need to reduce total
			{
				int maxReduce = Math.Min(totalDiff, newLengths[run1] - minRunLength);
				if (maxReduce > 0)
				{
					newLengths[run1] -= maxReduce;
					totalDiff -= maxReduce;
				}
			}
			else // need to increase total
			{
				int maxIncrease = Math.Min(-totalDiff, maxRunLength - newLengths[run2]);
				if (maxIncrease > 0)
				{
					newLengths[run2] += maxIncrease;
					totalDiff += maxIncrease;
				}
			}
		}

		// Now determine new positions that maintain adjacency (no gaps)
		// while trying to overlap with original positions where possible
		var newPositions = new List<(int start, int end)>();
		int currentPos = 0;

		for (int i = 0; i < this.runs.Count; i++)
		{
			var origStart = originalPositions[i].start;
			var origEnd = originalPositions[i].end;
			int newLength = newLengths[i];

			// Calculate ideal start position to maximize overlap with original
			int idealStart = Math.Max(0, Math.Min(origEnd - newLength, origStart));

			// But we must start at or after currentPos to maintain adjacency
			int actualStart = Math.Max(currentPos, idealStart);

			// Check if we can shift slightly to get better overlap
			if (actualStart > origEnd - 1 && actualStart > currentPos)
			{
				// No overlap possible, but we can try to get closer
				int shiftAmount = Math.Min(actualStart - currentPos, actualStart - (origEnd - newLength));
				if (shiftAmount > 0 && prng.NextInt32(2) == 0)
				{
					actualStart = Math.Max(currentPos, actualStart - shiftAmount);
				}
			}

			newPositions.Add((actualStart, actualStart + newLength));
			currentPos = actualStart + newLength;
		}

		// Verify total length is correct
		if (currentPos != this.TotalLength)
		{
			// Fallback to simpler approach
			return FallbackShift(prng, minRunLength, maxRunLength);
		}

		// Create new runs
		var newRuns = new List<Run>(this.runs.Count);
		for (int i = 0; i < this.runs.Count; i++)
		{
			int length = newPositions[i].end - newPositions[i].start;
			newRuns.Add(new Run(newPositions[i].start, length, this.runs[i].laneOffset));
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
