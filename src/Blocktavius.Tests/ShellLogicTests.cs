using Microsoft.VisualStudio.TestTools.UnitTesting;
using Blocktavius.Core;
using System.Collections.Generic;
using System.Linq;

namespace Blocktavius.Tests
{
	[TestClass]
	public class ShellLogicTests
	{

		[TestMethod]
		public void ComputeShells_SimpleSquare_CreatesOneOuterShell()
		{
			// ARRANGE
			var areaPoints = new List<XZ>();
			for (int x = 0; x < 3; x++)
			{
				for (int z = 0; z < 3; z++)
				{
					areaPoints.Add(new XZ(x, z));
				}
			}
			var area = new TestArea(areaPoints);

			// ACT
			var shells = ShellLogic.ComputeShells(area);

			// ASSERT
			Assert.AreEqual(1, shells.Count);
			var shell = shells[0];
			Assert.IsFalse(shell.IsHole);

			var shellPoints = shell.ShellItems.Select(i => i.XZ).ToHashSet();
			Assert.AreEqual(16, shellPoints.Count);
			Assert.AreEqual(16, shell.ShellItems.Count);
		}

		[TestMethod]
		public void ComputeShells_AreaWithHole_CreatesOuterAndInnerShell()
		{
			// ARRANGE
			// A 5x5 square with a 1x1 hole in the middle.
			var areaPoints = new List<XZ>();
			for (int x = 0; x < 5; x++)
			{
				for (int z = 0; z < 5; z++)
				{
					areaPoints.Add(new XZ(x, z));
				}
			}
			areaPoints.Remove(new XZ(2, 2)); // The hole
			var area = new TestArea(areaPoints);

			// ACT
			var shells = ShellLogic.ComputeShells(area);

			// ASSERT
			Assert.AreEqual(2, shells.Count);

			var outerShell = shells.Single(s => !s.IsHole);
			var innerShell = shells.Single(s => s.IsHole);

			Assert.AreEqual(24, outerShell.ShellItems.Count);

			Assert.AreEqual(1, innerShell.ShellItems.Select(i => i.XZ).Distinct().Count());
			Assert.AreEqual(new XZ(2, 2), innerShell.ShellItems[0].XZ);
			Assert.AreEqual(8, innerShell.ShellItems.Count);
		}

		[TestMethod]
		public void ComputeShells_InsideCorner_CreatesCorrectShellItems()
		{
			// ARRANGE
			// L-shape area
			//
			// Z v
			// 0 | % . .
			// 1 | % % %
			//    -------> X
			//     0 1 2
			var areaPoints = new List<XZ>
			{
				new XZ(0, 0),
				new XZ(0, 1), new XZ(1, 1), new XZ(2, 1)
			};
			var area = new TestArea(areaPoints);

			// The inside corner point is (1,0).

			// ACT
			var shells = ShellLogic.ComputeShells(area);

			// ASSERT
			Assert.AreEqual(1, shells.Count);
			var shell = shells[0];

			var insideCornerItems = shell.ShellItems.Where(i => i.XZ == new XZ(1, 0)).ToList();
			Assert.AreEqual(3, insideCornerItems.Count);

			var directions = insideCornerItems.Select(i => i.InsideDirection).ToHashSet();
			Assert.IsTrue(directions.Contains(Direction.West));
			Assert.IsTrue(directions.Contains(Direction.South));
			Assert.IsTrue(directions.Contains(Direction.SouthWest));
		}

		[TestMethod]
		public void compute_shells_that_overlap()
		{
			// ARRANGE
			// We expect two shells, one surrounding 0,0 and the other surrounding 2,2.
			// The xz coordinate 1,1 should appear in *both* shells.
			var areaPoints = new List<XZ> { new XZ(0, 0), new XZ(2, 2) };
			var area = new TestArea(areaPoints);

			// ACT
			var shells = ShellLogic.ComputeShells(area);

			// ASSERT
			Assert.AreEqual(2, shells.Count);
			Assert.IsTrue(shells[0].ShellItems.Any(i => i.XZ == new XZ(1, 1)));
			Assert.IsTrue(shells[1].ShellItems.Any(i => i.XZ == new XZ(1, 1)));
		}

		[TestMethod]
		public void compute_simple_shell()
		{
			// ARRANGE
			// Make the area a simple 2x2 square
			var areaPoints = new List<XZ>
			{
				new XZ(10, 10), new XZ(11, 10),
				new XZ(10, 11), new XZ(11, 11),
			};
			var area = new TestArea(areaPoints);

			// ACT
			var shells = ShellLogic.ComputeShells(area);

			// ASSERT
			// We expect 8 "normal" shell items, plus 4 outside corners, plus 0 inside corners.
			Assert.AreEqual(1, shells.Count);
			var shellItems = shells.Single().ShellItems;
			Assert.AreEqual(12, shellItems.Count);
			Assert.AreEqual(8, shellItems.Where(i => i.InsideDirection.IsCardinal).Count());
			Assert.AreEqual(4, shellItems.Where(i => i.InsideDirection.IsOrdinal).Count());
		}

