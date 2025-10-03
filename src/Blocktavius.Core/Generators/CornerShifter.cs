using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core.Generators;

/// <summary>
/// The <see cref="FencepostShifter"/> does the same thing with much cleaner code.
/// I need to visually confirm there is no aesthetic benefit to this code and then remove it.
/// </summary>
static class CornerShifter
{
	public record Settings
	{
		public required bool CanRelaxMaxRunLength { get; init; }
		public required bool CanRelaxMinRunLength { get; init; }

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

		// We need to store the artificial (boundary) posts separately from MinX/MaxX because
		// each post gives us a range (using the min+max run lengths) whereas MinX/MaxX are used
		// for the constraints prev[0-1] and prev[last+1]...
		// ...Maybe it would be smarter to not slice the spans, and use indexStart and indexEnd instead?
		// Then you could derive the "artificial posts" as Corners[indexStart-1] and Corners[indexEnd]
		// and you could derive MinX and MaxX as Prev[indexStart-1] and Corners[indexEnd]
		public required int ArtificialPostLeft { get; init; }
		public required int ArtificialPostRight { get; init; }

		/// <summary>
		/// Returns the range for <paramref name="i"/> based on immutable information,
		/// such as the previous layer and the settings.
		/// The returned range should always contain at least one value.
		/// </summary>
		public Range ImmutableRange(int i, Settings settings)
		{
			var range = new Range(Prev[i] - settings.MaxShift, Prev[i] + settings.MaxShift);
			range = range.ConstrainLeft(MinX).ConstrainRight(MaxX);

			if (i == 0)
			{
				// Normally the immutable range does not consider run lengths,
				// but the artificial posts are special because they are immutable.
				// Furthermore, the splitting logic requires this so it can detect a
				// split which violates the max run length.
				range = range
					.ConstrainLeft(ArtificialPostLeft + settings.MinRunLength)
					.ConstrainRight(ArtificialPostLeft + settings.MaxRunLength);
			}
			else
			{
				range = range.ConstrainLeft(Prev[i - 1]);
			}

			if (i + 1 == Prev.Length)
			{
				range = range
					.ConstrainLeft(ArtificialPostRight - settings.MaxRunLength)
					.ConstrainRight(ArtificialPostRight - settings.MinRunLength);
			}
			else
			{
				range = range.ConstrainRight(Prev[i + 1]);
			}

			return range;
		}

		/// <summary>
		/// Further constrains the <see cref="ImmutableRange"/> by looking at the current
		/// values of neighbors, which will be in flux while we are solving this subproblem.
		/// The returned range might not have any values, meaning we need to shift
		/// at least one neighbor.
		/// </summary>
		public Range DynamicRange(int i, Settings settings)
		{
			int leftPost = ArtificialPostLeft;
			int rightPost = ArtificialPostRight;

			if (i > 0)
			{
				leftPost = Corners[i - 1];
			}
			if (i + 1 < Corners.Length)
			{
				rightPost = Corners[i + 1];
			}

			return ImmutableRange(i, settings)
				.ConstrainLeft(leftPost + settings.MinRunLength)
				.ConstrainRight(leftPost + settings.MaxRunLength)
				.ConstrainLeft(rightPost - settings.MaxRunLength)
				.ConstrainRight(rightPost - settings.MinRunLength);
		}
	}

	private static bool DivideOrConquer(Subproblem subproblem, PRNG prng, Settings settings)
	{
		if (subproblem.Corners.Length < 16)
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

			if ("false".Length == 0) // NOMERGE - this is doing nothing, right?
			{
				// Make sure we will have enough room for the 2 subproblems (left and right).
				// For example, if we have Length=5 that means we have 4 runs;
				// and if we choose splitIndex=3 that means we have 3 runs to the left and 1 run to the right.
				int minWidthLeft = splitIndex * settings.MinRunLength;
				int minWidthRight = (subproblem.Corners.Length - 1 - splitIndex) * settings.MinRunLength;

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
					MaxX = Math.Min(myX - 1, subproblem.Prev[splitIndex]),
					ArtificialPostLeft = subproblem.ArtificialPostLeft,
					ArtificialPostRight = myX,
				};
				var right = new Subproblem()
				{
					Corners = subproblem.Corners.Slice(splitIndex + 1),
					Prev = subproblem.Prev.Slice(splitIndex + 1),
					MinX = Math.Max(myX + 1, subproblem.Prev[splitIndex]),
					MaxX = subproblem.MaxX,
					ArtificialPostLeft = myX,
					ArtificialPostRight = subproblem.ArtificialPostRight,
				};

				var ltest = left.ImmutableRange(splitIndex - 1, settings);
				var rtest = right.ImmutableRange(0, settings);
				if (Math.Min(ltest.Width, rtest.Width) < 1 || Math.Max(ltest.Width, rtest.Width) > settings.MaxRunLength)
				{
					// here is where we could put this choice of (splitIndex, x) into a queue of things we could try
					// if we relax the settings... but seems not to be needed yet
				}
				else if (DivideOrConquer(left, prng, settings) && DivideOrConquer(right, prng, settings))
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

	private static bool TryRelaxSettings(ref Settings settings)
	{
		if (settings.CanRelaxMaxRunLength && settings.MaxRunLength < settings.Width)
		{
			settings = settings with { MaxRunLength = settings.Width };
			return true;
		}
		else if (settings.CanRelaxMinRunLength && settings.MinRunLength > 1)
		{
			settings = settings with { MinRunLength = settings.MinRunLength - 1 };
			return true;
		}
		return false;
	}

	private static bool Conquer2(Subproblem subproblem, PRNG prng, Settings settings)
	{
		do
		{
			if (Conquer2(subproblem, prng, settings, retries: 1000)) { return true; }
		}
		while (TryRelaxSettings(ref settings));

		return false;
	}

	private static bool Conquer2(Subproblem subproblem, PRNG prng, Settings settings, int retries)
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
			if (runLength >= settings.MinRunLength && runLength <= settings.MaxRunLength)
			{
				continue; // already resolved
			}

			if (retries-- < 1)
			{
				return false;
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

	private static List<Corner> ShiftCorners3(PRNG prng, Contour previous, Settings settings)
	{
		var workingCopy = previous.Corners.Select(c => c.X).ToArray();

		// By convention, a corner having X=3 represents a change from X=3 to X=4.
		// So a corner at X=0 is fine, but a corner in the last position would be
		// equivalent to no corner at all. So subtract 2 instead of 1 here:
		int maxX = settings.Width - 2;

		var subproblem = new Subproblem()
		{
			Corners = workingCopy,
			Prev = workingCopy.ToArray(), // make another copy that is now immutable
			MinX = 0,
			MaxX = maxX,
			ArtificialPostLeft = 0 - settings.MinRunLength,
			ArtificialPostRight = maxX + settings.MinRunLength,
		};

		if (DivideOrConquer(subproblem, prng, settings))
		{
			return workingCopy.Index()
				.Select(x => previous.Corners[x.Index] with { X = x.Item })
				.ToList();
		}
		throw new Exception("assert fail");
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
