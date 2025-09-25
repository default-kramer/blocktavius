using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using static Blocktavius.Core.Generators.CornerShifter;

namespace Blocktavius.Core.Generators;

public static class CornerShifter
{
	public record Settings
	{
		public required int MaxShift { get; init; }
		public required int Width { get; init; }
		public required int MinRunLength { get; init; }
		public required int MaxRunLength { get; init; }
		public required int MaxDepth { get; init; }
		public required int MaxMatchingDirections { get; init; }

		public void Validate()
		{
			if (Width < 1)
			{
				throw new ArgumentOutOfRangeException(nameof(Width), Width, "Must be positive");
			}

			if (MaxShift > 1000000) // could cause algorithm to overflow
			{
				throw new ArgumentOutOfRangeException(nameof(MaxShift), MaxShift, "Must not exceed 1000000");
			}
			if (MaxShift < 2)
			{
				throw new ArgumentOutOfRangeException(nameof(MaxShift), MaxShift, "Must be at least 2");
			}
		}
	}

	public sealed class Contour
	{
		public readonly int Width;
		public readonly IReadOnlyList<Corner> Corners;
		public readonly int Depth;

		public Contour(IReadOnlyList<Corner> corners, int width)
		{
			this.Width = width;
			this.Corners = corners;
			this.Depth = CalculateDepth(corners);
		}

		public static Contour Generate(PRNG prng, Settings settings) => GenerateInitialBackstop(prng, settings);

		private static int CalculateDepth(IEnumerable<Corner> corners)
		{
			int min = 0;
			int max = 0;
			int current = 0;

			foreach (var corner in corners)
			{
				current += corner.Dir.Step.Z;
				min = Math.Min(current, min);
				max = Math.Max(current, max);
			}

			return 1 + (max - min);
		}

		public Contour Shift(PRNG prng, Settings settings)
		{
			var next = ShiftCorners3(prng, this, settings);
			//var next = ShiftCorners(prng, this, settings);
			//var next = ShiftCorners2(prng, this, settings);
			return new Contour(next, this.Width);
		}
	}

	public record struct Corner(int X, Direction Dir);

	enum Status
	{
		// ORDER MATTERS! The "prepare next pass" logic returns the maximum value remaining.
		// So larger values of this enum will get resolved first.
		None,
		Finalized,
		Pending,

		/// <summary>
		/// Means that this item's constraints are now fully contained by its neighbors.
		/// This means that we can choose any value from the constrained range and be
		/// certain that a valid solution still exists (assuming at least one already existed).
		/// </summary>
		FullyConstrained,

		Infeasible,
	}

	readonly record struct Range(int xMin, int xMax) // inclusive
	{
		public bool IsInfeasible => xMax < xMin;

		public int Width => (xMax + 1) - xMin;

		public static Range NoConstraints => new Range(int.MinValue, int.MaxValue);

		public Range Intersect(int xMin, int xMax)
		{
			return new Range(Math.Max(xMin, this.xMin), Math.Min(xMax, this.xMax));
		}

		public Range Intersect(Range other) => Intersect(other.xMin, other.xMax);

		public int RandomX(PRNG prng) => prng.NextInt32(xMin, xMax + 1);
	}

	// Initially, only the previous layer can contribute to the constrainedRange.
	// Whenever we finalize an entry we update the constrainedRanges of its neighbors.
	record class Entry
	{
		public required Range fullRange { get; init; }
		public required Range constrainedRange { get; init; }
		public required int newX { get; init; }
		public required Status status { get; init; }
		private Entry() { }

		public static Entry Create(Range fullRange)
		{
			return new Entry()
			{
				fullRange = fullRange,
				constrainedRange = fullRange,
				newX = -42,
				status = Status.Pending,
			};
		}

		public Entry ConstrainFurther(Range constraints)
		{
			var newConstrainedRange = this.constrainedRange.Intersect(constraints);
			var status = this.status;
			if (newConstrainedRange.IsInfeasible)
			{
				status = Status.Infeasible;
			}
			else if (newConstrainedRange.Width == 1)
			{
				status = Status.FullyConstrained;
			}
			return this with { constrainedRange = newConstrainedRange, status = status };
		}

