using System;
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
If this fence is too long, we have to pull the left and/or right neighbors closer.
If this fence is too shot, we have to push the left and/or right neighbors away.
There are 4 operations, all of which produce a "ResolutionPlan":
  * pull left closer
  * pull right closer
  * push left away
  * push right away
Each of these operations is given a fenceIndex and the requested amount of space.
Here is the pseudocode for the PushLeftAway() operation
  * leftPostIndex = fenceIndex - 1
  * rightPostIndex = fenceIndex
  * Compute how much space we can provide by adjusting the right post only.
    If this amount is exceeds the requested space, we are done.
  * Else we have to consider moving the left post too. We start by computing
  * maxPossibleLeftPostMove = leftPost.CurrentValue - leftPost.IroncladRange.Start
  * if maxPossibleLeftPostMove == 0 we can immediately return
  * actualLeftPostMove = recursive PushLeftAway(fenceIndex - 1, spaceRequested: maxPossiblePostMove)
  * now we have the final amount of space that can be freed and we are done
I think the ResolutionPlan that is returned should have something like this:
  * int AvailableSpace
  * Stack<(int postIndex, int plannedIndex)> PlannedAdjustments

The other 3 operations would work in a similar recursive manner to PushLeftAway().

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
[x] Convince me that you understand the proposed algorithm and agree with its soundness.
    (It would be even better if you can come up with a better algorithm.)
    Also let me know if you agree with this checklist or want to change it.
[x] Remove any useless leftover hacking from inside this class.
[ ] Create and test the ResolutionPlan mechanism.
    Tests should prove that even if the "random nudge" phase is totally broken
    (imagine that it assigns totally random values instead of a value from the
     ironclad range), the ResolutionPlan mechanism *still* works!
    (I think this is true; if not we can relax it.)
[ ] Complete the algorithm. In a new file, add statistical tests similar to those of ExerciseCornerShifter.
*/
internal class FencepostShifter
{
	internal sealed record Settings
	{
		public required int MaxNudge { get; init; }
		public required int TotalLength { get; init; }
		public required int MinFenceLength { get; init; }
		public required int MaxFenceLength { get; init; }
	}

	internal record struct Post(int X, Range IroncladRange);

	internal sealed record ResolutionPlan
	{
		public required int AvailableSpace { get; init; }
		public required Stack<(int PostIndex, int PlannedPosition)> PlannedAdjustments { get; init; }

		public static ResolutionPlan Empty => new()
		{
			AvailableSpace = 0,
			PlannedAdjustments = new Stack<(int, int)>()
		};

		public static ResolutionPlan NoSpace => new()
		{
			AvailableSpace = -1,
			PlannedAdjustments = new Stack<(int, int)>()
		};
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

			if (i > 0)
			{
				range = range.ConstrainLeft(posts[i - 1] + 1);
			}
			if (i < posts.Count - 1)
			{
				range = range.ConstrainRight(posts[i + 1] - 1);
			}

			ranges[i] = range;
		}

