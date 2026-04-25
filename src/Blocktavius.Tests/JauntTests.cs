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
	[TestMethod]
	public void SimpleJauntParse()
	{
		var area = TestUtil.CreateAreaFromAscii(@"
___123__________
123___1_________
_______12_______
_________1234567");

		Assert.IsTrue(Jaunt.TryParse(area.AsSampler(), out var jaunt), "TryParse failed");
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

		Assert.IsFalse(Jaunt.TryParse(area.AsSampler(), out _));
	}
}
