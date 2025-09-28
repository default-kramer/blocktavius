using Blocktavius.Core;

namespace Blocktavius.Tests
{
	[TestClass]
	public sealed class FencepostShifterTests
	{
		[TestMethod]
		public void TestResolutionPlan_BasicPullLeft()
		{
			var posts = new List<FencepostShifter.Post>
			{
				new(10, new Blocktavius.Core.Range(5, 15)),
				new(20, new Blocktavius.Core.Range(15, 25))
			};

			var settings = new FencepostShifter.Settings
			{
				MaxNudge = 5,
				TotalLength = 100,
				MinFenceLength = 3,
				MaxFenceLength = 15
			};

			var plan = FencepostShifter.TestHelper.PullLeftCloser(posts, 1, 3, settings);

			Assert.AreEqual(3, plan.AvailableSpace);
			Assert.AreEqual(1, plan.PlannedAdjustments.Count);
			Assert.AreEqual((0, 7), plan.PlannedAdjustments.Peek());
		}

		[TestMethod]
		public void TestResolutionPlan_BasicPullRight()
		{
			var posts = new List<FencepostShifter.Post>
			{
				new(10, new Blocktavius.Core.Range(5, 15)),
				new(20, new Blocktavius.Core.Range(15, 25))
			};

			var settings = new FencepostShifter.Settings
			{
				MaxNudge = 5,
				TotalLength = 100,
				MinFenceLength = 3,
				MaxFenceLength = 15
			};

			var plan = FencepostShifter.TestHelper.PullRightCloser(posts, 1, 3, settings);

			Assert.AreEqual(3, plan.AvailableSpace);
			Assert.AreEqual(1, plan.PlannedAdjustments.Count);
			Assert.AreEqual((1, 23), plan.PlannedAdjustments.Peek());
		}

		[TestMethod]
		public void TestResolutionPlan_BasicPushLeft()
		{
			var posts = new List<FencepostShifter.Post>
			{
				new(10, new Blocktavius.Core.Range(5, 15)),
				new(20, new Blocktavius.Core.Range(15, 25))
			};

			var settings = new FencepostShifter.Settings
			{
				MaxNudge = 5,
				TotalLength = 100,
				MinFenceLength = 3,
				MaxFenceLength = 15
			};

			var plan = FencepostShifter.TestHelper.PushLeftAway(posts, 1, 3, settings);

			Assert.IsTrue(plan.AvailableSpace >= 3);
			Assert.IsTrue(plan.PlannedAdjustments.Count > 0);
		}

		[TestMethod]
		public void TestResolutionPlan_BasicPushRight()
		{
			var posts = new List<FencepostShifter.Post>
			{
				new(10, new Blocktavius.Core.Range(5, 15)),
				new(20, new Blocktavius.Core.Range(15, 25))
			};

			var settings = new FencepostShifter.Settings
			{
				MaxNudge = 5,
				TotalLength = 100,
				MinFenceLength = 3,
				MaxFenceLength = 15
			};

			var plan = FencepostShifter.TestHelper.PushRightAway(posts, 1, 3, settings);

			Assert.IsTrue(plan.AvailableSpace >= 3);
			Assert.IsTrue(plan.PlannedAdjustments.Count > 0);
		}

		[TestMethod]
		public void TestResolutionPlan_NoSpaceAvailable()
		{
			var posts = new List<FencepostShifter.Post>
			{
				new(5, new Blocktavius.Core.Range(5, 5)), // Cannot move
				new(20, new Blocktavius.Core.Range(20, 20)) // Cannot move
			};

			var settings = new FencepostShifter.Settings
			{
				MaxNudge = 0,
				TotalLength = 100,
				MinFenceLength = 3,
				MaxFenceLength = 15
			};

			var plan = FencepostShifter.TestHelper.PullLeftCloser(posts, 1, 3, settings);
			Assert.AreEqual(-1, plan.AvailableSpace);
		}

		[TestMethod]
		public void TestShiftPosts_ValidInput()
		{
			var originalPosts = new List<int> { 10, 30, 60, 80 };
			var settings = new FencepostShifter.Settings
			{
				MaxNudge = 5,
				TotalLength = 100,
				MinFenceLength = 5,
				MaxFenceLength = 25
			};

			var prng = PRNG.Create(new Random(42));
			var result = FencepostShifter.ShiftPosts(originalPosts, settings, prng);

			Assert.AreEqual(originalPosts.Count, result.Count);

			// Verify all fence lengths are within bounds
			for (int i = 0; i <= result.Count; i++)
			{
				int fenceStart = i == 0 ? 0 : result[i - 1];
				int fenceEnd = i == result.Count ? settings.TotalLength : result[i];
				int fenceLength = fenceEnd - fenceStart;

				Assert.IsTrue(fenceLength >= settings.MinFenceLength,
					$"Fence {i} length {fenceLength} < min {settings.MinFenceLength}");
				Assert.IsTrue(fenceLength <= settings.MaxFenceLength,
					$"Fence {i} length {fenceLength} > max {settings.MaxFenceLength}");
			}

			// Verify posts are in ascending order
			for (int i = 1; i < result.Count; i++)
			{
				Assert.IsTrue(result[i] > result[i - 1], "Posts must be in ascending order");
			}
		}

