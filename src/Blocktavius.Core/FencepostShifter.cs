using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

/*
A fencepost is simply an integral value in a list or an array.
For example, consider the following list of posts:
	[0]: 3
	[1]: 8
	[2]: 12
The above example defines a list of 3 posts, which also defines 4 fences.
The first fence starts at 0 (always) and ends at 3, having length==3.
The second fence starts at 3 and ends at 8, having length==5.
The third fence starts at 8 and ends at 12, having length==4.
The fourth fence starts at 12 and ends at TotalLength.
In general, N posts always defines N+1 fences.

The caller will provide a list of posts (named `orig` here).
The problem is to return a new list of shifted posts (named `shifted` here)
subject to the following constraints:
  * `shifted.Count == orig.Count` (cannot add or remove fences)
  * `Math.Abs(shifted[i] - orig[i])` must not exceed MaxNudge
  * The MinFenceLength and MaxFenceLength must be respected.
    Remember that each post defines one half of two fences.
    This will naturally disallow negative/zero fence lengths.
  * Each fence defined by `shifted` must overlap that same fence
    as defined by `orig` by at least one space.
    In other words, `shifted[i] > orig[i-1]` must be true for all i>0
    and `shifted[i] < orig[i+1]` must be true for all i except the last one.

The algorithm will work something like this:
1) Calculate the "ironclad range" for each post.
   This is the smallest range for that post which could possibly contain a valid solution.
   It should include the MaxNudge constraint and the overlap constraint.
   This range is "ironclad" because it is based only on `orig` and the settings;
   it will be true no matter what random decisions we make later.
2) Randomly nudge each post to somewhere in its ironclad range.
   This may introduce fence length violations.
3) While any violations exist, pick one at random and resolve it.

It is only the Resolve step which is tricky, but I believe the following approach is sound.
First realize that only fences can become invalid, by violating either
the MinFenceLength or MaxFenceLength constraints.
So let's assume we are trying to resolve the fence bounded by orig[4] and orig[5].
If this fence is too long, we have to pull the left and/or right posts closer.
If this fence is too shot, we have to push the left and/or right posts away.
There are 4 operations, all of which produce a "ResolutionPlan":
  * pull left closer  (attract the left)
  * pull right closer (attract the right)
  * push left away    (repel the left)
  * push right away   (repel the right)
All 4 of these operations are recursive, and accept { int postIndex; int requestedSpace }.
Here is pseudocode for PushLeft(i, requestedSpace)
  define easySpace = how far left posts[i] can move without moving posts[i-1]
  if easySpace >= requestedSpace
    return requestedSpace
  else
    define hardSpace = recurse PushLeft(i-1, requestedSpace - easySpace)
    return easySpace + hardSpace
You can imagine the other 3 operations would be similar.


Here example pseudocode of a complete resolution.
  int thisFenceLength = shifted[5]-shifted[4]
  int excess = thisFenceLength - settings.MaxFenceLength;
  int shortage = settings.MinFenceLength - thisFenceLength;
  if (excess > 0)
    var leftPlan = PullLeftCloser(excess, fenceIndex: 4)
    var rightPlan = PullRightCloser(excess, fenceIndex: 5)
    int totalAvailableSpace = leftPlan.AvailableSpace + rightPlan.AvailableSpace
    // NOTE if either leftPlan or rightPlan has negative available space
    // you should replan the other side requesting `excess - theNegativePlan.AvailableSpace`
    if totalAvailableSpace < excess { ASSERT FAIL "no valid solution?" }
    Apply(excess, leftPlan, rightPlan)
  else if (shortage > 0)
    ... similar idea using PushLeftAway and PushRightAway ...
  else
    return
*/
internal class FencepostShifter
{
	public sealed record Settings
	{
		public required int MaxNudge { get; init; }
		public required int TotalLength { get; init; }
		public required int MinFenceLength { get; init; }
		public required int MaxFenceLength { get; init; }
	}

