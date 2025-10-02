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
	public void repel_left_vs_min_fence_length()
	{
		var shifter = NewShifter()
			.WithMinFenceLength(10)
			.Reload(10, 30);

		Assert.AreEqual("[0] 10 30 [99]", shifter.Print());
		Assert.AreEqual(6, shifter.RepelLeft("30", 6));
		Assert.AreEqual("[0] 10 24 [99]", shifter.Print());
		Assert.AreEqual(4, shifter.RepelLeft("24", 6));
		Assert.AreEqual("[0] 10 20 [99]", shifter.Print());
		Assert.AreEqual(0, shifter.RepelLeft("20", 1));
		Assert.AreEqual(0, shifter.RepelLeft("10", 1));
		Assert.AreEqual("[0] 10 20 [99]", shifter.Print());
	}

	[TestMethod]
	public void repel_right_vs_min_fence_length()
	{
		var shifter = NewShifter()
			.WithMinFenceLength(10)
			.Reload(69, 89);

		Assert.AreEqual("[0] 69 89 [99]", shifter.Print());
		Assert.AreEqual(6, shifter.RepelRight("69", 6));
		Assert.AreEqual("[0] 75 89 [99]", shifter.Print());
		Assert.AreEqual(4, shifter.RepelRight("75", 6));
		Assert.AreEqual("[0] 79 89 [99]", shifter.Print());
		Assert.AreEqual(0, shifter.RepelRight("79", 1));
		Assert.AreEqual(0, shifter.RepelRight("89", 1));
		Assert.AreEqual("[0] 79 89 [99]", shifter.Print());
	}

	[TestMethod]
	public void repel_left_vs_max_nudge()
	{
		var shifter = NewShifter()
			.WithMaxNudge(10)
			.Reload(40, 70);

		Assert.AreEqual("[0] 40 70 [99]", shifter.Print());
		Assert.AreEqual(7, shifter.RepelLeft("40", 7));
		Assert.AreEqual("[0] 33 70 [99]", shifter.Print());
		Assert.AreEqual(3, shifter.RepelLeft("33", 7));
		Assert.AreEqual("[0] 30 70 [99]", shifter.Print());
		Assert.AreEqual(0, shifter.RepelLeft("30", 1));
		Assert.AreEqual(10, shifter.RepelLeft("70", 999));
		Assert.AreEqual("[0] 30 60 [99]", shifter.Print());
	}

	[TestMethod]
	public void repel_right_vs_max_nudge()
	{
		var shifter = NewShifter()
			.WithMaxNudge(10)
			.Reload(30, 60);

		Assert.AreEqual("[0] 30 60 [99]", shifter.Print());
		Assert.AreEqual(7, shifter.RepelRight("60", 7));
		Assert.AreEqual("[0] 30 67 [99]", shifter.Print());
		Assert.AreEqual(3, shifter.RepelRight("67", 7));
		Assert.AreEqual("[0] 30 70 [99]", shifter.Print());
		Assert.AreEqual(0, shifter.RepelRight("70", 1));
		Assert.AreEqual(10, shifter.RepelRight("30", 999));
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
		Assert.AreEqual(2, shifter.RepelRight("20", 99));
		Assert.AreEqual("[0] 22 40 [99]", shifter.Print());

		// same test, other direction
		shifter = NewShifter()
			.WithMaxNudge(2)
			.Reload(20, 40);
		Assert.AreEqual(2, shifter.RepelLeft("40", 99));
		Assert.AreEqual("[0] 20 38 [99]", shifter.Print());
	}

	[TestMethod]
	public void repel_left_against_invalid_state()
	{
		var shifter = NewShifter()
			.WithMaxNudge(3)
			.WithMinFenceLength(10)
			.Reload(11, 22, 45, 48);

		// Initial state is not valid, 45---48 violates MinFenceLength=10
		Assert.AreEqual("[0] 11 22 45 48 [99]", shifter.Print());

		// We try to push 48 left...
		Assert.AreEqual(-4, shifter.RepelLeft("48", 1));
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
	public void repel_right_against_invalid_state()
	{
		var shifter = NewShifter()
			.WithMaxNudge(3)
			.WithMinFenceLength(10)
			.Reload(45, 48);

		// see "left" version of same test for comments
		Assert.AreEqual("[0] 45 48 [99]", shifter.Print());
		Assert.AreEqual(-4, shifter.RepelRight("45", 10));
		Assert.AreEqual("[0] 41 51 [99]", shifter.Print());
	}

	[TestMethod]
	public void repel_left_recursion()
	{
		var shifter = NewShifter()
			.WithMinFenceLength(10)
			.Reload(30, 40, 50, 77);

		Assert.AreEqual("[0] 30 40 50 77 [99]", shifter.Print());
		Assert.AreEqual(26, shifter.RepelLeft("77", 99));
		Assert.AreEqual("[0] 21 31 41 51 [99]", shifter.Print());
	}

	[TestMethod]
	public void repel_right_recursion()
	{
		var shifter = NewShifter()
			.WithMinFenceLength(10)
			.Reload(42, 50, 60, 70);

		Assert.AreEqual("[0] 42 50 60 70 [99]", shifter.Print());
		Assert.AreEqual(7, shifter.RepelRight("42", 99));
		Assert.AreEqual("[0] 49 59 69 79 [99]", shifter.Print());
	}

	[TestMethod]
	public void attract_right_recursion()
	{
		var shifter = NewShifter()
			.WithMaxFenceLength(10)
			.Reload(85, 92);

		// here "[99]" is the limiter
		Assert.AreEqual("[0] 85 92 [99]", shifter.Print());
		Assert.AreEqual(6, shifter.AttractRight("85", 99));
		Assert.AreEqual("[0] 79 89 [99]", shifter.Print());

		shifter = NewShifter()
			.WithMaxFenceLength(10)
			.Reload(4, 10, 20, 30, 40, 50, 51, 60, 70, 80, 90);

		// here "04" is the limiter
		Assert.AreEqual("[0] 04 10 20 30 40 50 51 60 70 80 90 [99]", shifter.Print());
		Assert.AreEqual(5, shifter.AttractRight("10", 99));
		Assert.AreEqual("[0] 04 05 15 25 35 45 51 60 70 80 90 [99]", shifter.Print());

		shifter = NewShifter()
			.WithMaxFenceLength(10)
			.Reload(50, 60, 70, 73, 74);

		// here "73" is the limiter; it must stop at 71
		// ... but actually, post "70" must stop at 61, and so on down the line.
		// It seems the "must overlap previous self" rule and MaxFenceLength interact
		// in such a way that this test may be less meaningful than I hoped:
		Assert.AreEqual("[0] 50 60 70 73 74 [99]", shifter.Print());
		Assert.AreEqual(9, shifter.AttractRight("50", 99));
		Assert.AreEqual("[0] 41 51 61 71 74 [99]", shifter.Print());
	}

	[TestMethod]
	public void attract_left_recursion()
	{
		var shifter = NewShifter()
			.WithMaxFenceLength(10)
			.Reload(3, 9);

		// "03" cannot move past "09"
		Assert.AreEqual("[0] 03 09 [99]", shifter.Print());
		Assert.AreEqual(9, shifter.AttractLeft("09", 99));
		Assert.AreEqual("[0] 08 18 [99]", shifter.Print());
	}
}
