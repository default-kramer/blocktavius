using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Blocktavius.Core.Generators.Cliffs;

/// <remarks>
/// This algorithm operates by constructing layers one at a time.
/// The width of all layers is equal and constant.
/// Each successive layer will satisfy
/// * currentLayer[x].Y must be less than previousLayer[x].Y
/// * currentLayer[x].NorthernmostPoint.Z == 1 + previousLayer[x].SouthernmostPoint.Z
/// These goals are the main reason the <see cref="Backstop"/> class exists.
/// Also, we can create a starter backstop to generate the first layer
/// (which will have no previous layer).
///
/// But wait, there's more!
/// The backstop tends toward Z-flatness as more layers are added.
/// (This is a natural consequence of cornering.
///  Also <see cref="FillAlcoves"/> causes Z-flatness.)
/// So we may introduce "shims" to counteract this.
/// </remarks>
public sealed class QuaintCliff
{
	public sealed record Config
	{
		/// <summary>
		/// Minimum separation (range is inclusive).
		/// The "separation" is calculated for every X, how much the Y value (elevation)
		/// drops compared to the same X in the previous layer.
		/// The formula is `separation(N,X) = Layers[N-1][X].Y - Layers[N][X].Y`
		/// </summary>
		public int MinSeparation { get; init; } = 1;

		/// <summary>
		/// Maximum separation (range is inclusive).
		/// See <see cref="MinSeparation"/>.
		/// </summary>
		public int MaxSeparation { get; init; } = 4;

		/// <summary>
		/// Minimum run length.
		/// Layers are built one run at a time.
		/// Each run of a layer chooses a Y value (elevation) that is +1 or -1 from the previous run.
		/// (Note that Y also changes whenever Z changes, so it's more accurate to define
		///  "adjusted Y" = Y+Z and say that each run chooses an "adjusted Y" that is +1 or -1
		///  from the previous run.)
		/// </summary>
		public int RunWidthMin { get; init; } = 2;
		public int RunWidthMax { get; init; } = 5;
		internal int RunWidthRand => (RunWidthMax + 1) - RunWidthMin;

		/// <summary>
		/// When the contour (Z) stays unchanged for at least this many steps,
		/// we consider it "too flat" and add a shim to introduce some jaggedness.
		/// </summary>
		public int UnacceptableZFlatness { get; init; } = 10;

		public int ShimMinOffset { get; init; } = 2;
	}

	public enum Kind
	{
		Nothing,
		Normal,
		Shim,
		Backfill,
	}

	public record struct Item(int y, int layerId, Kind kind) : IHaveElevation
	{
		public int Y => y;
	}

	record struct Point(XZ xz, int y)
	{
		public bool Include => y > 0;
	}

	/// <summary>
	/// When dealing with corners, we will need to populate 2 Z coords at a given X coord.
	/// For example, here is how a run of width 5 with a corner at `run[2]` would look
	///    _ _ c x x
	///    x x x _ _
	///
	/// In that example, the cell at `run[2]` will have
	/// * bool corner: true
	/// * a Z coordinate matching the cells to its left
	/// * a special <see cref="NorthernmostPoint"/> for the "c" spot in the diagram above.
	/// </summary>
	record struct Cell(Point point, bool corner, int layerId)
	{
		private Point CornerPoint() => new Point(point.xz.Add(0, -1), point.y + 1);

		public IEnumerable<Point> Points()
		{
			yield return point;
			if (corner)
			{
				yield return CornerPoint();
			}
		}

		public Point SouthernmostPoint => point;

		public Point NorthernmostPoint => corner ? CornerPoint() : point;

		public Cell ConvertToCorner()
		{
			if (corner)
			{
				throw new Exception("assert fail - already a corner");
			}
			var xz = this.point.xz.Add(0, 1);
			return new Cell(new Point(xz, this.point.y - 1), true, layerId);
		}
	}

	/// <summary>
	/// "Generate next layer" is almost* a function of the backstop.
	/// (* "almost", because we also use the shared PRNG and the width constant.)
	/// </summary>
	sealed class Backstop
	{
		private readonly IReadOnlyList<Point> points;

		public Backstop(IReadOnlyList<Point> points)
		{
			this.points = points;
		}

		private bool Rejects(Point point, Config config)
		{
			var north = this.points[point.xz.X];
			if (point.xz.Z != north.xz.Z + 1)
			{
				throw new ArgumentException("given point is not immediately south of backstop");
			}
			int separation = north.y - point.y;
			return separation < config.MinSeparation || separation > config.MaxSeparation;
		}

