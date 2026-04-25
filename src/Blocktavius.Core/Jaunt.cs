using Blocktavius.Core.Generators;
using Blocktavius.Core.Generators.BasicHill;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Blocktavius.Core.Generators.Hills.FacileCliffBuilder;

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

	private Jaunt(IReadOnlyList<Run> runs, bool validateZero)
	{
		this.runs = runs;
		this.TotalLength = runs.Sum(r => r.length);
		if (validateZero && runs.Min(x => x.laneOffset) != 0)
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

		return new Jaunt(runs, validateZero: true);
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

	internal Jaunt ShiftByFencepost(PRNG prng, FencepostShifter.Settings settings)
	{
		var origPosts = this.runs.Skip(1).Select(r => r.start).ToList();
		var shifter = FencepostShifter.Create(origPosts, settings);
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

		return new Jaunt(runs, validateZero: true);
	}

	public static bool TryParse(I2DSampler<bool> sampler, CardinalDirection outsideDirection, out PositionedJaunt positionedJaunt)
	{
		int rotation = outsideDirection switch
		{
			CardinalDirection.South => 0,
			CardinalDirection.East => 90,
			CardinalDirection.North => 180,
			CardinalDirection.West => 270,
			_ => throw new ArgumentException(nameof(outsideDirection)),
		};

		if (Jaunt.TryParse(sampler.Rotate(rotation), out var jaunt))
		{
			positionedJaunt = new PositionedJaunt
			{
				Bounds = sampler.Bounds,
				Jaunt = jaunt,
				Rotation = (360 - rotation) % 360,
				OutsideDirection = outsideDirection,
			};
			return true;
		}
		positionedJaunt = null!;
		return false;
	}

	/// <summary>
	/// Assumes the Jaunt runs from West to East (left to right).
	/// </summary>
	public static bool TryParse(I2DSampler<bool> sampler, out Jaunt jaunt)
	{
		int startX = sampler.Bounds.start.X;
		int startZ = sampler.Bounds.start.Z;

		// We consider only the southernmost Z for each X.
		// Cast to `int?` so that FirstOrDefault returns null for "none found".
		var zSouthToNorth = Enumerable.Range(sampler.Bounds.start.Z, sampler.Bounds.end.Z - sampler.Bounds.start.Z)
			.Cast<int?>()
			.ToList();
		zSouthToNorth.Reverse();

		Run? currentRun = null;
		List<Run> runs = new();

		for (int x = sampler.Bounds.start.X; x < sampler.Bounds.end.X; x++)
		{
			var currZ = zSouthToNorth.FirstOrDefault(z => sampler.Sample(new XZ(x, z!.Value)));
			if (!currZ.HasValue)
			{
				jaunt = null!;
				return false;
			}

			int laneOffset = currZ.Value - startZ;
			if (currentRun.HasValue && laneOffset == currentRun.Value.laneOffset)
			{
				currentRun = currentRun.Value with { length = currentRun.Value.length + 1 };
			}
			else
			{
				if (currentRun.HasValue)
				{
					runs.Add(currentRun.Value);
				}
				currentRun = new Run(x - startX, 1, laneOffset);
			}
		}

		if (currentRun.HasValue)
		{
			runs.Add(currentRun.Value);
		}

		// validate lane offset always changes by +1 or -1
		for (int i = 1; i < runs.Count; i++)
		{
			int delta = runs[i].laneOffset - runs[i - 1].laneOffset;
			if (delta != 1 && delta != -1)
			{
				jaunt = null!;
				return false;
			}
		}

		jaunt = new Jaunt(runs, validateZero: false);
		return true;
	}
}

public sealed record PositionedJaunt
{
	public required Jaunt Jaunt { get; init; }
	public required Rect Bounds { get; init; }
	public required int Rotation { get; init; }
	public required CardinalDirection OutsideDirection { get; init; }
}
