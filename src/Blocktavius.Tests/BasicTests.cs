using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Tests;

[TestClass]
public class BasicTests
{
	[TestMethod]
	public void XZ_comparison()
	{
		var prng = PRNG.Deserialize("1-2-3-4-5-6");
		for (int i = 0; i < 100; i++)
		{
			var a = new XZ(prng.NextInt32(4), prng.NextInt32(4));
			var b = new XZ(prng.NextInt32(4), prng.NextInt32(4));
			if (a.Z < b.Z)
			{
				Assert.IsTrue(a.CompareTo(b) < 0);
			}
			else if (a.Z > b.Z)
			{
				Assert.IsTrue(a.CompareTo(b) > 0);
			}
			else if (a.X < b.X)
			{
				Assert.IsTrue(a.CompareTo(b) < 0);
			}
			else if (a.X > b.X)
			{
				Assert.IsTrue(a.CompareTo(b) > 0);
			}
			else
			{
				Assert.IsTrue(a.CompareTo(b) == 0);
			}
		}
	}

	[TestMethod]
	public void rect_expansion()
	{
		var rect = new Rect(new XZ(4, 4), new XZ(8, 8));
		var expander = rect.BoundsExpander();
		Assert.AreEqual(rect, expander.CurrentBounds());

		expander.Include(new XZ(2, 6));
		Assert.AreEqual(new Rect(new XZ(2, 4), new XZ(8, 8)), expander.CurrentBounds());

		expander.Include(new XZ(0, 0));
		expander.Include(new XZ(10, 20));
		Assert.AreEqual(new Rect(new XZ(0, 0), new XZ(11, 21)), expander.CurrentBounds());

		var bounds = expander.CurrentBounds() ?? throw new Exception("was null");
		Assert.IsTrue(bounds.Contains(new XZ(0, 0)));
		Assert.IsFalse(bounds.Contains(new XZ(-1, 0)));
		Assert.IsFalse(bounds.Contains(new XZ(0, -1)));
		Assert.IsTrue(bounds.Contains(new XZ(10, 20)));
		Assert.IsFalse(bounds.Contains(new XZ(11, 20)));
		Assert.IsFalse(bounds.Contains(new XZ(10, 21)));
	}
}