	/// <summary>
	/// An index that would be out of bounds returns the Left/Right Anchor value instead.
	/// Simplifies code a bit by allowing you to unconditionally get a neighbor
	/// without checking that i-1 or i+1 is in range.
	/// </summary>
	sealed class Postlist<T> : IReadOnlyList<T>
	{
		private readonly IReadOnlyList<T> items;
		public Postlist(IEnumerable<T> items)
		{
			this.items = items.ToList();
		}

		public required T LeftAnchor { get; init; }
		public required T RightAnchor { get; init; }

		public T this[int index]
		{
			get
			{
				if (index < 0) { return LeftAnchor; }
				if (index < items.Count) { return items[index]; }
				return RightAnchor;
			}
		}

		public int Count => items.Count;

		public IEnumerator<T> GetEnumerator() => items.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();
	}

	sealed class Post
	{
		public required int X { get; init; }
		public required Range IroncladRange { get; init; }
	}

	sealed class MutablePost
	{
		public required int X { get; set; }
		public required Range IroncladRange { get; init; }

		public static MutablePost Copy(Post post) => new MutablePost { X = post.X, IroncladRange = post.IroncladRange };
	}

	private readonly Postlist<Post> orig;
	private readonly Postlist<MutablePost> shifted;
	private readonly Settings settings;

	public FencepostShifter(IReadOnlyList<int> orig, Settings settings)
	{
		this.settings = settings;

		var ranges = BuildIroncladRanges(orig, settings);

		var posts = orig.Zip(ranges).Select(x => new Post() { X = x.First, IroncladRange = x.Second }).ToList();
		this.orig = new Postlist<Post>(posts)
		{
			LeftAnchor = new Post() { X = 0, IroncladRange = new Range(0, 0) },
			RightAnchor = new Post() { X = settings.TotalLength, IroncladRange = new Range(settings.TotalLength, settings.TotalLength) }
		};

		this.shifted = new Postlist<MutablePost>(this.orig.Select(MutablePost.Copy).ToList())
		{
			LeftAnchor = MutablePost.Copy(this.orig.LeftAnchor),
			RightAnchor = MutablePost.Copy(this.orig.RightAnchor),
		};

		var args = new ShiftOperation.Args { settings = settings, shifted = shifted };
		this.attractTheLeft = new(args);
		this.repelTheRight = new(args);
		this.attractTheRight = new(args);
		this.repelTheLeft = new(args);
	}

	public List<int> Shift(PRNG prng)
	{
		RandomShift(prng);

		var violations = FindInvalidFences().ToList();
		while (violations.Count > 0)
		{
			prng.Shuffle(violations);
			foreach (var violation in violations)
			{
				if (violation.Excess > 0)
				{
					Resolve(prng, violation);
				}
				else
				{
					Resolve(prng, violation);
				}
			}

			violations = FindInvalidFences().ToList();
		}

		return shifted.Select(p => p.X).ToList();
	}

	private void RandomShift(PRNG prng)
	{
		foreach (var post in shifted)
		{
			post.X = prng.NextInt32(post.IroncladRange.xMin, post.IroncladRange.xMax + 1);
		}
	}

	sealed class InvalidFence
	{
		public required int LeftPostIndex { get; init; }
		public required int RightPostIndex { get; init; }
		public required int Excess { get; init; } // negative indicates shortage
	}

	private IEnumerable<InvalidFence> FindInvalidFences()
	{
		// -1 is a valid index due to our custom indexer trick
		for (int i = -1; i < shifted.Count; i++)
		{
			var violation = AsInvalidFence(i);
			if (violation != null)
			{
				yield return violation;
			}
		}
	}