		public Entry Finalize(int newX)
		{
			return this with
			{
				newX = newX,
				status = Status.Finalized,
				constrainedRange = new Range(newX, newX)
			};
		}
	}

	sealed class Algo
	{
		private readonly Contour previous;
		private readonly Settings settings;
		private readonly Entry[] entries;

		public Algo Clone() => new Algo(previous, this.settings, this.entries.ToArray());

		private Algo(Contour previous, Settings settings, Entry[] entries)
		{
			this.previous = previous;
			this.settings = settings;
			this.entries = entries;
		}

		public static Algo Create(Contour previous, Settings settings)
		{
			var prev = previous.Corners;
			var entries = new Entry[prev.Count];

			for (int i = 0; i < prev.Count; i++)
			{
				var fullRange = new Range(prev[i].X - settings.MaxShift, prev[i].X + settings.MaxShift);
				var entry = Entry.Create(fullRange);

				int clampLeft = 0;
				int clampRight = settings.Width - 1;
				if (i > 0)
				{
					clampLeft = prev[i - 1].X;
				}
				if (i < prev.Count - 1)
				{
					clampRight = prev[i + 1].X;
				}
				entry = entry.ConstrainFurther(new Range(clampLeft, clampRight));
				entries[i] = entry;
			}

			return new Algo(previous, settings, entries);
		}

		public Entry this[int i] => entries[i];

		public void Finalize(int i, int newX)
		{
			var exist = entries[i];
			if (exist.status == Status.Finalized)
			{
				throw new Exception("Assert fail");
			}
			entries[i] = exist.Finalize(newX);
			UpdateNeighbor(i, false);
			UpdateNeighbor(i, true);
		}

		private bool UpdateNeighbor(int i1, bool left)
		{
			var me = entries[i1];
			int i2 = i1 + (left ? -1 : 1);

			if (i2 >= 0 && i2 < entries.Length)
			{
				var neighbor = entries[i2];
				if (neighbor.status == Status.Finalized)
				{
					return false;
				}

				int xMin = int.MinValue;
				int xMax = int.MaxValue;
				// If `me` is finalized, then my constrainedRange will have xMin==xMax
				// and there is no ambiguity in the following logic.
				// Otherwise we have to assume `me` will be as far right as possible when
				// constraining my left neighbor, but also assume `me` will be as far left
				// as possible when constraining my right neighbor.
				if (left)
				{
					xMax = me.constrainedRange.xMax - settings.MinRunLength;
				}
				else
				{
					xMin = me.constrainedRange.xMin + settings.MinRunLength;
				}

				var newNeighbor = neighbor.ConstrainFurther(new Range(xMin, xMax));
				if (newNeighbor != neighbor)
				{
					entries[i2] = newNeighbor;
					return true;
				}
			}

			return false;
		}

		public Status PrepareNextPass()
		{
			Status status;
			while (UpdateNeighborsPass(out status)) { }
			return status;
		}

		private bool UpdateNeighborsPass(out Status biggestStatus)
		{
			int maxStatus = 0;

			bool anyChange = UpdateNeighbor(0, left: false);
			for (int i = 1; i < entries.Length; i++)
			{
				// don't short circuit here!
				bool left = UpdateNeighbor(i, true);
				bool right = UpdateNeighbor(i, false);
				anyChange = anyChange || left || right;

				int status1 = (int)entries[i - 1].status;
				int status2 = (int)entries[i].status;
				maxStatus = Math.Max(maxStatus, Math.Max(status1, status2));
			}

			biggestStatus = (Status)maxStatus;
			return anyChange;
		}

		public List<int> GetIndexes(Status status)
		{
			var indexes = new List<int>(entries.Length);
			for (int i = 0; i < entries.Length; i++)
			{
				if (entries[i].status == status)
				{
					indexes.Add(i);
				}
			}
			return indexes;
		}

