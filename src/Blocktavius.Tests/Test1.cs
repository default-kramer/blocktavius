using Blocktavius.Core;
using Blocktavius.Core.Generators;
using Blocktavius.Core.Generators.BasicHill;

namespace Blocktavius.Tests
{
	[TestClass]
	public sealed class Test1
	{
		[TestMethod]
		public void TestMethod1()
		{
			var prng = PRNG.Create(new Random());
			Console.WriteLine(prng.Serialize());

			for (int i = 0; i < 1000; i++)
			{
				var hill = BasicHillGenerator.Create(prng);
				Assert.IsNotNull(hill);
			}
		}

		[TestMethod]
		public void TestMethod2()
		{
			var prng = PRNG.Create(new Random());
			Console.WriteLine(prng.Serialize());

			for (int i = 0; i < 1000; i++)
			{
				var hill = BasicHill2.Create(prng, width: 50);
				Assert.IsNotNull(hill);
			}
		}

		[TestMethod]
		public void TestMethod3()
		{
			var prng = PRNG.Create(new Random());
			Console.WriteLine(prng.Serialize());

			for (int i = 0; i < 1000; i++)
			{
				var hill = Hillish.Create(prng);
				Assert.IsNotNull(hill);
			}
		}

		[TestMethod]
		public void ExerciseAdamantCliff()
		{
			var prng = PRNG.Create(new Random());
			Console.WriteLine(prng.Serialize());

			for (int i = 0; i < 98765; i++)
			{
				var cliff = Core.Generators.Hills.AdamantCliffBuilder.Generate(prng, 100, 60);
				Assert.IsNotNull(cliff);
			}
		}

		[TestMethod]
		public void AdamantCliffKnownBug()
		{
			var prng = PRNG.Deserialize("3309110861-16864670-3535033132-2513591414-720943084-1714556781");
			try
			{
				for (int i = 0; i < 1000; i++)
				{
					var cliff = Core.Generators.Hills.AdamantCliffBuilder.Generate(prng, 100, 60);
					Assert.IsNotNull(cliff);
				}
			}
			catch (Exception)
			{
				Assert.Inconclusive("The known issue in adamant cliff is still present... Fix is not urgent.");
				return;
			}
			Assert.Fail("ATTENTION: Is the bug in Adamant Cliff fixed? Or is it just fixed for this particular seed?");
		}

		[TestMethod]
		public void ExerciseTileTagger()
		{
			var prng = PRNG.Create(new Random());
			Console.WriteLine(prng.Serialize());

			// Use a single tag
			// Generate a blob-shaped hill
			// Render it!
		}

		[TestMethod]
		[Timeout(1000 * 60)]
		public void ExerciseCornerShifter()
		{
			var prng = PRNG.Create(new Random());
			Console.WriteLine(prng.Serialize());

			var settings = new CornerShifter.Settings()
			{
				MaxShift = 999,
				MaxMatchingDirections = 99,
				MaxDepth = 5,
				MaxRunLength = 7,
				MinRunLength = 1,
				Width = 350,
				CanRelaxMaxRunLength = false,
				CanRelaxMinRunLength = false,
			};

			int totalShifts = 0;
			int zeroShifts = 0;

			int totalRuns = 0;
			int longRuns = 0;

			const int shiftsPerRun = 50;
			int runs = 1234;

			while (runs-- > 0)
			{
				var initial = CornerShifter.Contour.Generate(prng, settings);
				var prev = initial;
				Assert.IsTrue(prev.Depth <= settings.MaxDepth);

				int shifts = shiftsPerRun;
				while (shifts-- > 0)
				{
					var current = prev.Shift(prng, settings);
					var workingCopy = current.Corners;

					Assert.AreEqual(prev.Corners.Count, current.Corners.Count);
					Assert.AreEqual(prev.Depth, current.Depth);

					Assert.IsTrue(current.Corners.First().X >= 0);
					Assert.IsTrue(current.Corners.Last().X < settings.Width);

					for (int i = 1; i < workingCopy.Count; i++)
					{
						totalRuns++;
						int runLength = workingCopy[i].X - workingCopy[i - 1].X;
						Assert.IsTrue(runLength >= settings.MinRunLength);
						//Assert.IsTrue(runLength <= settings.MaxRunLength);
						if (runLength > settings.MaxRunLength)
						{
							longRuns++;
						}
						Assert.IsTrue(workingCopy[i].X > workingCopy[i - 1].X);

						Assert.IsTrue(workingCopy[i].X >= prev.Corners[i - 1].X);
						if (i < workingCopy.Count - 1)
						{
							Assert.IsTrue(workingCopy[i].X <= prev.Corners[i + 1].X);
						}
					}

					Assert.IsTrue(current.Corners.Select(c => c.Dir).SequenceEqual(prev.Corners.Select(c => c.Dir)));

					int zeroShiftsThisTime = 0;
					for (int i = 0; i < current.Corners.Count; i++)
					{
						totalShifts++;
						if (current.Corners[i].X == prev.Corners[i].X)
						{
							zeroShiftsThisTime++;
							zeroShifts++;
						}
					}
					if (zeroShiftsThisTime > Math.Max(12, current.Corners.Count / 2))
					{
						Assert.Fail($"Too many zero shifts in a single pass: {zeroShiftsThisTime} (out of {current.Corners.Count})");
					}

					prev = current;
				}
			}

			if (longRuns > totalRuns / 20)
			{
				Assert.Fail($"Too many long runs: {longRuns} / {totalRuns}");
			}
			Assert.AreEqual(0, longRuns); // Oh yeah!

			if (zeroShifts > totalShifts / 3)
			{
				Assert.Fail($"Too many zero shifts: {zeroShifts} / {totalShifts}");
			}
			if (zeroShifts < totalShifts / 100)
			{
				Assert.Fail($"Too few zero shifts: {zeroShifts} / {totalShifts}");
			}
		}
	}
}