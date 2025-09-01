using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core.Generators.BasicHill;

public sealed class BasicHill2 : I2DSampler<int>
{
	private readonly PRNG prng;
	private readonly List<Cell[]> rows = new(); // rows[0] implies Z=0, etc...
	private readonly int width;

	private BasicHill2(PRNG prng, int width)
	{
		this.prng = prng;
		this.width = width;
	}

	readonly record struct Cell(int Y)
	{
		public bool HasData => Y > 0;
	}

	// The contour is the southernmost (max) Z coordinate for each X coordinate.
	readonly record struct ContourCell(int X, int Z, int Y, int runLength, int runEnd);

	public static I2DSampler<int> Create(PRNG prng, int width)
	{
		// Generate a very tall hill, then clamp it down to size.
		// This technique allows the RNG more time to do its thing.
		// Maybe not the most efficient, but it looks pretty good at least.

		var hill = new BasicHill2(prng, width);
		hill.Init(height: 300);
		hill.Loop();
		hill.Clamp(maxY: 30);
		return hill;
	}

	private void Clamp(int maxY)
	{
		foreach (var row in rows)
		{
			for (int x = 0; x < width; x++)
			{
				var cell = row[x];
				if (cell.Y > maxY)
				{
					row[x] = cell with { Y = maxY };
				}
			}
		}
	}

	private void Init(int height)
	{
		var row = GetRow(0);
		for (int x = 0; x < width; x++)
		{
			row[x] = new Cell(height);
		}
	}

	/// <summary>
	/// Mutation: Recomputes the run lengths and writes them to the given <paramref name="contour"/>.
	/// </summary>
	private static void ComputeRunLengths(ContourCell[] contour)
	{
		int width = contour.Length;
		int x = 0;
		while (x < width)
		{
			int runEnd;
			int z = contour[x].Z;

			for (runEnd = x; runEnd < width; runEnd++)
			{
				if (contour[runEnd].Z != z)
				{
					break;
				}
			}

			int runLength = runEnd - x;
			for (int temp = x; temp < runEnd; temp++)
			{
				contour[temp] = contour[temp] with { runLength = runLength, runEnd = runEnd };
			}
			x = runEnd;
		}
	}

	/// <summary>
	/// No mutation: Derives the current contour from the cell data.
	/// </summary>
	private ContourCell[] GetCurrentContour()
	{
		const int notInitialized = 0; // it will be computed later

		var contour = new ContourCell[width];

		for (int x = 0; x < width; x++)
		{
			bool found = false;
			for (int z = rows.Count - 1; z >= 0 && !found; z--)
			{
				var cell = rows[z][x];
				if (cell.HasData)
				{
					contour[x] = new ContourCell(x, z, cell.Y, notInitialized, notInitialized);
					found = true;
				}
			}

			if (!found)
			{
				throw new Exception($"Failed to build contour! Nothing at x={x}");
			}
		}

		ComputeRunLengths(contour);
		return contour;
	}

	private Cell[] GetRow(int z)
	{
		while (rows.Count <= z)
		{
			rows.Add(new Cell[width]);
		}
		return rows[z];
	}

	/// <summary>
	/// If the current contour has any runs which are too straight, add a random bump.
	/// </summary>
	bool MaybeAddBump(ContourCell[] contour)
	{
		const int minBumpWidth = 3;
		const int minBumpOffset = 2;
		const int couldAddBump = minBumpWidth + minBumpOffset + minBumpOffset;
		int tooLong = Math.Max(couldAddBump, 8);

		int runStart = 0;
		while (runStart < contour.Length)
		{
			var BLAH = contour[runStart];
			if (BLAH.runLength >= tooLong)
			{
				int bumpWidth = prng.NextInt32(BLAH.runLength - couldAddBump) + minBumpWidth;
				int bumpStart = runStart + minBumpOffset + prng.NextInt32(BLAH.runLength - (bumpWidth + minBumpOffset + minBumpOffset));
				int bumpY = contour.Skip(bumpStart).Take(bumpWidth).Min(cell => cell.Y) - 1;

				if (bumpY > 0)
				{
					var row = GetRow(BLAH.Z + 1);
					for (int bump = bumpStart; bump <= bumpStart + bumpWidth; bump++)
					{
						row[bump] = new Cell(bumpY);
					}
					return true;
				}
			}

			runStart = runStart + BLAH.runLength;
		}

		return false;
	}

