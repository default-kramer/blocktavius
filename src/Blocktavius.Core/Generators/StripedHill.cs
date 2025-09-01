using System;
using System.Collections.Generic;

namespace Blocktavius.Core.Generators;

public sealed class StripedHill : I2DSampler<int>
{
	private readonly PRNG prng;
	private readonly int width;
	private readonly List<Cell[]> rows = new();

	private readonly Config config;

	public record Config
	{
		public int YMax { get; init; } = 30;
		public int YMin { get; init; } = 1;

		// For each Y, we must cover at least N% of the total width
		public decimal MinCoveragePerY { get; init; } = 0.3m;

		// For each Y, we must cover at most N% of the total width
		public decimal MaxCoveragePerY { get; init; } = 0.6m;

		public int MinStripeWidth { get; init; } = 3;
		public int MaxStripeWidth { get; init; } = 10;

		public Func<int, int> MaxZForYProcessed { get; init; } = y => 1000; // unnecessary now?
	}

	private StripedHill(PRNG prng, int width, Config config)
	{
		this.prng = prng;
		this.width = width;
		this.config = config;
	}

	readonly record struct Cell(int Y)
	{
		public bool HasData => Y > 0;
	}

	public static StripedHill Create(PRNG prng, int width, Config config)
	{
		var hill = new StripedHill(prng, width, config);
		hill.Generate();
		return hill;
	}

	// FUTURE: make this a decorator that can work on any ISampler2D<int> right?
	public void Smooth(int radius = 1)
	{
		var newRows = new List<Cell[]>();
		for (int z = 0; z < rows.Count; z++)
		{
			var newRow = new Cell[width];
			for (int x = 0; x < width; x++)
			{
				int ySum = 0;
				int yCount = 0;
				for (int i = -radius; i <= radius; i++)
				{
					int currentX = x + i;
					if (currentX >= 0 && currentX < width)
					{
						var cell = GetCell(currentX, z);
						if (cell.HasData)
						{
							ySum += cell.Y;
							yCount++;
						}
					}
				}

				if (yCount > 0)
				{
					newRow[x] = new Cell(ySum / yCount);
				}
			}
			newRows.Add(newRow);
		}

		for (int z = 0; z < rows.Count; z++)
		{
			rows[z] = newRows[z];
		}
	}

	private void Generate()
	{
		for (int y = config.YMax; y >= config.YMin; y--)
		{
			var coveredX = new bool[width];
			int coveredCount = 0;

			int minCoverageCount = (int)(width * config.MinCoveragePerY);
			int maxCoverageCount = (int)(width * config.MaxCoveragePerY);
			int targetCoverage = prng.NextInt32(minCoverageCount, maxCoverageCount + 1);

			int attempts = 0;
			const int maxAttempts = 50;

			while (coveredCount < targetCoverage && attempts < maxAttempts)
			{
				int stripeWidth = prng.NextInt32(config.MinStripeWidth, config.MaxStripeWidth + 1);
				if (stripeWidth > width) stripeWidth = width;

				var minZValues = new int[width];
				int maxZThisStep = 0;
				for (int i = 0; i < width; i++)
				{
					minZValues[i] = FindMinimumUnoccupiedZForX(i);
					if (minZValues[i] > maxZThisStep)
					{
						maxZThisStep = minZValues[i];
					}
				}

				var deepXCoords = new List<int>();
				for (int i = 0; i < width; i++)
				{
					if (minZValues[i] <= maxZThisStep - 4)
					{
						deepXCoords.Add(i);
					}
				}

				int startX;
				if (deepXCoords.Count > 0)
				{
					int randomDeepX = deepXCoords[prng.NextInt32(0, deepXCoords.Count)];
					startX = randomDeepX - stripeWidth / 2;
					if (startX < 0) startX = 0;
					if (startX + stripeWidth >= width) startX = width - stripeWidth;
				}
				else
				{
					startX = prng.NextInt32(0, width - stripeWidth + 1);
				}

				int endX = startX + stripeWidth;

				int yProcessed = config.YMax - y;
				int maxZ = config.MaxZForYProcessed(yProcessed);

				bool placedAnyInStripe = false;
				for (int x = startX; x < endX; x++)
				{
					int z = FindMinimumUnoccupiedZForX(x);
					if (z <= maxZ)
					{
						var row = GetRow(z);
						row[x] = new Cell(y);
						if (!coveredX[x])
						{
							coveredX[x] = true;
							coveredCount++;
						}
						placedAnyInStripe = true;
					}
				}

				if (placedAnyInStripe)
				{
					attempts = 0;
				}
				else
				{
					attempts++;
				}
			}
		}
	}

	private int FindMinimumUnoccupiedZForX(int x)
	{
		int z = 0;
		while (true)
		{
			if (!GetCell(x, z).HasData)
			{
				return z;
			}
			z++;
		}
	}

	private Cell GetCell(int x, int z)
	{
		if (z >= rows.Count)
		{
			return default;
		}
		return rows[z][x];
	}

	private Cell[] GetRow(int z)
	{
		while (rows.Count <= z)
		{
			rows.Add(new Cell[width]);
		}
		return rows[z];
	}

	public Rect Bounds => new Rect(new XZ(0, 0), new XZ(width, rows.Count));

	public int Sample(XZ xz)
	{
		if (Bounds.Contains(xz))
		{
			return rows[xz.Z][xz.X].Y;
		}
		return -1;
	}
}