	private InvalidFence? AsInvalidFence(int leftPostIndex)
	{
		var left = shifted[leftPostIndex];
		var right = shifted[leftPostIndex + 1];
		if (!left.IroncladRange.Contains(left.X))
		{
			throw new Exception($"assert fail: {left}");
		}
		if (!right.IroncladRange.Contains(right.X))
		{
			throw new Exception($"assert fail: {right}");
		}

		int fenceLength = right.X - left.X;
		int excess = fenceLength - settings.MaxFenceLength;
		int shortage = settings.MinFenceLength - fenceLength;
		if (excess > 0)
		{
			// okay
		}
		else if (shortage > 0)
		{
			excess = -shortage;
		}
		else
		{
			return null;
		}

		return new InvalidFence
		{
			Excess = excess,
			LeftPostIndex = leftPostIndex,
			RightPostIndex = leftPostIndex + 1,
		};
	}

	private void Resolve(PRNG prng, InvalidFence fence)
	{
		var recheck = AsInvalidFence(fence.LeftPostIndex);
		if (recheck == null)
		{
			return; // already resolved
		}
		fence = recheck;

		ShiftOperation leftOp;
		ShiftOperation rightOp;
		if (fence.Excess > 0)
		{
			leftOp = attractTheLeft;
			rightOp = attractTheRight;
		}
		else if (fence.Excess < 0)
		{
			leftOp = repelTheLeft;
			rightOp = repelTheRight;
		}
		else
		{
			throw new Exception("assert fail");
		}

		int requestedSpace = Math.Abs(fence.Excess);

		var leftPlan = leftOp.Execute(fence.LeftPostIndex, requestedSpace);
		var rightPlan = rightOp.Execute(fence.RightPostIndex, requestedSpace);

		ResolutionPlan plan;
		if (leftPlan.AvailableSpace < 1)
		{
			plan = rightPlan;
		}
		else if (rightPlan.AvailableSpace < 1)
		{
			plan = leftPlan;
		}
		else
		{
			plan = prng.RandomChoice(leftPlan, rightPlan);
		}
		Apply(plan);
	}

	private void Apply(ResolutionPlan plan)
	{
		foreach (var move in plan.PlannedMoves)
		{
			shifted[move.postIndex].X = move.newPosition;
		}
	}

	sealed class ResolutionPlan
	{
		public Stack<(int postIndex, int newPosition)> PlannedMoves { get; private init; } = new();
		public required int RequestedSpace { get; init; }
		public int AvailableSpace { get; set; } = 0;
		public int StillNeeded => RequestedSpace - AvailableSpace;
		public bool IsDone => AvailableSpace >= RequestedSpace;

		public ResolutionPlan CreateRecursivePlan(int requestSpace) => new ResolutionPlan()
		{
			AvailableSpace = 0,
			RequestedSpace = requestSpace,
			PlannedMoves = this.PlannedMoves, // use the same mutable stack!
		};
	}

	abstract class ShiftOperation
	{
		public sealed record Args
		{
			public required Settings settings { get; init; }
			public required Postlist<MutablePost> shifted { get; init; }
		}

		protected readonly Settings settings;
		protected readonly Postlist<MutablePost> shifted;

		protected ShiftOperation(Args args)
		{
			this.settings = args.settings;
			this.shifted = args.shifted;
		}

		protected abstract int GetMaxPossibleSpace(MutablePost post);
		protected abstract int CalculateEasySpace(MutablePost post, int postIndex);
		protected abstract int GetNextPostIndex(int postIndex);
		protected abstract int CalculateNewPosition(MutablePost post, int totalSpace);

		public ResolutionPlan Execute(int postIndex, int spaceRequested)
		{
			var plan = new ResolutionPlan() { RequestedSpace = spaceRequested };
			Execute(postIndex, plan);
			return plan;
		}

