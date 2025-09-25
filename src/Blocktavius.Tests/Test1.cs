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
		public void ExerciseQuaintCliff()
		{
			var prng = PRNG.Create(new Random());
			Console.WriteLine(prng.Serialize());

			for (int i = 0; i < 1000; i++)
			{
				var cliff = Blocktavius.Core.Generators.Cliffs.QuaintCliff.Generate(prng, 100, 60);
				Assert.IsNotNull(cliff);
			}
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
			};

			int totalShifts = 0;
			int nullShifts = 0;

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
						int runLength = workingCopy[i].X - workingCopy[i - 1].X;
						Assert.IsTrue(runLength >= settings.MinRunLength);
						Assert.IsTrue(runLength <= settings.MaxRunLength);
						Assert.IsTrue(workingCopy[i].X > workingCopy[i - 1].X);

						Assert.IsTrue(workingCopy[i].X >= prev.Corners[i - 1].X);
						if (i < workingCopy.Count - 1)
						{
							Assert.IsTrue(workingCopy[i].X <= prev.Corners[i + 1].X);
						}
					}

					Assert.IsTrue(current.Corners.Select(c => c.Dir).SequenceEqual(prev.Corners.Select(c => c.Dir)));

					totalShifts++;
					if (current.Corners.Select(c => c.X).SequenceEqual(prev.Corners.Select(c => c.X)))
					{
						nullShifts++;
					}

					prev = current;
				}
			}

			if (nullShifts > totalShifts / 1000)
			{
				Assert.Fail($"Too many null shifts: {nullShifts} / {totalShifts}");
			}
		}
	}
}