		public List<int> CreateBatch(Status status, int maxBatchSize)
		{
			return entries.Index()
				.Where(x => x.Item.status == status)
				.OrderBy(x => x.Item.constrainedRange.Width)
				.Take(maxBatchSize)
				.Select(x => x.Index)
				.ToList();
		}

		public List<Corner> CreateFinalResult()
		{
			return entries.Index()
				.Select(x => new Corner(x.Item.newX, previous.Corners[x.Index].Dir))
				.ToList();
		}
	}

	private static List<Corner> ShiftCorners2(PRNG prng, Contour previous, Settings settings)
	{
		var result = Finalize(Algo.Create(previous, settings), prng);
		if (!result.Item1)
		{
			// The solver failed to find a solution. This can happen.
			// Instead of throwing, we will return the original contour,
			// which the test will count as a "null shift".
			return previous.Corners.ToList();
		}
		return result.Item2.CreateFinalResult();
	}

	private static (bool, Algo) Finalize(Algo algo, PRNG prng)
	{
		var status = algo.PrepareNextPass();
		while (status != Status.Finalized)
		{
			if (status == Status.Infeasible)
			{
				return (false, algo);
			}
			else if (status == Status.FullyConstrained)
			{
				var workQueue = algo.GetIndexes(status);
				prng.Shuffle(workQueue);
				foreach (var index in workQueue)
				{
					var entry = algo[index];
					if (entry.status == Status.Finalized) continue;
					int newX = entry.constrainedRange.RandomX(prng);
					algo.Finalize(index, newX);
				}

				status = algo.PrepareNextPass();
			}
			else if (status == Status.Pending)
			{
				return SolvePending(algo, 3, prng);
			}
			else
			{
				throw new Exception($"Assert fail: {status}");
			}
		}

		return (true, algo);
	}

	private static (bool, Algo) SolvePending(Algo backtrackPoint, int retries, PRNG prng)
	{
		while (retries-- > 0)
		{
			var algo = backtrackPoint.Clone();
			var batch = algo.CreateBatch(Status.Pending, maxBatchSize: 8);
			foreach (int i in batch)
			{
				if (algo[i].constrainedRange.Width <= 0) continue;
				int newX = algo[i].constrainedRange.RandomX(prng);
				algo.Finalize(i, newX);
			}
			var result = Finalize(algo, prng);
			if (result.Item1)
			{
				return result;
			}
		}

		return (false, backtrackPoint);
	}

	readonly ref struct Subproblem
	{
		public required ReadOnlySpan<int> Prev { get; init; }
		public required Span<int> Corners { get; init; }

		/// <summary>
		/// The leftmost value that Corners[0] is allowed to have.
		/// Initial value will be 0.
		/// After a split, the right subproblem will get `chosenX + MinRunLength`.
		/// </summary>
		public required int MinX { get; init; }

		/// <summary>
		/// The rightmost value that Corners.Last is allowed to have.
		/// Initial value will be settings.Width - 1.
		/// After a split, the left subproblem will get `chosenX - MinRunLength`.
		/// </summary>
		public required int MaxX { get; init; }
	}