	bool AddLayer(ContourCell[] contour)
	{
		int prevMinY = contour.Min(cell => cell.Y);

		int maxY = prevMinY - 2;
		int minY = maxY - 3;
		int currentY = (maxY + minY) / 2;
		if (maxY <= 0)
		{
			return false;
		}

		int runStart = 0;
		while (runStart < width)
		{
			int runEnd = runStart + prng.NextInt32(4) + 2;
			if (runEnd >= width)
			{
				runEnd = width;
			}
			else
			{
				var contourCell = contour[runEnd - 1];
				if (contourCell.runEnd == runEnd)
				{
					continue; // don't align the two runs, try RNG again
				}
			}

			int delta;
			if (currentY == maxY)
			{
				delta = -1;
			}
			else if (currentY == minY)
			{
				delta = 1;
			}
			else
			{
				delta = prng.RandomChoice(-1, 1);
			}

			currentY += delta;
			for (int x = runStart; x < runEnd; x++)
			{
				int z = contour[x].Z + 1;
				var row = GetRow(z);
				row[x] = new Cell(currentY);
			}
			runStart = runEnd;
		}

		// need a new contour
		contour = GetCurrentContour();

		// Always fill gaps of size 3 because if we don't, after "fill corners" we
		// will be left with a gap of size 1 which looks bad to me.
		const double always = double.MaxValue;
		FillGaps(contour, always, always, always, always, 0.4, 0.2, 0.08);

		// Use the contour from BEFORE the fill gaps operation.
		// Otherwise cornering might create a new undesirable gap.
		FillCorners(contour);

		return true;
	}

	private void FillCorners(ContourCell[] contour)
	{
		for (int x = 1; x < width - 1; x++)
		{
			var curr = contour[x];
			var prev = contour[x - 1];
			var next = contour[x + 1];
			bool prevStep = prev.Z > curr.Z && Math.Abs(prev.Y - curr.Y) < 3;
			bool nextStep = curr.Z < next.Z && Math.Abs(next.Y - curr.Y) < 3;

			if (prevStep || nextStep)
			{
				var row = GetRow(curr.Z + 1);
				if (row[x].HasData)
				{
					// already handled by FillGaps, leave it alone
				}
				else
				{
					row[x] = new Cell(curr.Y - 1);
				}
			}
		}
	}

	/// <summary>
	/// A "gap" is a run of the contour where both of its neighbors have a larger Z coordinate.
	/// In otherwords, a gap is inset relative to its neighbors.
	/// The <paramref name="weight"/> argument defines how likely a gap of size N-1 is to be filled.
	/// For example, if weight[2] is 0.75 that means gaps of size 3 should be filled 75% of the
	/// time subject to the PRNG.
	/// </summary>
	private void FillGaps(ContourCell[] contour, params double[] weight)
	{
		int looper = 0;
		while (looper < width)
		{
			var runStart = looper;
			var curr = contour[looper];
			looper = curr.runEnd; // immediately increment so we can `continue` whenever we want

			if (curr.runLength > weight.Length)
			{
				continue; // gap too big, never fill
			}

			bool isGap = true;
			if (runStart > 0)
			{
				var prev = contour[runStart - 1];
				isGap = isGap && prev.Z > curr.Z;
			}
			if (curr.runEnd < width)
			{
				var next = contour[curr.runEnd];
				isGap = isGap && next.Z > curr.Z;
			}

			if (!isGap)
			{
				continue;
			}

			double target = weight[curr.runLength - 1];
			if (prng.NextDouble() > target)
			{
				continue;
			}

			// fill the gap!
			var row = GetRow(curr.Z + 1);
			int y = curr.Y - 1;
			for (int x = runStart; x < curr.runEnd; x++)
			{
				row[x] = new Cell(y);
			}
		}
	}

	void Loop()
	{
		bool keepGoing = true;
		while (keepGoing)
		{
			var contour = GetCurrentContour();

			keepGoing = MaybeAddBump(contour)
				|| AddLayer(contour);
		}
	}

	public Rect Bounds => new Rect(new XZ(0, 0), new XZ(width, rows.Count));

	public int Sample(XZ xz)
	{
		if (Bounds.Contains(xz))
		{
			var row = rows[xz.Z];
			return row[xz.X].Y;
		}
		return -1;
	}
}