		private static void AssertDirs(Shell shell, params Direction[] insideDirs)
		{
			int limit = Math.Min(insideDirs.Length, shell.ShellItems.Count);
			for (int i = 0; i < limit; i++)
			{
				Assert.AreEqual(insideDirs[i], shell.ShellItems[i].InsideDirection, $"At index {i}");
			}

			Assert.AreEqual(insideDirs.Length, shell.ShellItems.Count, "Shell Items had wrong count");
		}

		[TestMethod]
		public void outside_corner_regression()
		{
			// 1,1 should be an outside corner pointing to 2,2.
			// It wasn't because it is also a normal edge pointing to 1,0 and the "cardinal preference"
			// logic short-circuited this.
			var areaPoints = new List<XZ>
			{
				new XZ(0, 0), new XZ(1, 0),
				new XZ(0, 1),
				new XZ(0, 2), /*skip 1, 2*/ new XZ(2, 2),
				new XZ(0, 3), new XZ(1, 3), new XZ(2, 3),
			};
			var area = new TestArea(areaPoints);

			var shells = ShellLogic.ComputeShells(area);
			Assert.AreEqual(1, shells.Count);
			var shell = shells.Single();
			Assert.AreEqual(new XZ(0, -1), shell.ShellItems[0].XZ);
			AssertDirs(shells.Single(),
				// newline when Z changes:
				Direction.South, Direction.South, Direction.SouthWest,
				Direction.West,
				Direction.NorthWest, Direction.North, Direction.NorthWest, Direction.West,
				Direction.West, Direction.SouthWest, Direction.South, Direction.SouthEast, Direction.East,
				Direction.SouthEast, Direction.South, Direction.SouthWest,
				Direction.West,
				Direction.West,
				Direction.NorthWest, Direction.North, Direction.North, Direction.North, Direction.NorthEast,
				Direction.East,
				Direction.East,
				Direction.East,
				Direction.East,
				Direction.SouthEast
			);
		}

		[TestMethod]
		public void duplicate_shell_regression()
		{
			var area = TestUtil.CreateAreaFromAscii(@"
_______xxx____________________
___xx_xxxxx___________________
__xxxxxxxx____________________
_xxxxxxxxxx___________________
__xxxxxxxxxx_xxxxxxx__________
_xxxxxxxxxxxxxxxxxxx__________
_xxxxxxxxxxxxxxxxxxxx_________
_xxxxxxxxxxxxxxxxxxxx_x_______
_xxxxxxxxxxxxxxxxxxxxxx_______
_xxxxxxxxxxxxxxxxxxxxxxx______
__xxxxxxxxxxxxxxxxxxxxxxx_____
__xxxxxxxxxxxxxxxxxxxxxxxx____
__xxxxxxxxxxxxxxxxxxxxxxx_____
__xxxxxxxxxxxxxxxxxxxxxxx_____
_xxxxxxxxxxxxxxxxxxxxxxxx_____
_xxxxxxxxxxxxxxxxxxxxxxxxx____
_xxxxxxxxxxxxxxxxxxxxxxxxxxxx_
_xxxxxxxxxxxxxxxxxxxxxxxxxxxx_
_xxxxxxxxxxxxxxxxxxxxxxxxxxxx_
_xxxxxxxxxxxxxxxxxxxxxxxxxxxx_
xxxxxxxxxxxxxxxxxxxxxxxxxxxxx_
xxxxxxxxxxxxxxxxxxxxxxxxxxxxx_
_xxxxxxxxxxxxxxxxxxxxxxxxxxxx_
_xxxxxxxxxxxxxxxxxxxxxxxxxxxxx
_xxxxxxxxxxxxxxxxxxxxxxxxxxxx_
_xxxxxxxxxxxxxxxxxxxxxxxxxxxx_
_xxxxxxxxxxxxxxxxxxxxxxxxxx___
xxxxxxxxxxxxxxxxxxxxxxxxxx____
xxxxxxxxxxxxxxxxxxxxxxxx______
xxxxxxxxxxxxxxxxxxxxxxxx______
xxxxxxxxxxxxxxxxxxxxxxxxx_____
x_xxxxxxxxxxxxxxxxxxxxx_______
__xxxxxxxxxxxxxxxxxx_xx_______
______xxxxxxxxxxxxxxx_________
_____xxxxxxxxxxxx_____________
_________xx_xx__x_____________
________xxx___________________");

			var shells = ShellLogic.ComputeShells(area)
				.Where(s => !s.IsHole)
				.ToList();

			Assert.AreEqual(1, shells.Count);
		}
	}
}