		private void Execute(int postIndex, ResolutionPlan plan)
		{
			if (postIndex < 0 || postIndex > shifted.Count - 1 || plan.IsDone)
			{
				return;
			}

			var post = shifted[postIndex];
			int maxPossibleSpace = GetMaxPossibleSpace(post);
			if (maxPossibleSpace < 1)
			{
				return; // This post cannot move
			}

			// First we clamp the incoming requested space down to the max possible
			// space this post could move. This avoids unnecessary recursion
			// and may be necessary for correctness.
			// This does not interfere with our ability to provide a partial result
			// (e.g. "you requested 4, but the best I can do is 2").
			int requestedSpace = Math.Min(maxPossibleSpace, plan.StillNeeded);

			// Compute "easy space" as the amount of space we can free up without recursing.
			int easySpace = CalculateEasySpace(post, postIndex);
			if (easySpace >= requestedSpace) // defensive, pretty sure it can never be > here
			{
				plan.AvailableSpace += requestedSpace;
				plan.PlannedMoves.Push((postIndex, CalculateNewPosition(post, requestedSpace)));
				return;
			}

			// The "easy space" is not enough, we have to recurse
			var recurse = plan.CreateRecursivePlan(requestedSpace - easySpace);
			Execute(GetNextPostIndex(postIndex), recurse);
			int hardSpace = Math.Min(recurse.AvailableSpace, recurse.RequestedSpace);

			// Either we've satisfied the request, or we've gone all the way to the boundary
			// and there's nothing more we can do.
			int totalSpace = easySpace + hardSpace;
			plan.AvailableSpace += totalSpace;
			plan.PlannedMoves.Push((postIndex, CalculateNewPosition(post, totalSpace)));
		}
	}

	sealed class AttractTheLeft : ShiftOperation
	{
		public AttractTheLeft(Args args) : base(args) { }

		protected override int GetMaxPossibleSpace(MutablePost post) => post.IroncladRange.xMax - post.X;
		protected override int GetNextPostIndex(int postIndex) => postIndex - 1;
		protected override int CalculateNewPosition(MutablePost post, int totalSpace) => post.X + totalSpace;

		protected override int CalculateEasySpace(MutablePost post, int postIndex) =>
			post.IroncladRange
				.ConstrainRight(shifted[postIndex - 1].X + settings.MaxFenceLength)
				.xMax - post.X;
	}

	sealed class RepelTheRight : ShiftOperation
	{
		public RepelTheRight(Args args) : base(args) { }

		protected override int GetMaxPossibleSpace(MutablePost post) => post.IroncladRange.xMax - post.X;
		protected override int GetNextPostIndex(int postIndex) => postIndex + 1;
		protected override int CalculateNewPosition(MutablePost post, int totalSpace) => post.X + totalSpace;

		protected override int CalculateEasySpace(MutablePost post, int postIndex) =>
			post.IroncladRange
				.ConstrainRight(shifted[postIndex + 1].X - settings.MinFenceLength)
				.xMax - post.X;
	}

	sealed class AttractTheRight : ShiftOperation
	{
		public AttractTheRight(Args args) : base(args) { }

		protected override int GetMaxPossibleSpace(MutablePost post) => post.X - post.IroncladRange.xMin;
		protected override int GetNextPostIndex(int postIndex) => postIndex + 1;
		protected override int CalculateNewPosition(MutablePost post, int totalSpace) => post.X - totalSpace;

		protected override int CalculateEasySpace(MutablePost post, int postIndex) =>
			post.X - post.IroncladRange
				.ConstrainLeft(shifted[postIndex + 1].X - settings.MaxFenceLength)
				.xMin;
	}

	sealed class RepelTheLeft : ShiftOperation
	{
		public RepelTheLeft(Args args) : base(args) { }

		protected override int GetMaxPossibleSpace(MutablePost post) => post.X - post.IroncladRange.xMin;
		protected override int GetNextPostIndex(int postIndex) => postIndex - 1;
		protected override int CalculateNewPosition(MutablePost post, int totalSpace) => post.X - totalSpace;

