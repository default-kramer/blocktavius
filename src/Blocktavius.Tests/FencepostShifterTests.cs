using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Blocktavius.Core.FencepostShifter;

namespace Blocktavius.Tests;

[TestClass]
public class FencepostShifterTests
{
	private static TestApi NewShifter() => new TestApi();

	[TestMethod]
	public void push_left_vs_min_fence_length()
	{
		var shifter = NewShifter()
			.WithMinFenceLength(10)
			.Reload(10, 30);

		Assert.AreEqual("[0] 10 30 [99]", shifter.Print());
		Assert.AreEqual(6, shifter.PushLeft("30", 6));
		Assert.AreEqual("[0] 10 24 [99]", shifter.Print());
		Assert.AreEqual(4, shifter.PushLeft("24", 6));
		Assert.AreEqual("[0] 10 20 [99]", shifter.Print());
		Assert.AreEqual(0, shifter.PushLeft("20", 1));
		Assert.AreEqual(0, shifter.PushLeft("10", 1));
		Assert.AreEqual("[0] 10 20 [99]", shifter.Print());
	}

	[TestMethod]
	public void push_right_vs_min_fence_length()
	{
		var shifter = NewShifter()
			.WithMinFenceLength(10)
			.Reload(69, 89);

		Assert.AreEqual("[0] 69 89 [99]", shifter.Print());
		Assert.AreEqual(6, shifter.PushRight("69", 6));
		Assert.AreEqual("[0] 75 89 [99]", shifter.Print());
		Assert.AreEqual(4, shifter.PushRight("75", 6));
		Assert.AreEqual("[0] 79 89 [99]", shifter.Print());
		Assert.AreEqual(0, shifter.PushRight("79", 1));
		Assert.AreEqual(0, shifter.PushRight("89", 1));
		Assert.AreEqual("[0] 79 89 [99]", shifter.Print());
	}

	[TestMethod]
	public void push_left_vs_max_nudge()
	{
		var shifter = NewShifter()
			.WithMaxNudge(10)
			.Reload(40, 70);

		Assert.AreEqual("[0] 40 70 [99]", shifter.Print());
		Assert.AreEqual(7, shifter.PushLeft("40", 7));
		Assert.AreEqual("[0] 33 70 [99]", shifter.Print());
		Assert.AreEqual(3, shifter.PushLeft("33", 7));
		Assert.AreEqual("[0] 30 70 [99]", shifter.Print());
		Assert.AreEqual(0, shifter.PushLeft("30", 1));
		Assert.AreEqual(10, shifter.PushLeft("70", 999));
		Assert.AreEqual("[0] 30 60 [99]", shifter.Print());
	}

	[TestMethod]
	public void push_right_vs_max_nudge()
	{
		var shifter = NewShifter()
			.WithMaxNudge(10)
			.Reload(30, 60);

		Assert.AreEqual("[0] 30 60 [99]", shifter.Print());
		Assert.AreEqual(7, shifter.PushRight("60", 7));
		Assert.AreEqual("[0] 30 67 [99]", shifter.Print());
		Assert.AreEqual(3, shifter.PushRight("67", 7));
		Assert.AreEqual("[0] 30 70 [99]", shifter.Print());
		Assert.AreEqual(0, shifter.PushRight("70", 1));
		Assert.AreEqual(10, shifter.PushRight("30", 999));
		Assert.AreEqual("[0] 40 70 [99]", shifter.Print());
	}

	[TestMethod]
	public void max_nudge_regression_1()
	{
		var shifter = NewShifter()
			.WithMaxNudge(2)
			.Reload(20, 40);

		// This was incorrectly returning recursing and returning 4
		// because it moved 20 to 22 and 40 to 42.
		// It should not recurse at all because the 40 post is not imposing
		// any constraints on the 20 post.
		Assert.AreEqual(2, shifter.PushRight("20", 99));
		Assert.AreEqual("[0] 22 40 [99]", shifter.Print());

		// same test, other direction
		shifter = NewShifter()
			.WithMaxNudge(2)
			.Reload(20, 40);
		Assert.AreEqual(2, shifter.PushLeft("40", 99));
		Assert.AreEqual("[0] 20 38 [99]", shifter.Print());
	}

