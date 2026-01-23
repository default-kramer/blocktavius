using Microsoft.VisualStudio.TestTools.UnitTesting;
using Blocktavius.Core;
using System.Collections.Generic;
using System.Linq;

namespace Blocktavius.Tests
{
	[TestClass]
	public class ExpandableShellTests
	{
		[TestMethod]
		public void Expand_SinglePoint_CorrectlyUpdatesShell()
		{
			// ARRANGE
			var areaPoints = new List<XZ>
			{
				new XZ(0, 0), new XZ(1, 0),
				new XZ(0, 1), new XZ(1, 1)
			};
			var area = new TestArea(areaPoints);
			var initialShells = ShellLogic.ComputeShells(area);
			Assert.AreEqual(1, initialShells.Count);
			var initialShell = initialShells[0];

			var expandableShell = new ExpandableShell<int>(initialShell);

			var expansion = new[] { (new XZ(2, 0), 1) };

			// ACT
			expandableShell.Expand(expansion);
			var newShell = expandableShell.CurrentShell();

			// ASSERT
			var expandedAreaPoints = new List<XZ>(areaPoints) { new XZ(2, 0) };
			var expectedShell = ShellLogic.ComputeShells(new TestArea(expandedAreaPoints)).Single();

			AssertShellsEqual(expectedShell.ShellItems, newShell);
		}

		[TestMethod]
		public void IndependentExpansions()
		{
			// a, b, and c are not connected to each other but they are connected to the original area
			const string ascii = @"
___a____
___aa___
_bxxxxc_
_bxxxxc_
__b_____";
			var origArea = TestUtil.CreateAreaFromAscii(ascii.MultiReplace(["a", "b", "c"], "_"));
			var origShell = ShellLogic.ComputeShells(origArea).Single();
			var origPoints = origArea.AllPointsInArea().ToList();

			var shellEx = new ExpandableShell<char>(origShell);
			var pointsEx = TestUtil.CreateAreaFromAscii(ascii).AllPointsInArea().Except(origPoints).ToList();
			Assert.AreEqual(8, pointsEx.Count);
			var expansionId = shellEx.Expand(pointsEx.Select(xz => (xz, '.')).ToList());
			var sampler = shellEx.GetSampler(expansionId, 'x');

			var expected = ShellLogic.ComputeShells(sampler.Project(a => a.Item1)).Single();
			AssertShellsEqual(expected.ShellItems, shellEx.CurrentShell());
		}

		[TestMethod]
		public void DiagonalIsDisconnected()
		{
			// ARRANGE
			var areaPoints = new List<XZ>
			{
				new XZ(0, 0), new XZ(1, 0),
				new XZ(0, 1), new XZ(1, 1)
			};
			var area = new TestArea(areaPoints);
			var initialShells = ShellLogic.ComputeShells(area);
			Assert.AreEqual(1, initialShells.Count);
			var initialShell = initialShells[0];

			var expandableShell = new ExpandableShell<int>(initialShell);

			var expansion = new[] { (new XZ(2, 2), 1) };

			// ACT
			bool gotExpectedException = false;
			try
			{
				expandableShell.Expand(expansion);
			}
			catch (Exception ex)
			{
				gotExpectedException = true;

				// ASSERT
				Assert.AreEqual("XZ (2,2) is not connected to the expanded area", ex.Message);
			}
			Assert.IsTrue(gotExpectedException, "Expected exception not thrown");
		}

		[TestMethod]
		public void HoleRegression()
		{
			// It's possible to introduce a hole during expansion.
			// TBD if we want to eagerly detect that or leave it for post-processing...
			// But no matter what, the shell must be the outside shell rather than the hole.
			const string ascii = @"
xxxxx
xx__x
xxaxx";
			var origArea = TestUtil.CreateAreaFromAscii(ascii.MultiReplace(["a"], "_"));
			var origShell = ShellLogic.ComputeShells(origArea).Single();
			var origPoints = origArea.AllPointsInArea().ToList();

			var shell = new ExpandableShell<int>(origShell);
			var expansion = TestUtil.CreateAreaFromAscii(ascii.MultiReplace(["x"], "_")).AllPointsInArea();
			shell.Expand(expansion.Select(xz => (xz, 67)));
			var expandedShell = shell.CurrentShell();

			const int sideLengthA = 5;
			const int sideLengthB = 3;
			const int cornerCount = 4;
			int expectedCount = 2 * sideLengthA + 2 * sideLengthB + cornerCount;
			Assert.AreEqual(expectedCount, expandedShell.Count);
		}

		private void AssertShellsEqual(IReadOnlyList<ShellItem> expected, IReadOnlyList<ShellItem> actual)
		{
			var expectedSet = new HashSet<ShellItem>(expected);
			var actualSet = new HashSet<ShellItem>(actual);

			if (expected.Count != actual.Count)
			{
				System.Console.WriteLine("Expected shell items:");
				foreach (var item in expected.OrderBy(i => i.XZ.X).ThenBy(i => i.XZ.Z).ThenBy(i => i.InsideDirection.ToString()))
				{
					System.Console.WriteLine(item);
				}
				System.Console.WriteLine("Actual shell items:");
				foreach (var item in actual.OrderBy(i => i.XZ.X).ThenBy(i => i.XZ.Z).ThenBy(i => i.InsideDirection.ToString()))
				{
					System.Console.WriteLine(item);
				}
			}

			Assert.AreEqual(expected.Count, actual.Count, "Shell item counts differ.");

			foreach (var item in expected)
			{
				Assert.IsTrue(actualSet.Contains(item), $"Expected shell item {item} not found in actual shell.");
			}

			foreach (var item in actual)
			{
				Assert.IsTrue(expectedSet.Contains(item), $"Actual shell item {item} not found in expected shell.");
			}
		}
	}
}