		/// <summary>
		/// Don't end a run where the Z coordinate changes for aesthetic reasons.
		/// (And maybe for correctness too??)
		/// </summary>
		public bool CanEndRunAt(int xEnd)
		{
			if (xEnd == points.Count)
			{
				return true;
			}
			if (xEnd > points.Count)
			{
				return false;
			}
			return points[xEnd].xz.Z == points[xEnd - 1].xz.Z;
		}

		/// <summary>
		/// Returns all possible Y values that the first cell of the next layer could start at
		/// </summary>
		public IEnumerable<int> InitialYChoices(Config config)
		{
			var anchor = this.points[0];
			int y = anchor.y;
			var xz = anchor.xz.Add(0, 1);

			int yMin = anchor.y - config.MaxSeparation;
			while (y >= yMin)
			{
				var test = new Point(xz, y);
				if (!Rejects(test, config))
				{
					yield return y;
				}
				y--;
			}
		}

		public bool GetCellForNextLayer(Config config, int x, ref int y, int layerId, out Cell cell)
		{
			var north = points[x];

			bool drop = false;
			bool raise = false;

			if (x < points.Count - 1)
			{
				var nextNorth = points[x + 1];
				if (nextNorth.xz.Z > north.xz.Z)
				{
					drop = true; // step out and drop Y
				}
			}

			if (x > 0)
			{
				var prevNorth = points[x - 1];
				if (prevNorth.xz.Z > north.xz.Z)
				{
					raise = true; // step in and raise Y
				}
			}

			if (drop && raise)
			{
				throw new Exception("assert fail - cannot drop and raise!");
			}

			// Corner cells expect to be given the southern of the two points.
			// So whether we are stepping in or out, we need to increase Z by 2.
			var xz = (drop || raise) ? north.xz.Add(0, 2) : north.xz.Add(0, 1);
			if (drop)
			{
				y--;
				cell = new Cell(new Point(xz, y), true, layerId);
			}
			else if (raise)
			{
				int thisY = y; // Cell wants the southern point, before we increment Y
				y++;
				cell = new Cell(new Point(xz, thisY), true, layerId);
			}
			else
			{
				cell = new Cell(new Point(xz, y), false, layerId);
			}

			return !Rejects(cell.NorthernmostPoint, config);
		}

		public bool TryAddShim(PRNG prng, Config config, int layerId, out Backstop backstop, out Shim shim)
		{
			if (config.UnacceptableZFlatness < 1)
			{
				backstop = default!;
				shim = default!;
				return false;
			}

			foreach (var zFlatness in GetZFlatnesses().Where(zF => zF.width >= config.UnacceptableZFlatness))
			{
				var best = new List<Point>();
				int x = zFlatness.start.X + config.ShimMinOffset;
				int shimEndLimit = zFlatness.xEnd - config.ShimMinOffset;
				while (x < shimEndLimit)
				{
					var start = points[x];
					var seq = points.Skip(x).TakeWhile(p => p.y == start.y && p.xz.X < shimEndLimit).ToList();
					// longest wins, taller Y is tiebreaker
					if (seq.Count > best.Count || (seq.Count == best.Count && seq[0].y > best[0].y))
					{
						best = seq;
					}
					x += seq.Count;
				}

				if (best.Count > 0)
				{
					// Decreasing Y here can cause the algorithm to get stuck, so we use the same Y.
					// (IMO, this also looks better than decreasing Y.)
					// But if you wanted, you could lower all shims as a post-processing step...
					shim = new Shim()
					{
						Start = best[0].xz.Add(0, 1),
						Width = best.Count,
						Y = best[0].y,
						LayerId = layerId,
					};

					var nextPoints = this.points.ToList();
					foreach (var point in shim.Points)
					{
						nextPoints[point.xz.X] = point;
					};
					backstop = new Backstop(nextPoints);

					return true;
				}
			}

			backstop = default!;
			shim = default!;
			return false;
		}

		private IEnumerable<ZFlatness> GetZFlatnesses()
		{
			int xStart = 0;
			while (xStart < points.Count)
			{
				int xEnd = xStart;
				var start = points[xStart];
				int minY = start.y;

				while (xEnd < points.Count && start.xz.Z == points[xEnd].xz.Z)
				{
					minY = Math.Min(minY, points[xEnd].y);
					xEnd++;
				}

				yield return new ZFlatness(start.xz, xEnd, minY);
				xStart = xEnd;
			}
		}

		record struct ZFlatness(XZ start, int xEnd, int minY)
		{
			public int width => xEnd - start.X;
		}
	}

	sealed class Layer
	{
		public required int LayerId { get; init; }
		private readonly IReadOnlyList<Cell> cells;

		public Layer(IReadOnlyList<Cell> cells)
		{
			this.cells = cells;
		}

		public bool HasData => cells.Any(cell => cell.point.y > 0);

