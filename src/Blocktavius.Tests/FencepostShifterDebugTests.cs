using Blocktavius.Core;

namespace Blocktavius.Tests
{
	[TestClass]
	public sealed class FencepostShifterDebugTests
	{
		[TestMethod]
		public void Debug_IroncladRanges()
		{
			var originalPosts = new List<int> { 10, 30, 60, 80 };
			var settings = new FencepostShifter.Settings
			{
				MaxNudge = 5,
				TotalLength = 100,
				MinFenceLength = 5,
				MaxFenceLength = 25
			};

			var ranges = FencepostShifter.TestHelper.BuildIroncladRanges(originalPosts, settings);

			for (int i = 0; i < ranges.Count; i++)
			{
				Console.WriteLine($"Post {i} (orig={originalPosts[i]}): Range({ranges[i].xMin}, {ranges[i].xMax})");
			}

			// Verify the valid solution you mentioned: {10, 35, 60, 80}
			var validSolution = new List<int> { 10, 35, 60, 80 };
			Console.WriteLine("\nTesting valid solution {10, 35, 60, 80}:");

			for (int fenceIndex = 0; fenceIndex <= validSolution.Count; fenceIndex++)
			{
				int fenceStart = fenceIndex == 0 ? 0 : validSolution[fenceIndex - 1];
				int fenceEnd = fenceIndex == validSolution.Count ? settings.TotalLength : validSolution[fenceIndex];
				int fenceLength = fenceEnd - fenceStart;

				Console.WriteLine($"Fence {fenceIndex}: {fenceStart} to {fenceEnd}, length = {fenceLength}");

				bool isValid = fenceLength >= settings.MinFenceLength && fenceLength <= settings.MaxFenceLength;
				Console.WriteLine($"  Valid: {isValid}");
			}
		}

		[TestMethod]
		public void Debug_BrokenRandomPositions()
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
			var ironcladRanges = FencepostShifter.TestHelper.BuildIroncladRanges(originalPosts, settings);
			var posts = new List<FencepostShifter.Post>();

			// Generate the same broken random positions as in the failing test
			for (int i = 0; i < originalPosts.Count; i++)
			{
				int brokenRandomPosition = prng.NextInt32(0, settings.TotalLength);
				posts.Add(new FencepostShifter.Post(brokenRandomPosition, ironcladRanges[i]));
				Console.WriteLine($"Post {i}: position = {brokenRandomPosition}, ironclad range = ({ironcladRanges[i].xMin}, {ironcladRanges[i].xMax})");
			}

			Console.WriteLine("\nInitial fence violations:");
			for (int fenceIndex = 0; fenceIndex <= posts.Count; fenceIndex++)
			{
				int fenceStart = fenceIndex == 0 ? 0 : posts[fenceIndex - 1].X;
				int fenceEnd = fenceIndex == posts.Count ? settings.TotalLength : posts[fenceIndex].X;
				int fenceLength = fenceEnd - fenceStart;

				bool isValid = fenceLength >= settings.MinFenceLength && fenceLength <= settings.MaxFenceLength;
				Console.WriteLine($"Fence {fenceIndex}: {fenceStart} to {fenceEnd}, length = {fenceLength}, valid = {isValid}");
			}

			// Test EnsurePostsAreValid
			FencepostShifter.TestHelper.EnsurePostsAreValid(posts);

			Console.WriteLine("\nAfter EnsurePostsAreValid:");
			for (int i = 0; i < posts.Count; i++)
			{
				Console.WriteLine($"Post {i}: position = {posts[i].X}, ironclad range = ({posts[i].IroncladRange.xMin}, {posts[i].IroncladRange.xMax})");
			}

			Console.WriteLine("\nFences after EnsurePostsAreValid:");
			for (int fenceIndex = 0; fenceIndex <= posts.Count; fenceIndex++)
			{
				int fenceStart = fenceIndex == 0 ? 0 : posts[fenceIndex - 1].X;
				int fenceEnd = fenceIndex == posts.Count ? settings.TotalLength : posts[fenceIndex].X;
				int fenceLength = fenceEnd - fenceStart;

				bool isValid = fenceLength >= settings.MinFenceLength && fenceLength <= settings.MaxFenceLength;
				Console.WriteLine($"Fence {fenceIndex}: {fenceStart} to {fenceEnd}, length = {fenceLength}, valid = {isValid}");
			}

			// Find first violation
			var violatingFenceIndex = FencepostShifter.TestHelper.FindViolatingFence(posts, settings);
			if (violatingFenceIndex.HasValue)
			{
				Console.WriteLine($"\nFirst violating fence: {violatingFenceIndex.Value}");
			}
			else
			{
				Console.WriteLine("\nNo violations found!");
			}
		}

		[TestMethod]
		public void Debug_ValidInputFailure()
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
			var ironcladRanges = FencepostShifter.TestHelper.BuildIroncladRanges(originalPosts, settings);
			var posts = new List<FencepostShifter.Post>();

			// Normal nudge phase (not broken)
			for (int i = 0; i < originalPosts.Count; i++)
			{
				var range = ironcladRanges[i];
				int nudgedPosition = range.RandomX(prng);
				posts.Add(new FencepostShifter.Post(nudgedPosition, range));
				Console.WriteLine($"Post {i}: position = {nudgedPosition}, ironclad range = ({range.xMin}, {range.xMax})");
			}

			Console.WriteLine("\nAfter normal nudging:");
			for (int fenceIndex = 0; fenceIndex <= posts.Count; fenceIndex++)
			{
				int fenceStart = fenceIndex == 0 ? 0 : posts[fenceIndex - 1].X;
				int fenceEnd = fenceIndex == posts.Count ? settings.TotalLength : posts[fenceIndex].X;
				int fenceLength = fenceEnd - fenceStart;

				bool isValid = fenceLength >= settings.MinFenceLength && fenceLength <= settings.MaxFenceLength;
				Console.WriteLine($"Fence {fenceIndex}: {fenceStart} to {fenceEnd}, length = {fenceLength}, valid = {isValid}");
			}

			// Find first violation
			var violatingFenceIndex = FencepostShifter.TestHelper.FindViolatingFence(posts, settings);
			if (violatingFenceIndex.HasValue)
			{
				Console.WriteLine($"\nFirst violating fence: {violatingFenceIndex.Value}");

				// Try to debug the resolution for this specific violation
				var fenceIndex = violatingFenceIndex.Value;
				int fenceStart = fenceIndex == 0 ? 0 : posts[fenceIndex - 1].X;
				int fenceEnd = fenceIndex == posts.Count ? settings.TotalLength : posts[fenceIndex].X;
				int fenceLength = fenceEnd - fenceStart;

				Console.WriteLine($"Trying to resolve fence {fenceIndex} with length {fenceLength}");

				if (fenceLength > settings.MaxFenceLength)
				{
					int excess = fenceLength - settings.MaxFenceLength;
					Console.WriteLine($"Excess: {excess}");

					var leftPlan = FencepostShifter.TestHelper.PullLeftCloser(posts, fenceIndex, excess, settings);
					var rightPlan = FencepostShifter.TestHelper.PullRightCloser(posts, fenceIndex, excess, settings);

					Console.WriteLine($"Left plan: AvailableSpace = {leftPlan.AvailableSpace}");
					Console.WriteLine($"Right plan: AvailableSpace = {rightPlan.AvailableSpace}");
				}
			}
			else
			{
				Console.WriteLine("\nNo violations found after normal nudging!");
			}
		}
	}
}