	private static bool DivideOrConquer(Subproblem subproblem, PRNG prng, Settings settings)
	{
		if (subproblem.Corners.Length > 7)
		{
			return Conquer(subproblem, prng, settings);
		}

		// divide
		var splitCenter = subproblem.Corners.Length / 2;
		List<int> splitIndexes = [splitCenter - 2, splitCenter - 1, splitCenter, splitCenter + 1, splitCenter + 2];
		prng.Shuffle(splitIndexes);
		foreach (var splitIndex in splitIndexes)
		{
			var myRange = new Range(subproblem.Prev[splitIndex] - settings.MaxShift, subproblem.Prev[splitIndex] + settings.MaxShift);

			myRange = myRange.Intersect(subproblem.MinX, subproblem.MaxX);

			// Can't shift beyond previous neighbors
			myRange = myRange.Intersect(subproblem.Prev[splitIndex - 1], subproblem.Prev[splitIndex + 1]);

			// Make sure we will have enough room for the 2 subproblems (left and right)
			// we are about to create.
			// For example, if we choose 3 as the splitIndex we must leave room on the left side for:
			// * item[0]
			// * minRunLength
			// * item[1]
			// * minRunLength
			// * item[2]
			// * minRunLength
			int minWidthLeft = splitIndex * (settings.MinRunLength + 1);
			// ... and then we have the current item, which doesn't count towards either limit
			// * item[3]
			// ... and then we would also need to leave room on the right side for
			// * minRunLength
			// * item[4]
			// * minRunLength
			// * item[5]
			int minWidthRight = (subproblem.Corners.Length - splitIndex) * (settings.MinRunLength + 1);

			// For a simple example, let's imagine that minWidthLeft is 6 representing 1 neighbor
			// plus a MinRunLength of 5. In that case, if MinX is 100 that means the range [100..105]
			// is reserved for the left subproblem, and the range for the split item will be [106..??]
			myRange = myRange.Intersect(subproblem.MinX + minWidthLeft, subproblem.MaxX - minWidthRight);
			if (myRange.IsInfeasible)
			{
				return false;
			}

			var xChoices = Enumerable.Range(myRange.xMin, myRange.Width).ToList();
			prng.Shuffle(xChoices);
			foreach (var myX in xChoices)
			{
				var left = new Subproblem()
				{
					Corners = subproblem.Corners.Slice(0, splitIndex),
					Prev = subproblem.Prev.Slice(0, splitIndex),
					MinX = subproblem.MinX,
					MaxX = Math.Min(myX - settings.MinRunLength, subproblem.Prev[splitIndex]),
				};
				var right = new Subproblem()
				{
					Corners = subproblem.Corners.Slice(splitIndex + 1),
					Prev = subproblem.Prev.Slice(splitIndex + 1),
					MinX = Math.Max(myX + settings.MinRunLength, subproblem.Prev[splitIndex]),
					MaxX = subproblem.MaxX,
				};

				if (DivideOrConquer(left, prng, settings) && DivideOrConquer(right, prng, settings))
				{
					return true;
				}
			}
		}

		return false;
	}

	private static bool Conquer(Subproblem subproblem, PRNG prng, Settings settings)
	{
		// solve using existing implementation:
		int xOffset = subproblem.MinX;
		var corners = subproblem.Corners.ToArray().Select(x => new Corner(x - xOffset, Direction.North)).ToList();
		var contour = new Contour(corners, 1 + subproblem.MaxX - subproblem.MinX);
		var result = ShiftCorners(prng, contour, settings);
		for (int i = 0; i < result.Count; i++)
		{
			subproblem.Corners[i] = result[i].X + xOffset;
		}
		return true;
	}

	private static List<Corner> ShiftCorners3(PRNG prng, Contour previous, Settings settings)
	{
		var workingCopy = previous.Corners.Select(c => c.X).ToArray();

		var subproblem = new Subproblem()
		{
			Corners = workingCopy,
			Prev = workingCopy.ToArray(), // make another copy that is now immutable
			MinX = 0,
			MaxX = settings.Width - 1,
		};

		if (DivideOrConquer(subproblem, prng, settings))
		{
			return workingCopy.Index()
				.Select(x => previous.Corners[x.Index] with { X = x.Item })
				.ToList();
		}
		throw new Exception("assert fail");
	}

