
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core.Generators;

public static class CornerShifter
{
	public record Settings
	{
		public required int Width { get; init; }
		public required int MinRunLength { get; init; }
		public required int MaxRunLength { get; init; }
		public required int MaxDepth { get; init; }
		public required int MaxMatchingDirections { get; init; }
	}

	public sealed class Contour
	{
		public readonly int Width;
		public readonly IReadOnlyList<Corner> Corners;
		public readonly int Depth;

		public Contour(IReadOnlyList<Corner> corners, int width)
		{
			this.Width = width;
			this.Corners = corners;
			this.Depth = CalculateDepth(corners);
		}

		public static Contour Generate(PRNG prng, Settings settings) => GenerateInitialBackstop(prng, settings);

		private static int CalculateDepth(IEnumerable<Corner> corners)
		{
			int min = 0;
			int max = 0;
			int current = 0;

			foreach (var corner in corners)
			{
				current += corner.Dir.Step.Z;
				min = Math.Min(current, min);
				max = Math.Max(current, max);
			}

			return 1 + (max - min);
		}

		public Contour Shift(PRNG prng, Settings settings)
		{
			var next = ShiftCorners(prng, this, settings);
			return new Contour(next, this.Width);
		}
	}

	public record struct Corner(int X, Direction Dir);

	private static List<Corner> ShiftCorners(PRNG prng, Contour previous, Settings settings)
	{
		var prevCorners = previous.Corners;
		if (prevCorners.Count == 0) return new List<Corner>();

		var corners = prevCorners.ToList();
		
		// The test requires that at least one corner moves.
		// We will try to find a valid shift. If after many attempts we can't,
		// we will return the original list to avoid an infinite loop.
		for (int attempt = 0; attempt < 100; attempt++)
		{
			var tempCorners = prevCorners.ToList();
			bool changed = false;

			// Pick a random corner and a random amount to shift it by.
			int cornerIndex = prng.NextInt32(tempCorners.Count);
			int shiftAmount = prng.NextInt32(-6, 7);
			if (shiftAmount == 0) shiftAmount = 1; // Ensure some movement

			var provisionalX = tempCorners[cornerIndex].X + shiftAmount;

			// Clamp to world bounds
			provisionalX = Math.Max(0, Math.Min(settings.Width - 1, provisionalX));
			
			// Clamp to previous layer guides
			int minBoundRule3 = (cornerIndex > 0) ? prevCorners[cornerIndex - 1].X : 0;
			int maxBoundRule3 = (cornerIndex < prevCorners.Count - 1) ? prevCorners[cornerIndex + 1].X : settings.Width - 1;
			provisionalX = Math.Max(minBoundRule3, Math.Min(maxBoundRule3, provisionalX));

			if (provisionalX != tempCorners[cornerIndex].X)
			{
				changed = true;
			}
			tempCorners[cornerIndex] = tempCorners[cornerIndex] with { X = provisionalX };

			// Now, try to make the rest of the corners fit, propagating from our change.
			bool possible = true;

			// Propagate left
			for (int i = cornerIndex - 1; i >= 0; i--)
			{
				int neighborX = tempCorners[i + 1].X;
				int min = neighborX - settings.MaxRunLength;
				int max = neighborX - settings.MinRunLength;

				min = Math.Max(min, (i > 0) ? prevCorners[i - 1].X : 0);
				max = Math.Min(max, prevCorners[i + 1].X);

				if (min > max) { possible = false; break; }
				tempCorners[i] = tempCorners[i] with { X = prng.NextInt32(min, max + 1) };
			}
			if (!possible) continue;

			// Propagate right
			for (int i = cornerIndex + 1; i < tempCorners.Count; i++)
			{
				int neighborX = tempCorners[i - 1].X;
				int min = neighborX + settings.MinRunLength;
				int max = neighborX + settings.MaxRunLength;

				min = Math.Max(min, (i > 0) ? prevCorners[i - 1].X : 0);
				max = Math.Min(max, (i < tempCorners.Count - 1) ? prevCorners[i + 1].X : settings.Width - 1);

				if (min > max) { possible = false; break; }
				tempCorners[i] = tempCorners[i] with { X = prng.NextInt32(min, max + 1) };
			}
			if (!possible) continue;

			// If we made it here, the contour is valid.
			// But the test requires a change. If only the initial shift happened with no propagation,
			// it might be that other corners were not affected. Let's ensure the final result is different.
			if (!changed)
			{
				 for(int i = 0; i < corners.Count; i++)
				 {
					 if (corners[i].X != tempCorners[i].X)
					 {
						 changed = true;
						 break;
					 }
				 }
			}

			if (changed) return tempCorners;
		}

		// If we failed to find a valid changed contour after many attempts, return the original.
		return corners;
	}

	private static Contour GenerateInitialBackstop(PRNG prng, Settings config)
	{
		int width = config.Width;
		int runWidthMin = config.MinRunLength;
		int runWidthRand = config.MaxRunLength - config.MinRunLength;

		int minZ = 0;
		int maxZ = config.MaxDepth - 1;
		int z = prng.NextInt32(maxZ + 1);
		int x = 0;

		var points = new List<Corner>();
		while (x < width)
		{
			x = x + runWidthMin + prng.NextInt32(runWidthRand);
			if (x >= width)
			{
				break;
			}

			Direction dir;
			if (z == minZ)
			{
				dir = Direction.South;
			}
			else if (z == maxZ)
			{
				dir = Direction.North;
			}
			else
			{
				dir = prng.RandomChoice(Direction.North, Direction.South);
			}

			z += dir.Step.Z;
			points.Add(new Corner() { Dir = dir, X = x });
		}

		return new Contour(points, config.Width);
	}
}
