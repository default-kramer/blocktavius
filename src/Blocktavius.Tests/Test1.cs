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

			// TODO this seeds fails:
			//var prng = PRNG.Deserialize("3309110861-16864670-3535033132-2513591414-720943084-1714556781");

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

		[TestMethod]
		[Timeout(1000 * 60)]
		public void ExerciseFencepostShifter()
		{
			var prng = PRNG.Create(new Random());
			Console.WriteLine(prng.Serialize());
			//prng = PRNG.Deserialize("2738926009-1368371339-1413019004-521382713-187568799-2606996474");

			var settings = new FencepostShifter.Settings()
			{
				MaxNudge = 5,
				TotalLength = 100,
				MinFenceLength = 2,
				MaxFenceLength = 15
			};

			int totalFences = 0;
			int longFences = 0;
			int shortFences = 0;
			int totalShifts = 0;
			int zeroShifts = 0;

			const int shiftsPerRun = 50;
			int runs = 500;

			while (runs-- > 0)
			{
				var (shifter, prev) = CreateFencepostShifter(prng, ref settings);

				int shifts = shiftsPerRun;
				while (shifts-- > 0)
				{
					var current = shifter.Shift(prng).ToList();

					// Verify basic properties
					Assert.AreEqual(prev.Count, current.Count);

					// Check that all posts are within bounds
					Assert.IsTrue(current.First() > 0);
					Assert.IsTrue(current.Last() < settings.TotalLength);

					// Check fence length constraints
					for (int i = 0; i <= current.Count; i++)
					{
						totalFences++;
						int leftX = i == 0 ? 0 : current[i - 1];
						int leftXPrev = i == 0 ? 0 : prev[i - 1];
						int rightX = i == current.Count ? settings.TotalLength : current[i];
						int rightXPrev = i == prev.Count ? settings.TotalLength : prev[i];
						int fenceLength = rightX - leftX;

						Assert.IsTrue(fenceLength >= settings.MinFenceLength,
							$"Fence {i} too short: {fenceLength} < {settings.MinFenceLength}");
						Assert.IsTrue(fenceLength <= settings.MaxFenceLength,
							$"Fence {i} too long: {fenceLength} > {settings.MaxFenceLength}");

						if (fenceLength > settings.MaxFenceLength - 1)
						{
							longFences++;
						}
						if (fenceLength <= settings.MinFenceLength + 1)
						{
							shortFences++;
						}

						// Consider the fence having posts L and R.
						// If post L increases to (or beyond) the original position of post R there is no overlap.
						// If post R decreases to (or beyond) the original position of post L there is no overlap.
						// This must be disallowed; at least 1 space of overlap is required.
						Assert.IsTrue(leftX < rightXPrev);
						Assert.IsTrue(rightX > leftXPrev);
					}

					// Check MaxNudge constraint
					for (int i = 0; i < current.Count; i++)
					{
						totalShifts++;
						int nudgeAmount = Math.Abs(current[i] - prev[i]);
						Assert.IsTrue(nudgeAmount <= settings.MaxNudge,
							$"Post {i} nudged too far: {nudgeAmount} > {settings.MaxNudge}");

						if (nudgeAmount == 0)
						{
							zeroShifts++;
						}
					}

					// Check monotonic ordering
					for (int i = 1; i < current.Count; i++)
					{
						Assert.IsTrue(current[i] > current[i - 1],
							$"Posts not in order: {current[i]} <= {current[i - 1]} at index {i}");
					}

					shifter = new FencepostShifter(current.ToList(), settings);
					prev = current;
				}
			}

			// Statistical checks
			if (longFences > totalFences / 5)
			{
				Assert.Fail($"Too many near-maximum fences: {longFences} / {totalFences}");
			}

			if (shortFences > totalFences / 5)
			{
				Assert.Fail($"Too many near-minimum fences: {shortFences} / {totalFences}");
			}

			if (zeroShifts > totalShifts / 3)
			{
				Assert.Fail($"Too many zero shifts: {zeroShifts} / {totalShifts}");
			}

			if (zeroShifts < totalShifts / 100)
			{
				Assert.Fail($"Too few zero shifts: {zeroShifts} / {totalShifts}");
			}
		}

		private static (FencepostShifter, IReadOnlyList<int>) CreateFencepostShifter(PRNG prng, ref FencepostShifter.Settings settings)
		{
			var posts = new List<int>();
			int curr = 0;
			do
			{
				curr += prng.NextInt32(settings.MinFenceLength, settings.MaxFenceLength + 1);
				posts.Add(curr);
			} while (curr < 100);

			settings = settings with
			{
				TotalLength = curr + prng.NextInt32(settings.MinFenceLength, settings.MaxFenceLength + 1)
			};

			return (new FencepostShifter(posts, settings), posts);
		}
	}
}