		public Backstop ToBackstop()
		{
			var points = this.cells.Select(cell => cell.SouthernmostPoint).ToList();
			return new Backstop(points);
		}

		public int MaxZ => cells.Select(cell => cell.SouthernmostPoint.xz.Z).Max();

		public IEnumerable<Point> Points => cells.SelectMany(cell => cell.Points());
	}

	sealed class Shim
	{
		public required XZ Start { get; init; }
		public required int Width { get; init; }
		public required int Y { get; init; }
		public required int LayerId { get; init; }

		public int xEnd => Start.X + Width;
		public IEnumerable<Point> Points => Enumerable.Range(0, Width).Select(i => new Point(Start.Add(i, 0), Y));
	}

	private readonly PRNG prng;
	private readonly int width;
	private readonly Config config;

	private QuaintCliff(PRNG prng, int width)
	{
		this.prng = prng;
		this.width = width;
		this.config = new Config();
	}

	public static I2DSampler<Item> Generate(PRNG prng, int width, int height)
	{
		var hill = new QuaintCliff(prng, width);
		var (layers, shims) = hill.BuildLayers(height);

		var allPoints = layers.SelectMany(l => l.Points)
			.Concat(shims.SelectMany(s => s.Points))
			.Where(p => p.Include);

		int zEnd = 1 + allPoints.Max(p => p.xz.Z);

		var box = new Rect(new XZ(0, 0), new XZ(width, zEnd));
		var array = new MutableArray2D<Item>(box, new Item(-1, -1, Kind.Nothing));

		foreach (var layer in layers)
		{
			foreach (var point in layer.Points.Where(p => p.Include))
			{
				array.Put(point.xz, new Item(point.y, layer.LayerId, Kind.Normal));
			}
		}

		foreach (var shim in shims)
		{
			foreach (var point in shim.Points.Where(p => p.Include))
			{
				array.Put(point.xz, new Item(point.y, shim.LayerId, Kind.Shim));
			}
		}

		for (int x = 0; x < width; x++)
		{
			var xz = new XZ(x, 0);
			while (array.Sample(xz).y < 0)
			{
				array.Put(xz, new Item(height, -42, Kind.Backfill));
				xz = xz.Add(0, 1);
			}
		}

		return array;
	}

	private (List<Layer>, List<Shim>) BuildLayers(int height)
	{
		var layers = new List<Layer>();
		var shims = new List<Shim>();

		// We increment layerId *before* generating the layer
		// so that it remains correct for any shims that may follow.
		int layerId = -1;

		var backstop = GenerateInitialBackstop(height + config.MinSeparation);
		var layer = GenerateLayer(backstop, ++layerId);

		while (layer.HasData)
		{
			layers.Add(layer);
			backstop = layer.ToBackstop();
			while (backstop.TryAddShim(prng, config, layerId, out var nextBackstop, out var shim))
			{
				shims.Add(shim);
				backstop = nextBackstop;
			}
			layer = GenerateLayer(backstop, ++layerId);
		}

		return (layers, shims);
	}

	private Backstop GenerateInitialBackstop(int y)
	{
		// This backstop is not part of the hill.
		// It is only used to constrain the first layer.
		// So we build this backstop at Z=-1 so that the first layer will end up at Z=0.
		const int SHIFT = -1;

		const int minZ = 0 + SHIFT; // inclusive
		const int maxZ = 4 + SHIFT; // inclusive
		int z = prng.NextInt32(maxZ + 1);
		int x = 0;

		var points = new List<Point>();
		while (x < width)
		{
			int xEnd = x + config.RunWidthMin + prng.NextInt32(config.RunWidthRand);
			xEnd = Math.Min(xEnd, width);

			for (; x < xEnd; x++)
			{
				points.Add(new Point(new XZ(x, z), y));
			}

			int dz;
			if (z == minZ)
			{
				dz = 1;
			}
			else if (z == maxZ)
			{
				dz = -1;
			}
			else
			{
				dz = prng.RandomChoice(-1, 1);
			}

			z += dz;
		}

		if (points.Count != width)
		{
			throw new Exception("assert fail");
		}

		// back that thing up if needed
		int backup = points.Select(x => x.xz.Z - SHIFT).Min();
		if (backup > 0)
		{
			points = points.Select(p => new Point(p.xz.Add(0, -backup), p.y)).ToList();
		}

		return new Backstop(points);
	}

