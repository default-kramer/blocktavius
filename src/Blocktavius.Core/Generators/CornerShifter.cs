using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

		/// <summary>
		/// Returns the range for <paramref name="i"/> based on immutable information,
		/// such as the previous layer and the settings.
		/// The returned range should always contain at least one value.
		/// </summary>
		public Range ImmutableRange(int i, Settings settings)
		{
			int left = Math.Max(this.MinX, Prev[i] - settings.MaxShift);
			int right = Math.Min(this.MaxX, Prev[i] + settings.MaxShift);
			if (i > 0)
			{
				left = Math.Max(left, Prev[i - 1]);
			}
			if (i + 1 < Prev.Length)
			{
				right = Math.Min(right, Prev[i + 1]);
			}
			return new Range(left, right);
		}

		/// <summary>
		/// Further constrains the <see cref="ImmutableRange"/> by looking at the current
		/// values of neighbors, which will be in flux while we are solving this subproblem.
		/// The returned range might not have any values, meaning we need to shift
		/// at least one neighbor.
		/// </summary>
		public Range DynamicRange(int i, Settings settings)
		{
			var range = ImmutableRange(i, settings);
			if (i > 0)
			{
				range = range.Intersect(Corners[i - 1] + settings.MinRunLength, int.MaxValue);
			}
			if (i + 1 < Corners.Length)
			{
				range = range.Intersect(int.MinValue, Corners[i + 1] - settings.MinRunLength);
			}
			return range;
		}
	}

	private static bool DivideOrConquer(Subproblem subproblem, PRNG prng, Settings settings)
	{
		if (subproblem.Corners.Length < 7)
		{
			return Conquer2(subproblem, prng, settings);
		}
		else
		{
			return Divide(subproblem, prng, settings);
		}
	}

	private static bool Divide(Subproblem subproblem, PRNG prng, Settings settings)
	{
		var splitCenter = subproblem.Corners.Length / 2;
		List<int> splitIndexes = [splitCenter - 2, splitCenter - 1, splitCenter, splitCenter + 1, splitCenter + 2];
		prng.Shuffle(splitIndexes);
		foreach (var splitIndex in splitIndexes)
		{
			var myRange = subproblem.ImmutableRange(splitIndex, settings);

			// Make sure we will have enough room for the 2 subproblems (left and right)
			int minWidthLeft = splitIndex * settings.MinRunLength;
			int minWidthRight = (subproblem.Corners.Length - splitIndex) * settings.MinRunLength;

			// For example, imagine that we have
			//   minWidthLeft = 6
			//   minWidthRight = 3
			//   MinX = 100
			//   MaxX = 120
			// That means we have the following ranges
			//   [100..105] reserved for the left subproblem
			//   [106..117] range this pivot index could have without dooming either subproblem
			//   [118..120] reserved for the right subproblem.
			// Those "reserved" ranges become irrelevant soon - when we choose a value for the pivot index
			// we recurse into the left and right subproblems using the actual ranges based on that choice.
			myRange = myRange.Intersect(subproblem.MinX + minWidthLeft, subproblem.MaxX - minWidthRight);
			if (myRange.IsInfeasible)
			{
				continue;
			}

			var xChoices = Enumerable.Range(myRange.xMin, myRange.Width).ToList();
			prng.Shuffle(xChoices);
			foreach (var myX in xChoices)
			{
				subproblem.Corners[splitIndex] = myX;

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

			// This splitIndex doesn't work.
			// Revert the mutation and try next split index.
			subproblem.Corners[splitIndex] = subproblem.Prev[splitIndex];
		}

		return false;
	}

	private static bool Conquer2(Subproblem subproblem, PRNG prng, Settings settings)
	{
		var corners = subproblem.Corners;

		// Populate with initial guesses.
		for (int i = 0; i < corners.Length; i++)
		{
			var range = subproblem.ImmutableRange(i, settings);
			corners[i] = range.RandomX(prng);
		}

		// Now we have to enforce MinRunLength.
		// Metaphorically, we can call corners "posts" and runs "fences".
		// We have N posts and N-1 fences.
		// We say that the Nth fence connects post N to post N+1.
		// In other words, if unresolved.Contains(3) that means we have not ensured that
		// corners[4] - corners[3] >= MinRunLength.
		int numFences = corners.Length - 1;
		var unresolvedFences = new Queue<int>(Enumerable.Range(0, numFences));
		while (unresolvedFences.TryDequeue(out int i))
		{
			if (i < 0 || i >= numFences) // we don't do bounds checks when adding to the queue
			{
				continue;
			}

			int runLength = corners[i + 1] - corners[i];
			if (runLength >= settings.MinRunLength)
			{
				continue; // already resolved
			}

			// If either the left post or the right post has a nonzero dynamic range,
			// then we know that moving that post within that range will resolve the
			// current fence and will not cause any other fences to unresolve.
			var dynamicRangeLeft = (0, subproblem.DynamicRange(i, settings));
			var dynamicRangeRight = (1, subproblem.DynamicRange(i + 1, settings));
			var dynamicRange = dynamicRangeLeft.Item2.Width > 0 ? dynamicRangeLeft.AsNullable() : null;
			if (dynamicRangeRight.Item2.Width > 0)
			{
				if (dynamicRange == null || prng.NextInt32(2) == 0)
				{
					dynamicRange = dynamicRangeRight;
				}
			}

			if (dynamicRange.HasValue)
			{
				i += dynamicRange.Value.Item1;
				corners[i] = dynamicRange.Value.Item2.RandomX(prng);
			}
			else
			{
				// Neither left nor right has a tight range, too bad.
				// Randomly choose the left or right post and move it
				// to a random position anywhere in its immutable range.
				// Basically, we just shake things up randomly.
				// Assuming there is a valid solution, we will eventually land on it.
				i += prng.NextInt32(2);
				corners[i] = subproblem.ImmutableRange(i, settings).RandomX(prng);
				unresolvedFences.Enqueue(i - 1); // the fence to the left of post i
				unresolvedFences.Enqueue(i);     // the fence to the right of post i
			}
		}

		return true;
	}

	/// <remarks>
	/// AI code that I don't really understand, but the tests pass
	/// </remarks>
	private static bool Conquer(Subproblem subproblem, PRNG prng, Settings settings)
	{
		// A simplified, self-contained version of the ShiftCorners logic, adapted for a subproblem.
		bool maxRunLengthViolated = false;

		for (int attempt = 0; attempt < 10; attempt++)
		{
			subproblem.Prev.CopyTo(subproblem.Corners); // reset
			var tempCorners = subproblem.Corners;

			bool possible = true;

			// 1. Pick a random corner and shift it
			int cornerIndex = prng.NextInt32(tempCorners.Length);
			int shiftAmount = prng.NextInt32(-6, 7);
			if (shiftAmount == 0) shiftAmount = 1;

			tempCorners[cornerIndex] += shiftAmount;

			// 2. Clamp the initial shift
			if (cornerIndex == 0) tempCorners[cornerIndex] = Math.Max(tempCorners[cornerIndex], subproblem.MinX);
			if (cornerIndex == tempCorners.Length - 1) tempCorners[cornerIndex] = Math.Min(tempCorners[cornerIndex], subproblem.MaxX);
			if (cornerIndex > 0) tempCorners[cornerIndex] = Math.Max(tempCorners[cornerIndex], subproblem.Prev[cornerIndex - 1]);
			if (cornerIndex < tempCorners.Length - 1) tempCorners[cornerIndex] = Math.Min(tempCorners[cornerIndex], subproblem.Prev[cornerIndex + 1]);

			// 3. Propagate changes
			// Propagate left
			for (int i = cornerIndex - 1; i >= 0; i--)
			{
				int neighborX = tempCorners[i + 1];
				int min = neighborX - settings.MaxRunLength;
				int max = neighborX - settings.MinRunLength;

				// Clamp
				if (i == 0) min = Math.Max(min, subproblem.MinX);
				if (i > 0) min = Math.Max(min, subproblem.Prev[i - 1]);
				max = Math.Min(max, subproblem.Prev[i + 1]);

				if (min > max) { possible = false; break; }
				int choice = max;
				if (choice == subproblem.Prev[i] && max > min)
				{
					choice = max - 1;
				}
				tempCorners[i] = choice;
			}
			if (!possible) continue;

			// Propagate right
			for (int i = cornerIndex + 1; i < tempCorners.Length; i++)
			{
				int neighborX = tempCorners[i - 1];
				int min = neighborX + settings.MinRunLength;
				int max = neighborX + settings.MaxRunLength;

				// Clamp
				min = Math.Max(min, subproblem.Prev[i - 1]);
				if (i == tempCorners.Length - 1) max = Math.Min(max, subproblem.MaxX);
				if (i < tempCorners.Length - 1) max = Math.Min(max, subproblem.Prev[i + 1]);

				if (min > max) { possible = false; break; }
				int choice = min;
				if (choice == subproblem.Prev[i] && min < max)
				{
					choice = min + 1;
				}
				tempCorners[i] = choice;
			}
			if (!possible) continue;

			// If we got here, a valid solution was found.
			maxRunLengthViolated.ToString(); // silence compiler
			return true;
		}

		// All strict attempts failed. Perform one last non-strict attempt.
		// This guarantees a solution, even if it violates MaxRunLength.
		maxRunLengthViolated = true;
		{
			subproblem.Prev.CopyTo(subproblem.Corners); // reset
			var corners = subproblem.Corners;

			int cornerIndex = corners.Length / 2;
			// Propagate left
			for (int i = cornerIndex - 1; i >= 0; i--)
			{
				int neighborX = corners[i + 1];
				int min = int.MinValue;
				int max = neighborX - settings.MinRunLength;
				if (i == 0) min = Math.Max(min, subproblem.MinX);
				if (i > 0) min = Math.Max(min, subproblem.Prev[i - 1]);
				max = Math.Min(max, subproblem.Prev[i + 1]);
				corners[i] = max;
			}
			// Propagate right
			for (int i = cornerIndex; i < corners.Length; i++)
			{
				int neighborX = (i > 0) ? corners[i - 1] : 0; // Simplified for non-strict pass
				int min = neighborX + settings.MinRunLength;
				int max = int.MaxValue;
				min = Math.Max(min, (i > 0) ? subproblem.Prev[i - 1] : 0);
				if (i == corners.Length - 1) max = Math.Min(max, subproblem.MaxX);
				if (i < corners.Length - 1) max = Math.Min(max, subproblem.Prev[i + 1]);
				corners[i] = min;
			}
		}

		maxRunLengthViolated.ToString(); // silence compiler
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
