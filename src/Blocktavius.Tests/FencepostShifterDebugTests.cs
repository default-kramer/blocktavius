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

		[TestMethod]
		public void Debug_PullOperations()
		{
			// Let's test the Pull operations with specific known values
			var posts = new List<FencepostShifter.Post>
			{
				new(10, new Blocktavius.Core.Range(5, 15)),   // Post 0: has 5 units to move left, 5 to move right
				new(30, new Blocktavius.Core.Range(25, 35)),  // Post 1: has 5 units to move left, 5 to move right
				new(60, new Blocktavius.Core.Range(55, 65)),  // Post 2: has 5 units to move left, 5 to move right
				new(80, new Blocktavius.Core.Range(75, 85))   // Post 3: has 5 units to move left, 5 to move right
			};

			var settings = new FencepostShifter.Settings
			{
				MaxNudge = 5,
				TotalLength = 100,
				MinFenceLength = 5,
				MaxFenceLength = 25
			};

			Console.WriteLine("Posts and their movement capabilities:");
			for (int i = 0; i < posts.Count; i++)
			{
				var post = posts[i];
				int leftMove = post.X - post.IroncladRange.xMin;
				int rightMove = post.IroncladRange.xMax - post.X;
				Console.WriteLine($"Post {i}: X={post.X}, range=({post.IroncladRange.xMin}, {post.IroncladRange.xMax}), can move left={leftMove}, right={rightMove}");
			}

			Console.WriteLine("\nFence lengths:");
			for (int fenceIndex = 0; fenceIndex <= posts.Count; fenceIndex++)
			{
				int fenceStart = fenceIndex == 0 ? 0 : posts[fenceIndex - 1].X;
				int fenceEnd = fenceIndex == posts.Count ? settings.TotalLength : posts[fenceIndex].X;
				int fenceLength = fenceEnd - fenceStart;
				bool isValid = fenceLength >= settings.MinFenceLength && fenceLength <= settings.MaxFenceLength;
				Console.WriteLine($"Fence {fenceIndex}: {fenceStart} to {fenceEnd}, length = {fenceLength}, valid = {isValid}");
			}

			// Test pulling operations on fence 1 (between posts 0 and 1)
			Console.WriteLine("\nTesting Pull operations on fence 1:");

			var leftPlan = FencepostShifter.TestHelper.PullLeftCloser(posts, 1, 5, settings);
			Console.WriteLine($"PullLeftCloser(fence=1, space=5): AvailableSpace = {leftPlan.AvailableSpace}, Adjustments = {leftPlan.PlannedAdjustments.Count}");

			var rightPlan = FencepostShifter.TestHelper.PullRightCloser(posts, 1, 5, settings);
			Console.WriteLine($"PullRightCloser(fence=1, space=5): AvailableSpace = {rightPlan.AvailableSpace}, Adjustments = {rightPlan.PlannedAdjustments.Count}");

			// Debug the specific logic
			Console.WriteLine("\nDebugging PullLeftCloser logic:");
			int leftPostIndex = 1 - 1; // fenceIndex - 1 = 0
			Console.WriteLine($"leftPostIndex = {leftPostIndex}");
			if (leftPostIndex >= 0)
			{
				var leftPost = posts[leftPostIndex];
				int maxLeftMove = leftPost.X - leftPost.IroncladRange.xMin;
				Console.WriteLine($"leftPost.X = {leftPost.X}, leftPost.IroncladRange.xMin = {leftPost.IroncladRange.xMin}");
				Console.WriteLine($"maxLeftMove = {maxLeftMove}");

				if (maxLeftMove > 0)
				{
					int spaceAvailable = Math.Min(maxLeftMove, 5);
					Console.WriteLine($"spaceAvailable = Math.Min({maxLeftMove}, 5) = {spaceAvailable}");
				}
			}
		}
	}
}