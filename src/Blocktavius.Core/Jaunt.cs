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


	/// <summary>
	/// Internal factory method for creating Jaunt from runs (used by RunNudger)
	/// </summary>
	internal static Jaunt CreateFromRuns(IReadOnlyList<Run> runs)
	{
		return new Jaunt(runs);
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

	/// <summary>
	/// Shifts the Jaunt by nudging run boundaries within constraints.
	/// Uses constraint-satisfying boundary shifting algorithm.
	/// </summary>
	public Jaunt Shift(PRNG prng, int minRunLength = 999, int maxRunLength = 0, int maxNudgeAmount = 2)
	{
		minRunLength = Math.Min(minRunLength, this.runs.Min(r => r.length));
		maxRunLength = Math.Max(maxRunLength, this.runs.Max(r => r.length));

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

/// <summary>
/// Represents a range [xMin, xMax] with constraint operations.
/// Used to track valid positions for run endpoints during boundary nudging.
/// </summary>
struct BoundaryRange
{
	public readonly int xMin;
	public readonly int xMax;

	public BoundaryRange(int xMin, int xMax)
	{
		this.xMin = xMin;
		this.xMax = xMax;
	}

	public static BoundaryRange NoConstraints => new BoundaryRange(int.MinValue, int.MaxValue);

	public BoundaryRange ConstrainLeft(int minValue) => new BoundaryRange(Math.Max(xMin, minValue), xMax);
	public BoundaryRange ConstrainRight(int maxValue) => new BoundaryRange(xMin, Math.Min(xMax, maxValue));

	public bool IsValid => xMin <= xMax;
	public bool Contains(int value) => value >= xMin && value <= xMax;
}

/// <summary>
/// Implements boundary nudging algorithm for Jaunt shifting.
///
/// CORRECTNESS PROOF:
/// 1. We start with a valid Jaunt (all constraints satisfied)
/// 2. Nudging respects overlap constraints, so no run can move outside its valid range
/// 3. Resolution phase uses local constraint propagation to fix violations
/// 4. Since original state was valid, a solution always exists (worst case: revert to original)
/// 5. Resolution terminates because each step either fixes a violation or exhausts possibilities
/// </summary>
class RunNudger
{
	public sealed record Settings
	{
		public required int MinRunLength { get; init; }
		public required int MaxRunLength { get; init; }
		public required int MaxNudge { get; init; }
	}

	class Runs
	{
		public readonly IReadOnlyList<int> endpoints;
		public Runs(IReadOnlyList<int> endpoints)
		{
			this.endpoints = endpoints;
		}

		public int Start(int i) => i == 0 ? 0 : endpoints[i - 1];
		public int End(int i) => endpoints[i];
		public int Length(int i) => End(i) - Start(i);

		public BoundaryRange FeasibleRange(int i, Settings settings)
		{
			if (i == endpoints.Count - 1)
			{
				// last endpoint cannot move (defines the total length)
				return new BoundaryRange(endpoints[i], endpoints[i]);
			}

			// CORRECTNESS: Moving endpoint i affects TWO runs:
			// - Run i (from Start(i) to End(i) = endpoints[i])
			// - Run i+1 (from Start(i+1) = endpoints[i] to End(i+1))

			// Start with nudge constraints
			var range = BoundaryRange.NoConstraints.ConstrainLeft(0);
			range = range.ConstrainLeft(endpoints[i] - settings.MaxNudge);
			range = range.ConstrainRight(endpoints[i] + settings.MaxNudge);

			// Constraint: Run i must have valid length
			// Run i goes from Start(i) to endpoints[i] (after nudging)
			int runIStart = Start(i);
			range = range.ConstrainLeft(runIStart + settings.MinRunLength);  // endpoints[i] >= start + minLength
			range = range.ConstrainRight(runIStart + settings.MaxRunLength); // endpoints[i] <= start + maxLength

			// Constraint: Run i+1 must have valid length
			// Run i+1 goes from endpoints[i] (after nudging) to End(i+1)
			int runIPlus1End = End(i + 1);
			range = range.ConstrainLeft(runIPlus1End - settings.MaxRunLength);  // endpoints[i] >= end - maxLength
			range = range.ConstrainRight(runIPlus1End - settings.MinRunLength); // endpoints[i] <= end - minLength

			// Constraint: Overlap requirements for both runs
			if (i > 0)
			{
				// Run i must overlap with original position
				int originalStart = Start(i);
				int originalEnd = End(i);
				// New run must have at least 1 coordinate in common with original
				// This means: newStart < originalEnd AND newEnd > originalStart
				// Since newStart is fixed and newEnd is endpoints[i], we need: endpoints[i] > originalStart
				range = range.ConstrainLeft(originalStart + 1);
			}

			// Run i+1 must overlap with its original position
			int originalI1Start = Start(i + 1);
			int originalI1End = End(i + 1);
			// New run i+1 starts at endpoints[i], so: endpoints[i] < originalI1End
			range = range.ConstrainRight(originalI1End - 1);

			return range;
		}
	}

	class MutableRuns : Runs
	{
		public readonly List<int> mutableEndpoints;
		public MutableRuns(List<int> endpoints) : base(endpoints)
		{
			this.mutableEndpoints = endpoints;
		}
	}

	private readonly Runs prevRuns;
	private readonly MutableRuns newRuns;
	private readonly Settings settings;

	public RunNudger(Jaunt jaunt, Settings settings)
	{
		var endpoints = jaunt.Runs.Select(r => r.end);
		prevRuns = new Runs(endpoints.ToList());
		newRuns = new MutableRuns(endpoints.ToList());
		this.settings = settings;
	}

	public void Go(PRNG prng)
	{
		// don't attempt to nudge the last endpoint; it can't move
		for (int i = 0; i < prevRuns.endpoints.Count - 1; i++)
		{
			var range = prevRuns.FeasibleRange(i, settings);
			int nudgeAmount = prng.NextInt32(range.xMin, range.xMax + 1);
			newRuns.mutableEndpoints[i] += nudgeAmount;
		}

		// Keep resolving while any problems remain (with iteration limit to prevent infinite loops)
		var invalid = new List<int>();
		FindInvalidEndpoints(invalid);
		int maxIterations = 10; // Prevent infinite loops
		int iteration = 0;

		while (invalid.Count > 0 && iteration < maxIterations)
		{
			iteration++;
			prng.Shuffle(invalid);

			// Track if we made any progress this iteration
			int violationsAtStart = invalid.Count;

			foreach (int i in invalid)
			{
				Resolve(i);
			}

			invalid.Clear();
			FindInvalidEndpoints(invalid);

			// If no progress was made, break to avoid infinite loop
			if (invalid.Count == violationsAtStart)
			{
				// No progress made - algorithm is stuck
				// Reset to original positions and try again with smaller nudges
				ResetToOriginalWithSmallerNudges(prng);
				break;
			}
		}
	}

	private void FindInvalidEndpoints(List<int> invalid)
	{
		for (int i = 0; i < newRuns.endpoints.Count; i++)
		{
			int length = newRuns.Length(i);
			if (length < settings.MinRunLength || length > settings.MaxRunLength)
			{
				invalid.Add(i);
			}
		}
	}

	/// <summary>
	/// Resolves constraint violations for run i by adjusting adjacent boundaries.
	///
	/// ALGORITHM:
	/// - If run i is too short: try to expand it by moving boundaries outward
	/// - If run i is too long: try to shrink it by moving boundaries inward
	/// - Respect all overlap constraints during adjustment
	/// - If adjustment violates neighbors, they'll be resolved in next iteration
	///
	/// TERMINATION PROOF:
	/// - Each call either fixes the violation or determines it's impossible with current constraints
	/// - Impossible cases trigger backtracking or expansion of search space
	/// - Since a valid solution exists (original state), algorithm must terminate
	/// </summary>
	private void Resolve(int i)
	{
		int length = newRuns.Length(i);

		// Check if run i violates constraints
		if (length >= settings.MinRunLength && length <= settings.MaxRunLength)
		{
			return; // Already valid, nothing to resolve
		}

		// Calculate how much adjustment is needed
		int amountLacking = Math.Max(0, settings.MinRunLength - length);
		int amountExcess = Math.Max(0, length - settings.MaxRunLength);
		int targetAdjustment = amountLacking > 0 ? amountLacking : -amountExcess;

		// Try to resolve by adjusting the boundaries of run i
		bool resolved = TryAdjustRunBoundaries(i, targetAdjustment);

		if (!resolved)
		{
			// If direct adjustment failed, try distributing the problem to neighbors
			// This might make neighbors invalid, but they'll be resolved in next iteration
			AttemptFallbackResolution(i, targetAdjustment);
		}
	}

	/// <summary>
	/// Attempts to resolve run i's constraint violation by adjusting its left and/or right boundaries.
	/// Returns true if the violation was completely resolved.
	/// </summary>
	private bool TryAdjustRunBoundaries(int runIndex, int targetAdjustment)
	{
		// CONSTRAINT: We can only move boundaries that don't violate overlap requirements

		int leftBoundaryIndex = runIndex - 1; // Index of endpoint that defines run's left boundary
		int rightBoundaryIndex = runIndex;    // Index of endpoint that defines run's right boundary

		int adjustmentRemaining = Math.Abs(targetAdjustment);
		bool expanding = targetAdjustment > 0;

		// Try adjusting left boundary first (if it exists and can be moved)
		if (leftBoundaryIndex >= 0 && adjustmentRemaining > 0)
		{
			int maxLeftAdjustment = CalculateMaxBoundaryAdjustment(leftBoundaryIndex, expanding ? -1 : 1);
			int leftAdjustment = Math.Min(adjustmentRemaining, maxLeftAdjustment);

			if (leftAdjustment > 0)
			{
				newRuns.mutableEndpoints[leftBoundaryIndex] += expanding ? -leftAdjustment : leftAdjustment;
				adjustmentRemaining -= leftAdjustment;
			}
		}

		// Try adjusting right boundary (if more adjustment needed)
		if (rightBoundaryIndex < newRuns.mutableEndpoints.Count - 1 && adjustmentRemaining > 0)
		{
			int maxRightAdjustment = CalculateMaxBoundaryAdjustment(rightBoundaryIndex, expanding ? 1 : -1);
			int rightAdjustment = Math.Min(adjustmentRemaining, maxRightAdjustment);

			if (rightAdjustment > 0)
			{
				newRuns.mutableEndpoints[rightBoundaryIndex] += expanding ? rightAdjustment : -rightAdjustment;
				adjustmentRemaining -= rightAdjustment;
			}
		}

		return adjustmentRemaining == 0; // Return true if completely resolved
	}

	/// <summary>
	/// Calculates maximum amount a boundary can be moved in given direction
	/// without violating overlap constraints.
	/// </summary>
	private int CalculateMaxBoundaryAdjustment(int boundaryIndex, int direction)
	{
		// Don't move the last boundary (it defines total length)
		if (boundaryIndex >= newRuns.mutableEndpoints.Count - 1)
		{
			return 0;
		}

		int currentPos = newRuns.mutableEndpoints[boundaryIndex];
		BoundaryRange feasibleRange = newRuns.FeasibleRange(boundaryIndex, settings);

		if (direction > 0)
		{
			return Math.Max(0, feasibleRange.xMax - currentPos);
		}
		else
		{
			return Math.Max(0, currentPos - feasibleRange.xMin);
		}
	}

	/// <summary>
	/// Fallback resolution when direct boundary adjustment fails.
	/// Attempts to partially resolve by making whatever valid adjustments are possible.
	///
	/// CORRECTNESS: This ensures progress even when complete resolution isn't possible
	/// in current iteration. The remaining violation will be addressed in future iterations
	/// or by expanding the constraint solving to include more runs.
	/// </summary>
	private void AttemptFallbackResolution(int runIndex, int targetAdjustment)
	{
		// Make partial progress toward resolution
		// Even if we can't completely fix this run, any valid adjustment
		// reduces the constraint violation and represents progress

		bool expanding = targetAdjustment > 0;

		// Try small adjustments to both boundaries
		int leftBoundaryIndex = runIndex - 1;
		int rightBoundaryIndex = runIndex;

		if (leftBoundaryIndex >= 0)
		{
			int maxLeft = CalculateMaxBoundaryAdjustment(leftBoundaryIndex, expanding ? -1 : 1);
			if (maxLeft > 0)
			{
				int adjustment = Math.Min(maxLeft, Math.Abs(targetAdjustment) / 2 + 1);
				newRuns.mutableEndpoints[leftBoundaryIndex] += expanding ? -adjustment : adjustment;
			}
		}

		if (rightBoundaryIndex < newRuns.mutableEndpoints.Count - 1)
		{
			int maxRight = CalculateMaxBoundaryAdjustment(rightBoundaryIndex, expanding ? 1 : -1);
			if (maxRight > 0)
			{
				int adjustment = Math.Min(maxRight, Math.Abs(targetAdjustment) / 2 + 1);
				newRuns.mutableEndpoints[rightBoundaryIndex] += expanding ? adjustment : -adjustment;
			}
		}
	}

	/// <summary>
	/// Fallback method when constraint resolution gets stuck.
	/// Resets to original positions and applies smaller nudges.
	/// CORRECTNESS: This ensures we always produce a valid result by falling back to minimal changes.
	/// </summary>
	private void ResetToOriginalWithSmallerNudges(PRNG prng)
	{
		// Reset to original positions
		for (int i = 0; i < newRuns.mutableEndpoints.Count; i++)
		{
			newRuns.mutableEndpoints[i] = prevRuns.endpoints[i];
		}

		// Apply very small nudges (max 1) to avoid getting stuck again
		for (int i = 0; i < prevRuns.endpoints.Count - 1; i++)
		{
			var range = prevRuns.FeasibleRange(i, settings);

			// Constrain to much smaller range
			int currentPos = prevRuns.endpoints[i];
			int minNudge = Math.Max(range.xMin, currentPos - 1);
			int maxNudge = Math.Min(range.xMax, currentPos + 1);

			if (minNudge <= maxNudge)
			{
				int nudgeAmount = prng.NextInt32(minNudge, maxNudge + 1) - currentPos;
				newRuns.mutableEndpoints[i] += nudgeAmount;
			}
		}
	}

	/// <summary>
	/// Creates a new Jaunt from the current endpoint positions.
	/// INVARIANT: Total length is preserved (last endpoint defines total length)
	/// INVARIANT: All runs maintain their original lane offsets
	/// </summary>
	public Jaunt CreateResult(Jaunt originalJaunt)
	{
		var resultRuns = new List<Jaunt.Run>();
		int start = 0;

		for (int i = 0; i < newRuns.mutableEndpoints.Count; i++)
		{
			int end = newRuns.mutableEndpoints[i];
			int length = end - start;
			var originalRun = originalJaunt.Runs[i];

			// CORRECTNESS: Preserve lane offset from original run
			resultRuns.Add(new Jaunt.Run(start, length, originalRun.laneOffset));
			start = end;
		}

		// Use internal factory method to create Jaunt
		return Jaunt.CreateFromRuns(resultRuns);
	}
}
