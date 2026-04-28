using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Tests;

[TestClass]
public class JauntTests
{
	private static bool TryParseJaunt(IArea area, out PositionedJaunt posJaunt, CardinalDirection outsideDir = CardinalDirection.South)
	{
		return Jaunt.TryParse(area.AsSampler(), outsideDir, out posJaunt);
	}

	[TestMethod]
	public void SimpleJauntParse()
	{
		var area = TestUtil.CreateAreaFromAscii(@"
___123__________
123___1_________
_______12_______
_________1234567");

		Assert.IsTrue(TryParseJaunt(area, out var posJaunt), "TryParse failed");
		var jaunt = posJaunt.Jaunt;
		Assert.AreEqual(5, jaunt.Runs.Count);

		Assert.IsTrue(jaunt.Runs.Select(r => r.length).SequenceEqual([3, 3, 1, 2, 7]), "wrong run lengths");
		Assert.IsTrue(jaunt.Runs.Select(r => r.laneOffset).SequenceEqual([1, 0, 1, 2, 3]), "wrong lane offsets");
	}

	[TestMethod]
	public void lane_offset_cannot_jump_by_more_than_1()
	{
		var area = TestUtil.CreateAreaFromAscii(@"
123___
______
___123");

		Assert.IsFalse(TryParseJaunt(area, out _));
	}

	[TestMethod]
	public void crops_to_jaunt_bounds()
	{
		// -- South --
		var area = TestUtil.CreateAreaFromAscii(@"
xxxxxxxxxxxx
xxxxxxxxxxxx
123xxxxxxxxx
___12345xxxx
________1234
____________
____________
____________
____________");

		Assert.IsTrue(TryParseJaunt(area, out var posJaunt, CardinalDirection.South));
		Assert.AreEqual(new Rect(new XZ(0, 2), new XZ(12, 5)), posJaunt.Bounds);
		Assert.AreEqual(0, posJaunt.Jaunt.Runs.Min(r => r.laneOffset));

		// -- East --
		area = TestUtil.CreateAreaFromAscii(@"
xx1___
xx2___
x1____
x2____
x3____");
		Assert.IsTrue(TryParseJaunt(area, out posJaunt, CardinalDirection.East));
		Assert.AreEqual(new Rect(new XZ(1, 0), new XZ(3, 5)), posJaunt.Bounds);
		Assert.AreEqual(0, posJaunt.Jaunt.Runs.Min(r => r.laneOffset));

		// -- North --
		area = TestUtil.CreateAreaFromAscii(@"
_____
_____
_____
__123
12xxx
xxxxx
xxxxx");
		Assert.IsTrue(TryParseJaunt(area, out posJaunt, CardinalDirection.North));
		Assert.AreEqual(new Rect(new XZ(0, 3), new XZ(5, 5)), posJaunt.Bounds);
		Assert.AreEqual(0, posJaunt.Jaunt.Runs.Min(r => r.laneOffset));

		// -- West --
		area = TestUtil.CreateAreaFromAscii(@"
___1x
___2x
__1xx
__2xx
__3xx
__4xx");
		Assert.IsTrue(TryParseJaunt(area, out posJaunt, CardinalDirection.West));
		Assert.AreEqual(new Rect(new XZ(2, 0), new XZ(4, 6)), posJaunt.Bounds);
		Assert.AreEqual(0, posJaunt.Jaunt.Runs.Min(r => r.laneOffset));
	}
}