	[TestMethod]
	public void push_left_against_invalid_state()
	{
		var shifter = NewShifter()
			.WithMaxNudge(3)
			.WithMinFenceLength(10)
			.Reload(11, 22, 45, 48);

		// Initial state is not valid, 45---48 violates MinFenceLength=10
		Assert.AreEqual("[0] 11 22 45 48 [99]", shifter.Print());

		// We try to push 48 left...
		Assert.AreEqual(-4, shifter.PushLeft("48", 1));
		Assert.AreEqual("[0] 11 22 42 52 [99]", shifter.Print());
		// ... It does its best and succeeds at pushing 45 to 42 (max nudge = 3)
		// and returns the available space of -4.
		// The fact that the test API immediately applies that -4, moving 48 to 52, is
		// a somewhat arbitrary decision affecting tests only.
		// Real scenarios would call both PushLeft and PushRight and look at both
		// plans before deciding what to do.
		// (Probably prefer to move as few posts as possible.)
	}

	[TestMethod]
	public void push_right_against_invalid_state()
	{
		var shifter = NewShifter()
			.WithMaxNudge(3)
			.WithMinFenceLength(10)
			.Reload(45, 48);

		// see "left" version of same test for comments
		Assert.AreEqual("[0] 45 48 [99]", shifter.Print());
		Assert.AreEqual(-4, shifter.PushRight("45", 10));
		Assert.AreEqual("[0] 41 51 [99]", shifter.Print());
	}

	[TestMethod]
	public void push_left_recursion()
	{
		var shifter = NewShifter()
			.WithMinFenceLength(10)
			.Reload(30, 40, 50, 77);

		Assert.AreEqual("[0] 30 40 50 77 [99]", shifter.Print());
		Assert.AreEqual(26, shifter.PushLeft("77", 99));
		Assert.AreEqual("[0] 21 31 41 51 [99]", shifter.Print());
	}

	[TestMethod]
	public void push_right_recursion()
	{
		var shifter = NewShifter()
			.WithMinFenceLength(10)
			.Reload(42, 50, 60, 70);

		Assert.AreEqual("[0] 42 50 60 70 [99]", shifter.Print());
		Assert.AreEqual(7, shifter.PushRight("42", 99));
		Assert.AreEqual("[0] 49 59 69 79 [99]", shifter.Print());
	}

	[TestMethod]
	public void push_right_tricky_recursion()
	{
		// Looking at the test above made me think "is it true that newPosts[i] must be
		// less than oldPosts[i+1] ?"
		// Because 42 stops just short of 50.
		// But that is because I packed everything so tightly.
		// Here we leave space after 50, so 42 can move past it.
		// As you can see, you have to look 2 posts ahead to see where it will stop.
		var shifter = NewShifter()
			.WithMinFenceLength(10)
			.Reload(42, 50, 70);

		Assert.AreEqual("[0] 42 50 70 [99]", shifter.Print());
		Assert.AreEqual(17, shifter.PushRight("42", 99));
		Assert.AreEqual("[0] 59 69 79 [99]", shifter.Print());
	}

	[TestMethod]
	public void pull_left_recursion()
	{
		var shifter = NewShifter()
			.WithMaxFenceLength(10)
			.Reload(85, 92);

		// here "[99]" is the limiter
		Assert.AreEqual("[0] 85 92 [99]", shifter.Print());
		Assert.AreEqual(6, shifter.PullLeft("85", 99));
		Assert.AreEqual("[0] 79 89 [99]", shifter.Print());

		shifter = NewShifter()
			.WithMaxFenceLength(10)
			.Reload(4, 10, 20, 30, 40, 50, 51, 60, 70, 80, 90);

		// here "04" is the limiter
		Assert.AreEqual("[0] 04 10 20 30 40 50 51 60 70 80 90 [99]", shifter.Print());
		Assert.AreEqual(5, shifter.PullLeft("10", 99));
		Assert.AreEqual("[0] 04 05 15 25 35 45 51 60 70 80 90 [99]", shifter.Print());

		shifter = NewShifter()
			.WithMaxFenceLength(10)
			.Reload(50, 60, 70, 73, 74);

		// here "73" is the limiter; it must stop at 71
		// ... but actually, post "70" must stop at 61, and so on down the line.
		// It seems the "must overlap previous self" rule and MaxFenceLength interact
		// in such a way that this test may be less meaningful than I hoped:
		Assert.AreEqual("[0] 50 60 70 73 74 [99]", shifter.Print());
		Assert.AreEqual(9, shifter.PullLeft("50", 99));
		Assert.AreEqual("[0] 41 51 61 71 74 [99]", shifter.Print());
	}
}
