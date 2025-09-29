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
			.Reload(11, 22, 33, 44, 51);

		Assert.AreEqual("[0] 11 22 33 44 51 [99]", shifter.Print());
		Assert.AreEqual(1, shifter.PushLeft("51", 99));
		Assert.AreEqual("[0] 10 20 30 40 50 [99]", shifter.Print());

		// reload same array, but push 44 this time, leaving 51 in place
		shifter.Reload(11, 22, 33, 44, 51);
		Assert.AreEqual("[0] 11 22 33 44 51 [99]", shifter.Print());
		Assert.AreEqual(4, shifter.PushLeft("44", 99));
		Assert.AreEqual("[0] 10 20 30 40 51 [99]", shifter.Print());
	}

	[TestMethod]
	public void push_right_recursion()
	{
		var shifter = NewShifter()
			.WithMinFenceLength(10)
			.Reload(48, 55, 66, 77, 88);

		Assert.AreEqual("[0] 48 55 66 77 88 [99]", shifter.Print());
		Assert.AreEqual(1, shifter.PushRight("48", 99));
		Assert.AreEqual("[0] 49 59 69 79 89 [99]", shifter.Print());

		// reload same array but push 55 this time
		shifter.Reload(48, 55, 66, 77, 88);
		Assert.AreEqual("[0] 48 55 66 77 88 [99]", shifter.Print());
		Assert.AreEqual(4, shifter.PushRight("55", 99));
		Assert.AreEqual("[0] 48 59 69 79 89 [99]", shifter.Print());
	}
}