		return ranges;
	}

	private static ResolutionPlan PullLeftCloser(IReadOnlyList<Post> posts, int fenceIndex, int spaceRequested, Settings settings)
	{
		int leftPostIndex = fenceIndex - 1;

		if (leftPostIndex < 0)
			return ResolutionPlan.NoSpace;

		var leftPost = posts[leftPostIndex];
		int maxRightMove = leftPost.IroncladRange.xMax - leftPost.X;

		if (maxRightMove <= 0)
			return ResolutionPlan.NoSpace;

		int spaceAvailable = Math.Min(maxRightMove, spaceRequested);
		int newLeftPosition = leftPost.X + spaceAvailable;

		var plan = new ResolutionPlan
		{
			AvailableSpace = spaceAvailable,
			PlannedAdjustments = new Stack<(int, int)>()
		};
		plan.PlannedAdjustments.Push((leftPostIndex, newLeftPosition));

		return plan;
	}

	private static ResolutionPlan PullRightCloser(IReadOnlyList<Post> posts, int fenceIndex, int spaceRequested, Settings settings)
	{
		int rightPostIndex = fenceIndex;

		if (rightPostIndex >= posts.Count)
			return ResolutionPlan.NoSpace;

		var rightPost = posts[rightPostIndex];
		int maxLeftMove = rightPost.X - rightPost.IroncladRange.xMin;

		if (maxLeftMove <= 0)
			return ResolutionPlan.NoSpace;

		int spaceAvailable = Math.Min(maxLeftMove, spaceRequested);
		int newRightPosition = rightPost.X - spaceAvailable;

		var plan = new ResolutionPlan
		{
			AvailableSpace = spaceAvailable,
			PlannedAdjustments = new Stack<(int, int)>()
		};
		plan.PlannedAdjustments.Push((rightPostIndex, newRightPosition));

		return plan;
	}

	private static ResolutionPlan PushLeftAway(IReadOnlyList<Post> posts, int fenceIndex, int spaceRequested, Settings settings)
	{
		int leftPostIndex = fenceIndex - 1;
		int rightPostIndex = fenceIndex;

		// For fence 0, there's no left post to push, so we can only move the right post
		if (fenceIndex == 0)
		{
			if (rightPostIndex >= posts.Count)
				return ResolutionPlan.NoSpace;

			var fenceRightPost = posts[rightPostIndex];
			int maxRightMoveFence = fenceRightPost.IroncladRange.xMax - fenceRightPost.X;
			int spaceAvailable = Math.Min(maxRightMoveFence, spaceRequested);

			if (spaceAvailable <= 0)
				return ResolutionPlan.NoSpace;

			var plan = new ResolutionPlan
			{
				AvailableSpace = spaceAvailable,
				PlannedAdjustments = new Stack<(int, int)>()
			};
			plan.PlannedAdjustments.Push((rightPostIndex, fenceRightPost.X + spaceAvailable));
			return plan;
		}

		// For last fence, there's no right post, so this operation doesn't apply
		if (rightPostIndex >= posts.Count)
			return ResolutionPlan.NoSpace;

		var rightPost = posts[rightPostIndex];
		int maxRightMove = rightPost.IroncladRange.xMax - rightPost.X;
		int spaceFromRightMove = Math.Min(maxRightMove, spaceRequested);

		if (spaceFromRightMove >= spaceRequested)
		{
			var plan = new ResolutionPlan
			{
				AvailableSpace = spaceFromRightMove,
				PlannedAdjustments = new Stack<(int, int)>()
			};
			plan.PlannedAdjustments.Push((rightPostIndex, rightPost.X + spaceFromRightMove));
			return plan;
		}

		var leftPost = posts[leftPostIndex];
		int maxLeftMove = leftPost.X - leftPost.IroncladRange.xMin;

		if (maxLeftMove <= 0)
		{
			var plan = new ResolutionPlan
			{
				AvailableSpace = spaceFromRightMove,
				PlannedAdjustments = new Stack<(int, int)>()
			};
			plan.PlannedAdjustments.Push((rightPostIndex, rightPost.X + spaceFromRightMove));
			return plan;
		}

		int remainingSpaceNeeded = spaceRequested - spaceFromRightMove;
		var recursivePlan = PushLeftAway(posts, fenceIndex - 1, remainingSpaceNeeded, settings);

		int totalAvailableSpace = spaceFromRightMove + Math.Max(0, recursivePlan.AvailableSpace);

		var combinedPlan = new ResolutionPlan
		{
			AvailableSpace = totalAvailableSpace,
			PlannedAdjustments = new Stack<(int, int)>(recursivePlan.PlannedAdjustments)
		};

		if (spaceFromRightMove > 0)
			combinedPlan.PlannedAdjustments.Push((rightPostIndex, rightPost.X + spaceFromRightMove));

		return combinedPlan;
	}

	private static ResolutionPlan PushRightAway(IReadOnlyList<Post> posts, int fenceIndex, int spaceRequested, Settings settings)
	{
		int leftPostIndex = fenceIndex - 1;
		int rightPostIndex = fenceIndex;

		// For last fence, there's no right post to push, so we can only move the left post
		if (fenceIndex == posts.Count)
		{
			if (leftPostIndex < 0)
				return ResolutionPlan.NoSpace;

			var fenceLeftPost = posts[leftPostIndex];
			int maxLeftMoveFence = fenceLeftPost.X - fenceLeftPost.IroncladRange.xMin;
			int spaceAvailable = Math.Min(maxLeftMoveFence, spaceRequested);

			if (spaceAvailable <= 0)
				return ResolutionPlan.NoSpace;

			var plan = new ResolutionPlan
			{
				AvailableSpace = spaceAvailable,
				PlannedAdjustments = new Stack<(int, int)>()
			};
			plan.PlannedAdjustments.Push((leftPostIndex, fenceLeftPost.X - spaceAvailable));
			return plan;
		}

		// For fence 0, there's no left post, so this operation doesn't apply
		if (leftPostIndex < 0)
			return ResolutionPlan.NoSpace;

		// Both posts exist for middle fences
		if (rightPostIndex >= posts.Count)
			return ResolutionPlan.NoSpace;

		var leftPost = posts[leftPostIndex];
		int maxLeftMove = leftPost.X - leftPost.IroncladRange.xMin;
		int spaceFromLeftMove = Math.Min(maxLeftMove, spaceRequested);

		if (spaceFromLeftMove >= spaceRequested)
		{
			var plan = new ResolutionPlan
			{
				AvailableSpace = spaceFromLeftMove,
				PlannedAdjustments = new Stack<(int, int)>()
			};
			plan.PlannedAdjustments.Push((leftPostIndex, leftPost.X - spaceFromLeftMove));
			return plan;
		}

		var rightPost = posts[rightPostIndex];
		int maxRightMove = rightPost.IroncladRange.xMax - rightPost.X;

		if (maxRightMove <= 0)
		{
			var plan = new ResolutionPlan
			{
				AvailableSpace = spaceFromLeftMove,
				PlannedAdjustments = new Stack<(int, int)>()
			};
			plan.PlannedAdjustments.Push((leftPostIndex, leftPost.X - spaceFromLeftMove));
			return plan;
		}

		int remainingSpaceNeeded = spaceRequested - spaceFromLeftMove;
		var recursivePlan = PushRightAway(posts, fenceIndex + 1, remainingSpaceNeeded, settings);

		int totalAvailableSpace = spaceFromLeftMove + Math.Max(0, recursivePlan.AvailableSpace);

		var combinedPlan = new ResolutionPlan
		{
			AvailableSpace = totalAvailableSpace,
			PlannedAdjustments = new Stack<(int, int)>(recursivePlan.PlannedAdjustments)
		};

		if (spaceFromLeftMove > 0)
			combinedPlan.PlannedAdjustments.Push((leftPostIndex, leftPost.X - spaceFromLeftMove));

		return combinedPlan;
	}

	private static void ApplyResolutionPlans(List<Post> posts, int spaceNeeded, ResolutionPlan leftPlan, ResolutionPlan rightPlan)
	{
		int totalAvailableSpace = Math.Max(0, leftPlan.AvailableSpace) + Math.Max(0, rightPlan.AvailableSpace);

		if (totalAvailableSpace < spaceNeeded)
			throw new InvalidOperationException("Insufficient space to resolve fence violation");

		// Apply minimal adjustments to get the required space
		int spaceStillNeeded = spaceNeeded;

		// Apply left plan adjustments first
		if (spaceStillNeeded > 0 && leftPlan.AvailableSpace > 0)
		{
			int spaceFromLeft = Math.Min(spaceStillNeeded, leftPlan.AvailableSpace);
			ApplyAdjustmentsForSpace(posts, leftPlan.PlannedAdjustments, spaceFromLeft);
			spaceStillNeeded -= spaceFromLeft;
		}

		// Apply right plan adjustments if still needed
		if (spaceStillNeeded > 0 && rightPlan.AvailableSpace > 0)
		{
			int spaceFromRight = Math.Min(spaceStillNeeded, rightPlan.AvailableSpace);
			ApplyAdjustmentsForSpace(posts, rightPlan.PlannedAdjustments, spaceFromRight);
			spaceStillNeeded -= spaceFromRight;
		}
	}

	private static void ApplyAdjustmentsForSpace(List<Post> posts, Stack<(int, int)> adjustments, int spaceNeeded)
	{
		// For now, apply all adjustments (this is a simplified approach)
		// A more sophisticated version would calculate the exact adjustments needed
		foreach (var (postIndex, plannedPosition) in adjustments)
		{
			var currentPost = posts[postIndex];
			posts[postIndex] = currentPost with { X = plannedPosition };
		}
	}

	public static IReadOnlyList<int> ShiftPosts(IReadOnlyList<int> originalPosts, Settings settings, PRNG prng)
	{
		var ironcladRanges = BuildIroncladRanges(originalPosts, settings);
		var posts = new List<Post>();

		for (int i = 0; i < originalPosts.Count; i++)
		{
			var range = ironcladRanges[i];
			int nudgedPosition = range.RandomX(prng);
			posts.Add(new Post(nudgedPosition, range));
		}

		// First, ensure all posts are within their ironclad ranges and in ascending order
		EnsurePostsAreValid(posts);

		while (true)
		{
			int? violatingFenceIndex = FindViolatingFence(posts, settings);
			if (violatingFenceIndex == null)
				break;

			ResolveFenceViolation(posts, violatingFenceIndex.Value, settings);
		}

		return posts.Select(p => p.X).ToList();
	}

	private static void EnsurePostsAreValid(List<Post> posts)
	{
		// First pass: Move each post to within its ironclad range
		for (int i = 0; i < posts.Count; i++)
		{
			var post = posts[i];
			if (!post.IroncladRange.Contains(post.X))
			{
				// Move to the closest valid position within the range
				int newX = Math.Max(post.IroncladRange.xMin, Math.Min(post.IroncladRange.xMax, post.X));
				posts[i] = post with { X = newX };
			}
		}

		// Second pass: Ensure ascending order while respecting ironclad ranges
		for (int i = 1; i < posts.Count; i++)
		{
			if (posts[i].X <= posts[i - 1].X)
			{
				// Find a valid position for post i that's greater than post i-1
				int minRequired = posts[i - 1].X + 1;
				int newX = Math.Max(minRequired, posts[i].IroncladRange.xMin);

				if (newX <= posts[i].IroncladRange.xMax)
				{
					posts[i] = posts[i] with { X = newX };
				}
				else
				{
					// If we can't fit post i after post i-1, try to move post i-1 left
					int maxAllowedForPrevious = posts[i].IroncladRange.xMin - 1;
					if (maxAllowedForPrevious >= posts[i - 1].IroncladRange.xMin)
					{
						posts[i - 1] = posts[i - 1] with { X = Math.Min(posts[i - 1].X, maxAllowedForPrevious) };
						posts[i] = posts[i] with { X = posts[i].IroncladRange.xMin };
					}
					else
					{
						// Last resort: use ironclad range centers
						posts[i] = posts[i] with { X = (posts[i].IroncladRange.xMin + posts[i].IroncladRange.xMax) / 2 };
					}
				}
			}
		}
	}

	private static int? FindViolatingFence(IReadOnlyList<Post> posts, Settings settings)
	{
		for (int fenceIndex = 0; fenceIndex <= posts.Count; fenceIndex++)
		{
			int fenceLength = GetFenceLength(posts, fenceIndex, settings.TotalLength);


			if (fenceLength < settings.MinFenceLength || fenceLength > settings.MaxFenceLength)
				return fenceIndex;
		}
		return null;
	}

	private static int GetFenceLength(IReadOnlyList<Post> posts, int fenceIndex, int totalLength)
	{
		int fenceStart = fenceIndex == 0 ? 0 : posts[fenceIndex - 1].X;
		int fenceEnd = fenceIndex == posts.Count ? totalLength : posts[fenceIndex].X;
		return fenceEnd - fenceStart;
	}

	private static void ResolveFenceViolation(List<Post> posts, int fenceIndex, Settings settings)
	{
		int fenceLength = GetFenceLength(posts, fenceIndex, settings.TotalLength);
		int excess = fenceLength - settings.MaxFenceLength;
		int shortage = settings.MinFenceLength - fenceLength;

		if (excess > 0)
		{
			// Fence is too long, need to make it shorter by pulling posts closer
			var leftPlan = PullLeftCloser(posts, fenceIndex, excess, settings);
			var rightPlan = PullRightCloser(posts, fenceIndex, excess, settings);

			if (Math.Max(0, leftPlan.AvailableSpace) + Math.Max(0, rightPlan.AvailableSpace) < excess)
				throw new InvalidOperationException("Cannot resolve fence violation - no valid solution exists");

			ApplyResolutionPlans(posts, excess, leftPlan, rightPlan);
		}
		else if (shortage > 0)
		{
			// Fence is too short, need to make it longer by pushing posts apart
			var leftPlan = PushLeftAway(posts, fenceIndex, shortage, settings);
			var rightPlan = PushRightAway(posts, fenceIndex, shortage, settings);

			if (Math.Max(0, leftPlan.AvailableSpace) + Math.Max(0, rightPlan.AvailableSpace) < shortage)
				throw new InvalidOperationException("Cannot resolve fence violation - no valid solution exists");

			ApplyResolutionPlans(posts, shortage, leftPlan, rightPlan);
		}
	}

	internal static class TestHelper
	{
		public static ResolutionPlan PullLeftCloser(IReadOnlyList<Post> posts, int fenceIndex, int spaceRequested, Settings settings)
			=> FencepostShifter.PullLeftCloser(posts, fenceIndex, spaceRequested, settings);

		public static ResolutionPlan PullRightCloser(IReadOnlyList<Post> posts, int fenceIndex, int spaceRequested, Settings settings)
			=> FencepostShifter.PullRightCloser(posts, fenceIndex, spaceRequested, settings);

		public static ResolutionPlan PushLeftAway(IReadOnlyList<Post> posts, int fenceIndex, int spaceRequested, Settings settings)
			=> FencepostShifter.PushLeftAway(posts, fenceIndex, spaceRequested, settings);

		public static ResolutionPlan PushRightAway(IReadOnlyList<Post> posts, int fenceIndex, int spaceRequested, Settings settings)
			=> FencepostShifter.PushRightAway(posts, fenceIndex, spaceRequested, settings);

		public static IReadOnlyList<Range> BuildIroncladRanges(IReadOnlyList<int> posts, Settings settings)
			=> FencepostShifter.BuildIroncladRanges(posts, settings);

		public static int? FindViolatingFence(IReadOnlyList<Post> posts, Settings settings)
			=> FencepostShifter.FindViolatingFence(posts, settings);

		public static void ResolveFenceViolation(List<Post> posts, int fenceIndex, Settings settings)
			=> FencepostShifter.ResolveFenceViolation(posts, fenceIndex, settings);

		public static void EnsurePostsAreValid(List<Post> posts)
			=> FencepostShifter.EnsurePostsAreValid(posts);
	}
}