	private Layer GenerateLayer(Backstop backstop, int layerId)
	{
		var buffer = new Cell[width];
		var shared = new Shared(buffer)
		{
			config = config,
			backstop = backstop,
			LayerId = layerId,
			prng = prng,
			legalRunLengths = Enumerable.Range(config.RunWidthMin, config.RunWidthRand).ToImmutableSortedSet(),
		};

		var yChoices = backstop.InitialYChoices(config).OrderBy(x => prng.NextDouble()).ToList();

		foreach (int y in yChoices)
		{
			bool okay = new LayerGenerator(shared, 0, y).Execute();
			if (okay)
			{
				FillAlcoves(buffer);
				return new Layer(buffer)
				{
					LayerId = layerId
				};
			}
		}

		throw new Exception($"failed to generate layer! tried y=[{string.Join(',', yChoices)}]");
	}

	/// <summary>
	/// An "alcove" is an inset gap.
	/// For example, here is what an alcove having <paramref name="alcoveWidth"/> 2 looks like:
	///    _ _ _ _ c x x c _ _ _ _
	///    x x x x x _ _ x x x x x
	///
	/// It is *essential* that we fill alcoves having width 2 or less.
	/// Otherwise we might get stuck later on.
	/// We will replace alcove cells with corner cells.
	/// So the example above would get replaced by this:
	///    _ _ _ _ c c c c _ _ _ _
	///    x x x x x x x x x x x x
	/// </summary>
	private static bool IsAlcove(IReadOnlyList<Cell> cells, int start, int alcoveWidth)
	{
		int zStart = cells[start].SouthernmostPoint.xz.Z;

		int end = start + alcoveWidth + 1;
		if (end >= cells.Count)
		{
			return false;
		}

		int zEnd = cells[end].SouthernmostPoint.xz.Z;
		if (zStart != zEnd)
		{
			return false;
		}

		for (int i = 1; i <= alcoveWidth; i++)
		{
			if (cells[start + i].SouthernmostPoint.xz.Z >= zStart)
			{
				return false;
			}
		}

		return true;
	}

	private static void FillAlcoves(Cell[] buffer)
	{
		for (int x = 0; x < buffer.Length; x++)
		{
			for (int alcoveWidth = 2; alcoveWidth >= 1; alcoveWidth--)
			{
				if (IsAlcove(buffer, x, alcoveWidth))
				{
					for (int i = 1; i <= alcoveWidth; i++)
					{
						buffer[x + i] = buffer[x + i].ConvertToCorner();
					}
				}
			}
		}
	}

	sealed class Shared
	{
		public required Config config { get; init; }
		public required PRNG prng { get; init; }
		public required Backstop backstop { get; init; }
		public required IImmutableSet<int> legalRunLengths { get; init; }
		public required int LayerId { get; init; }
		public int Width => Buffer.Length;

		private readonly Cell[] Buffer;
		public Shared(Cell[] buffer)
		{
			this.Buffer = buffer;
		}

		public Span<Cell> GetWritableBuffer(int xStart, int runLength)
		{
			return Buffer.AsSpan().Slice(xStart, runLength);
		}
	}

	/// <summary>
	/// Holds the state needed to generate a single run.
	/// If we succeed, we recurse.
	/// If we cannot, we backtrack to an earlier state and rely on the mutable state
	/// of the shared PRNG to explore other possible recursions.
	/// </summary>
	record struct LayerGenerator(Shared shared, int xStart, int y)
	{
		public bool Execute()
		{
			if (xStart >= shared.Width)
			{
				return true;
			}

			var runLengths = shared.legalRunLengths;

			for (int i = 0; i < 5; i++)
			{
				if (!ChooseRandomRunLength(ref runLengths, out int runLength))
				{
					return false; // no valid run lengths remain
				}
				if (GenerateRunRecursive(runLength))
				{
					return true;
				}
			}

			return false;
		}

		private bool ChooseRandomRunLength(ref IImmutableSet<int> runLengths, out int runLength)
		{
			while (runLengths.Any())
			{
				runLength = shared.prng.RandomChoice(runLengths.ToArray());
				if (shared.backstop.CanEndRunAt(xStart + runLength))
				{
					return true;
				}
				else
				{
					runLengths = runLengths.Remove(runLength);
				}
			}

			runLength = int.MinValue;
			return false;
		}

		private bool GenerateRunRecursive(int runLength)
		{
			int y = this.y;
			if (xStart > 0)
			{
				y += shared.prng.RandomChoice(-1, 1);
			}

			var buffer = shared.GetWritableBuffer(xStart, runLength);
			for (int i = 0; i < runLength; i++)
			{
				int x = xStart + i;
				if (shared.backstop.GetCellForNextLayer(shared.config, x, ref y, shared.LayerId, out var cell))
				{
					buffer[i] = cell;
				}
				else
				{
					return false;
				}
			}

			var next = new LayerGenerator(shared, xStart + runLength, y);
			return next.Execute();
		}
	}
}