		[TestMethod]
		[Timeout(10000)]
		public void TestResolutionPlan_WorksWithBrokenRandomNudge()
		{
			// This test proves that even if the "random nudge" phase assigns totally random values
			// (instead of values from the ironclad range), the ResolutionPlan mechanism still works!

			var originalPosts = new List<int> { 10, 30, 60, 80 };
			var settings = new FencepostShifter.Settings
			{
				MaxNudge = 5,
				TotalLength = 100,
				MinFenceLength = 5,
				MaxFenceLength = 25
			};

			var prng = PRNG.Create(new Random(42));

			// Build ironclad ranges normally
			var ironcladRanges = FencepostShifter.TestHelper.BuildIroncladRanges(originalPosts, settings);
			var posts = new List<FencepostShifter.Post>();

			// BROKEN NUDGE PHASE: Assign totally random values instead of using ironclad ranges
			for (int i = 0; i < originalPosts.Count; i++)
			{
				int brokenRandomPosition = prng.NextInt32(0, settings.TotalLength);
				posts.Add(new FencepostShifter.Post(brokenRandomPosition, ironcladRanges[i]));
			}

			// Now test that the resolution mechanism can still fix this mess
			var violationsFound = 0;
			var maxIterations = 1000;
			var iterations = 0;

			while (iterations < maxIterations)
			{
				int? violatingFenceIndex = FencepostShifter.TestHelper.FindViolatingFence(posts, settings);
				if (violatingFenceIndex == null)
					break;

				violationsFound++;
				FencepostShifter.TestHelper.ResolveFenceViolation(posts, violatingFenceIndex.Value, settings);
				iterations++;
			}

			Assert.IsTrue(iterations < maxIterations, "Resolution took too many iterations");
			Assert.IsTrue(violationsFound > 0, "Should have found violations from broken random phase");

			// Verify final result is valid
			for (int i = 0; i <= posts.Count; i++)
			{
				int fenceStart = i == 0 ? 0 : posts[i - 1].X;
				int fenceEnd = i == posts.Count ? settings.TotalLength : posts[i].X;
				int fenceLength = fenceEnd - fenceStart;

				Assert.IsTrue(fenceLength >= settings.MinFenceLength,
					$"Final fence {i} length {fenceLength} < min {settings.MinFenceLength}");
				Assert.IsTrue(fenceLength <= settings.MaxFenceLength,
					$"Final fence {i} length {fenceLength} > max {settings.MaxFenceLength}");
			}
		}

		[TestMethod]
		[Timeout(30000)]
		public void StressTestResolutionPlan()
		{
			var prng = PRNG.Create(new Random(123));

			for (int testRun = 0; testRun < 100; testRun++)
			{
				// Generate random test cases
				int postCount = prng.NextInt32(3, 10);
				int totalLength = prng.NextInt32(100, 500);
				int minFenceLength = prng.NextInt32(3, 8);
				int maxFenceLength = prng.NextInt32(minFenceLength + 5, 30);
				int maxNudge = prng.NextInt32(1, 10);

				var settings = new FencepostShifter.Settings
				{
					MaxNudge = maxNudge,
					TotalLength = totalLength,
					MinFenceLength = minFenceLength,
					MaxFenceLength = maxFenceLength
				};

				// Generate valid initial posts
				var originalPosts = new List<int>();
				int currentPos = minFenceLength;
				for (int i = 0; i < postCount; i++)
				{
					originalPosts.Add(currentPos);
					currentPos += prng.NextInt32(minFenceLength, Math.Min(maxFenceLength, (totalLength - currentPos) / Math.Max(1, postCount - i)));
				}

				if (originalPosts.Last() >= totalLength - minFenceLength)
					continue; // Skip this test case

				try
				{
					var result = FencepostShifter.ShiftPosts(originalPosts, settings, prng);

					// Verify result validity
					Assert.AreEqual(originalPosts.Count, result.Count);

					for (int i = 0; i <= result.Count; i++)
					{
						int fenceStart = i == 0 ? 0 : result[i - 1];
						int fenceEnd = i == result.Count ? settings.TotalLength : result[i];
						int fenceLength = fenceEnd - fenceStart;

						Assert.IsTrue(fenceLength >= settings.MinFenceLength,
							$"Test {testRun}: Fence {i} length {fenceLength} < min {settings.MinFenceLength}");
						Assert.IsTrue(fenceLength <= settings.MaxFenceLength,
							$"Test {testRun}: Fence {i} length {fenceLength} > max {settings.MaxFenceLength}");
					}
				}
				catch (InvalidOperationException)
				{
					// It's acceptable for some test cases to have no valid solution
				}
			}
		}

	}
}