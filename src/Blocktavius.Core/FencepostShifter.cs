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
	public sealed record Settings
	{
		public required int MaxNudge { get; init; }
		public required int TotalLength { get; init; }
		public required int MinFenceLength { get; init; }
		public required int MaxFenceLength { get; init; }
	}

	record struct Post(int X, Range IroncladRange);

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
				range = range.ConstrainRight(posts[i + 1] - settings.MinFenceLength);
			}

			ranges[i] = range;
		}

		return ranges;
	}
}
