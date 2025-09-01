using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core.Generators.BasicHill;

public sealed class SimpleSlope : I2DSampler<int>
{
	public int Width { get; init; }
	public int Elevation { get; init; }

	public Rect Bounds => new Rect(new XZ(0, 0), new XZ(Width, Elevation));

	public int Sample(XZ xz)
	{
		if (Bounds.Contains(xz))
		{
			return Elevation - xz.Z;
		}
		return -1;
	}
}

sealed class Contour
{
	internal readonly IReadOnlyList<int> offsets;

	internal Contour(IReadOnlyList<int> offsets)
	{
		this.offsets = offsets;
	}

	public static Contour BuildContour(PRNG prng, int minLength)
	{
		const int minOffset = 0;
		const int maxOffset = 5;
		int offset = 0;
		List<int> offsets = new();

		while (minLength > 0)
		{
			if (offset == maxOffset)
			{
				offset--;
			}
			else if (offset == minOffset)
			{
				offset++;
			}
			else
			{
				int delta = prng.NextInt32(2) == 0 ? -1 : 1;
				offset += delta;
			}

			int runLength = 3 + prng.NextInt32(6);
			offsets.AddRange(Enumerable.Repeat(offset, runLength));
			minLength -= runLength;
		}

		return new Contour(offsets);
	}
}

sealed class Layer
{
	internal record struct LayerCell(int offset, int Y, int? corner);

	internal readonly LayerCell[] cells;
	public bool IsEmpty { get; }
	public Rect BoundingBox { get; }

	private Layer(LayerCell[] cells)
	{
		this.cells = cells;

		IsEmpty = true;
		int maxOffset = 0;

		foreach (var cell in cells)
		{
			if (cell.Y > 0)
			{
				IsEmpty = false;
				maxOffset = Math.Max(maxOffset, cell.offset);
			}
		}

		BoundingBox = new Rect(new XZ(0, 0), new XZ(cells.Length, maxOffset + 1));
	}

	internal static Layer FromContour(PRNG prng, Contour contour, int Y) => FromContour(prng, contour.offsets, Y);

	internal static Layer FromContour(PRNG prng, IReadOnlyList<int> offsets, int Y)
	{
		int minY = Y - 2;
		int maxY = Y + 2;
		const int runLengthMin = 2;
		const int runLengthRand = 4;

		var cells = new LayerCell[offsets.Count];
		int runStart = 0;
		int retryCounter = 0;

		while (runStart < offsets.Count)
		{
			int runEnd = runStart + runLengthMin + prng.NextInt32(runLengthRand);
			runEnd = Math.Min(runEnd, offsets.Count);

			// Don't align the end of a run where the offset changes, for aesthetic reasons
			var runOkay = () => runEnd == offsets.Count || offsets[runEnd - 1] == offsets[runEnd];

			if (!runOkay())
			{
				retryCounter++;
				if (retryCounter > 50)
				{
					throw new Exception("TODO - is this a logic fail or some really bad luck with the PRNG?");
				}
			}
			else
			{
				int dY;
				if (Y == minY)
				{
					dY = 1;
				}
				else if (Y == maxY)
				{
					dY = -1;
				}
				else
				{
					dY = prng.NextInt32(2) == 0 ? -1 : 1;
				}
				int prevOffset = offsets[runStart];

				for (int i = runStart; i < runEnd; i++)
				{
					int offset = offsets[i];
					int adjustedY = Y + dY - offset; // aesthetic - decrease Y by 1 per offset

					// This cornering logic assumes that the contour never changes by more than 1 inward/outward step at a time.
					int? corner = null;
					if ((i - 1) >= 0 && offset < offsets[i - 1])
					{
						corner = adjustedY - 1; // this offset < prev offset; we have just taken a step inwards
					}
					else if ((i + 1) < offsets.Count && offset < offsets[i + 1])
					{
						corner = adjustedY - 1; // this offset < next offset; we are about to take a step outwards
					}

					cells[i] = new LayerCell(offset, adjustedY, corner);
				}

				// accept this outcome and advance:
				runStart = runEnd;
				Y = Y + dY;
				retryCounter = 0;
			}
		}

		return new Layer(cells);
	}

	public Contour NextContour()
	{
		var offsets = this.cells.Select(c => c.corner.HasValue ? c.offset + 2 : c.offset + 1).ToList();
		return new Contour(offsets);
	}
}

sealed class Wall : I2DSampler<int>
{
	private readonly I2DSampler<int> sampler;
	public Rect Bounds => sampler.Bounds;

	const int empty = -1; // use Y=-1 as an empty value

	private Wall(IReadOnlyList<Layer> layers)
	{
		var box = Rect.Union(layers.Select(l => l.BoundingBox));
		var array = new MutableArray2D<int>(box, empty);

		for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
		{
			var layer = layers[layerIndex];
			for (int i = 0; i < layer.cells.Length; i++)
			{
				var cell = layer.cells[i];
				if (cell.Y > 0)
				{
					var xz = new XZ(i, cell.offset);
					if (array.Sample(xz) == empty) // may already be set by a corner
					{
						array.Put(xz, cell.Y);
					}
					if (layerIndex < layers.Count - 1 && cell.corner.HasValue && cell.corner.Value > 0)
					{
						array.Put(xz.Add(0, 1), cell.corner.Value);
					}
				}
			}
		}

		sampler = array;
	}

	public int Sample(XZ xz) => sampler.Sample(xz);

	public static Wall BuildWall(PRNG prng, Contour contour, int Y)
	{
		var layers = new List<Layer>();
		bool done = false;
		while (!done)
		{
			var layer = Layer.FromContour(prng, contour, Y);
			if (layer.IsEmpty)
			{
				done = true;
			}
			else
			{
				layers.Add(layer);
				Y -= 5;
				contour = layer.NextContour();
			}
		}

		return new Wall(layers);
	}
}

public sealed class BasicHillGenerator : I2DSampler<int>
{
	private readonly Wall wall;

	private BasicHillGenerator(Wall wall)
	{
		this.wall = wall;
	}

	public Rect Bounds => wall.Bounds;
	public int Sample(XZ xz) => wall.Sample(xz);

	public static BasicHillGenerator Create(PRNG prng)
	{
		var contour = Contour.BuildContour(prng, 50);
		var wall = Wall.BuildWall(prng, contour, 30);
		return new BasicHillGenerator(wall);
	}
}
