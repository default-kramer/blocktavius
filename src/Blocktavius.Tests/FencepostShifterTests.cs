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
}
