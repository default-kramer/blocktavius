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
  * pull left closer
  * pull right closer
  * push left away
  * push right away
All 4 of these operations are recursive, and accept { int postIndex; int requestedSpace }.
Here pseudocode for PushLeft(i, requestedSpace)
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

INSTRUCTIONS FOR CLAUDE:
Do not modify this comment; make all changes inside the class.
I will update this comment as we proceed.

Be rigorous. This algorithm is an essential building block for this project
which requires great code, comments, and tests.
Don't hesitate to add custom types tailored to this problem
if it will improve clarity or maintainability.

Overall progress
[ ] Convince me that you understand the proposed algorithm and agree with its soundness.
    (It would be even better if you can come up with a better algorithm.)
    Also let me know if you agree with this checklist or want to change it.
[ ] Create and test the ResolutionPlan mechanism.
    Tests should prove that even if the "random nudge" phase is totally broken
    (imagine that it assigns totally random values instead of a value from the
     ironclad range), the ResolutionPlan mechanism *still* works!
    (I think this is true; if not we can relax it.)
[ ] Complete the algorithm and add statistical tests similar to those of ExerciseCornerShifter.
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
	}

	sealed class ResolutionPlan
	{
		public Stack<(int postIndex, int newPosition)> PlannedMoves { get; private init; } = new();
		public required int RequestedSpace { get; init; }
		public int AvailableSpace { get; set; } = 0;
		public int NeededSpace => RequestedSpace - AvailableSpace;
		public bool IsDone => AvailableSpace >= RequestedSpace;

		public ResolutionPlan CreateRecursivePlan(int requestSpace) => new ResolutionPlan()
		{
			AvailableSpace = 0,
			RequestedSpace = requestSpace,
			PlannedMoves = this.PlannedMoves, // use the same mutable stack!
		};
	}

	private void PushPostsLeft(int postIndex, ResolutionPlan plan)
	{
		if (postIndex < 0 || plan.IsDone)
		{
			return;
		}

		var post = shifted[postIndex];
		int leftPostMaxPossibleShift = post.X - post.IroncladRange.xMin;
		int requestedSpace = Math.Min(leftPostMaxPossibleShift, plan.NeededSpace);
		if (requestedSpace < 1)
		{
			return;
		}

		// Compute "easy space" as the amount of space we can free up without recursing.
		int easyShiftPosition = shifted[postIndex - 1].X + settings.MinFenceLength;
		int easySpace = post.X - easyShiftPosition;
		if (easySpace >= requestedSpace)
		{
			plan.AvailableSpace += requestedSpace;
			plan.PlannedMoves.Push((postIndex, post.X - requestedSpace));
			return;
		}

		// The "easy space" is not enough, we have to recurse
		var recurse = plan.CreateRecursivePlan(requestedSpace - easySpace);
		PushPostsLeft(postIndex - 1, recurse);
		int hardSpace = Math.Min(recurse.AvailableSpace, recurse.RequestedSpace);

		// Either we've satisfied the request, or we've gone all the way to the left
		// and there's nothing more we can do.
		int totalSpace = easySpace + hardSpace;
		plan.AvailableSpace += totalSpace;
		plan.PlannedMoves.Push((postIndex, post.X - totalSpace));
	}

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

			if (i - 1 >= 0)
			{
				// exclude left neighbor's post
				range = range.ConstrainLeft(posts[i - 1] + 1);
			}
			if (i + 1 < posts.Count)
			{
				// exclude right neighbor's post
				range = range.ConstrainRight(posts[i + 1] - settings.MinFenceLength);
			}

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

		public int PushLeft(string postName, int amount)
		{
			int postValue = int.Parse(postName);
			var postIndex = shifter.shifted.Index().Where(p => p.Item.X == postValue).Single().Index;

			var plan = new ResolutionPlan() { RequestedSpace = amount };
			shifter.PushPostsLeft(postIndex, plan);
			foreach (var move in plan.PlannedMoves)
			{
				shifter.shifted[move.postIndex].X = move.newPosition;
			}

			return plan.AvailableSpace;
		}
	}
}