		protected override int CalculateEasySpace(MutablePost post, int postIndex) =>
			post.X - post.IroncladRange
				.ConstrainLeft(shifted[postIndex - 1].X + settings.MinFenceLength)
				.xMin;
	}

	private readonly AttractTheLeft attractTheLeft;
	private readonly RepelTheRight repelTheRight;
	private readonly AttractTheRight attractTheRight;
	private readonly RepelTheLeft repelTheLeft;

	private static IReadOnlyList<Range> BuildIroncladRanges(IReadOnlyList<int> posts, Settings settings)
	{
		var ranges = GC.AllocateUninitializedArray<Range>(posts.Count);

		for (int i = 0; i < posts.Count; i++)
		{
			var range = Range.NoConstraints
				.ConstrainLeft(posts[i] - settings.MaxNudge)
				.ConstrainRight(posts[i] + settings.MaxNudge)
				.ConstrainLeft(0)
				.ConstrainRight(settings.TotalLength - 1);

			// Consider the fence having posts L and R.
			// If post L increases to (or beyond) the original position of post R there is no overlap.
			// If post R decreases to (or beyond) the original position of post L there is no overlap.
			// This must be disallowed; at least 1 space of overlap is required.
			if (i - 1 >= 0)
			{
				range = range.ConstrainLeft(posts[i - 1] + 1);
			}
			if (i + 1 < posts.Count)
			{
				range = range.ConstrainRight(posts[i + 1] - 1);
			}

			// This really only matters near the start and end of the list,
			// but there's no reason not to do it for everyone
			range = range.ConstrainLeft(i * settings.MinFenceLength);
			range = range.ConstrainRight(settings.TotalLength - (posts.Count - i) * settings.MinFenceLength);

			ranges[i] = range;
		}

		return ranges;
	}

	internal class TestApi
	{
		private Settings settings;
		private IReadOnlyList<int> orig;
		private FencepostShifter shifter;

		public TestApi()
		{
			settings = new Settings()
			{
				MinFenceLength = 1,
				MaxFenceLength = 99,
				TotalLength = 99,
				MaxNudge = 99,
			};

			orig = [7, 20, 30];

			shifter = new(orig, settings);
		}

		public TestApi WithMinFenceLength(int minFenceLength)
		{
			settings = settings with { MinFenceLength = minFenceLength };
			return this;
		}

		public TestApi WithMaxFenceLength(int maxFenceLength)
		{
			settings = settings with { MaxFenceLength = maxFenceLength };
			return this;
		}

		public TestApi WithMaxNudge(int maxNudge)
		{
			settings = settings with { MaxNudge = maxNudge };
			return this;
		}

		public TestApi Reload(params int[] posts)
		{
			this.orig = posts;
			this.shifter = new FencepostShifter(orig, settings);
			return this;
		}

		public string Print(string format = "D2")
		{
			var sb = new StringBuilder();
			sb.Append($"[0] ");
			sb.Append(string.Join(" ", shifter.shifted.Select(post => post.X.ToString(format))));
			sb.Append($" [{shifter.settings.TotalLength}]");
			return sb.ToString();
		}

		private int Do(string postName, ShiftOperation operation, int amount)
		{
			// makes tests more readable -- the string "25" means the post having 25==post.X;
			// whereas 25 means the post at index 25
			int postValue = int.Parse(postName);
			int postIndex = shifter.shifted.Index().Where(p => p.Item.X == postValue).Single().Index;

			var plan = operation.Execute(postIndex, amount);
			shifter.Apply(plan);

			return plan.AvailableSpace;
		}

		public int RepelLeft(string postName, int amount) => Do(postName, shifter.repelTheLeft, amount);

		public int RepelRight(string postName, int amount) => Do(postName, shifter.repelTheRight, amount);

		public int AttractRight(string postName, int amount) => Do(postName, shifter.attractTheRight, amount);

		public int AttractLeft(string postName, int amount) => Do(postName, shifter.attractTheLeft, amount);
	}
}