	private static List<Corner> ShiftCorners(PRNG prng, Contour previous, Settings settings)
	{
		var prevCorners = previous.Corners;
		if (prevCorners.Count == 0) return new List<Corner>();

		var corners = prevCorners.ToList();

		// The test requires that at least one corner moves.
		// We will try to find a valid shift. If after many attempts we can't,
		// we will return the original list to avoid an infinite loop.
		for (int attempt = 0; attempt < 100; attempt++)
		{
			var tempCorners = prevCorners.ToList();
			bool changed = false;

			// Pick a random corner and a random amount to shift it by.
			int cornerIndex = prng.NextInt32(tempCorners.Count);
			int shiftAmount = prng.NextInt32(-6, 7);
			if (shiftAmount == 0) shiftAmount = 1; // Ensure some movement

			var provisionalX = tempCorners[cornerIndex].X + shiftAmount;

			// Clamp to world bounds
			provisionalX = Math.Max(0, Math.Min(settings.Width - 1, provisionalX));

			// Clamp to previous layer guides
			int minBoundRule3 = (cornerIndex > 0) ? prevCorners[cornerIndex - 1].X : 0;
			int maxBoundRule3 = (cornerIndex < prevCorners.Count - 1) ? prevCorners[cornerIndex + 1].X : settings.Width - 1;
			provisionalX = Math.Max(minBoundRule3, Math.Min(maxBoundRule3, provisionalX));

			if (provisionalX != tempCorners[cornerIndex].X)
			{
				changed = true;
			}
			tempCorners[cornerIndex] = tempCorners[cornerIndex] with { X = provisionalX };

			// Now, try to make the rest of the corners fit, propagating from our change.
			bool possible = true;

			// Propagate left
			for (int i = cornerIndex - 1; i >= 0; i--)
			{
				int neighborX = tempCorners[i + 1].X;
				int min = neighborX - settings.MaxRunLength;
				int max = neighborX - settings.MinRunLength;

				min = Math.Max(min, (i > 0) ? prevCorners[i - 1].X : 0);
				max = Math.Min(max, prevCorners[i + 1].X);

				if (min > max) { possible = false; break; }
				int choice = max;
				if (choice == prevCorners[i].X && max > min)
				{
					choice = max - 1;
				}
				tempCorners[i] = tempCorners[i] with { X = choice };
			}
			if (!possible) continue;

			// Propagate right
			for (int i = cornerIndex + 1; i < tempCorners.Count; i++)
			{
				int neighborX = tempCorners[i - 1].X;
				int min = neighborX + settings.MinRunLength;
				int max = neighborX + settings.MaxRunLength;

				min = Math.Max(min, (i > 0) ? prevCorners[i - 1].X : 0);
				max = Math.Min(max, (i < tempCorners.Count - 1) ? prevCorners[i + 1].X : settings.Width - 1);

				if (min > max) { possible = false; break; }
				int choice = min;
				if (choice == prevCorners[i].X && min < max)
				{
					choice = min + 1;
				}
				tempCorners[i] = tempCorners[i] with { X = choice };
			}
			if (!possible) continue;

			// If we made it here, the contour is valid.
			// But the test requires a change. If only the initial shift happened with no propagation,
			// it might be that other corners were not affected. Let's ensure the final result is different.
			if (!changed)
			{
				for (int i = 0; i < corners.Count; i++)
				{
					if (corners[i].X != tempCorners[i].X)
					{
						changed = true;
						break;
					}
				}
			}

			if (changed) return tempCorners;
		}

		// If we failed to find a valid changed contour after many attempts, return the original.
		return corners;
	}

	private static Contour GenerateInitialBackstop(PRNG prng, Settings config)
	{
		int width = config.Width;
		int runWidthMin = config.MinRunLength;
		int runWidthRand = config.MaxRunLength - config.MinRunLength;

		int minZ = 0;
		int maxZ = config.MaxDepth - 1;
		int z = prng.NextInt32(maxZ + 1);
		int x = 0;

		var points = new List<Corner>();
		while (x < width)
		{
			x = x + runWidthMin + prng.NextInt32(runWidthRand);
			if (x >= width)
			{
				break;
			}

			Direction dir;
			if (z == minZ)
			{
				dir = Direction.South;
			}
			else if (z == maxZ)
			{
				dir = Direction.North;
			}
			else
			{
				dir = prng.RandomChoice(Direction.North, Direction.South);
			}

			z += dir.Step.Z;
			points.Add(new Corner() { Dir = dir, X = x });
		}

		return new Contour(points, config.Width);
	}
}
