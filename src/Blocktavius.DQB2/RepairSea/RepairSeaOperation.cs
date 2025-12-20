using Blocktavius.Core;
using Blocktavius.DQB2.Mutations;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Blocktavius.DQB2.RepairSea;

sealed class RepairSeaOperation
{
	public sealed class Params
	{
		public required LiquidFamily LiquidFamily { get; init; }
		public required int SeaLevel { get; init; }
		public required LiquidAmountIndex SeaSurfaceType { get; init; }
		public required IPolicy Policy { get; init; }
		public required IMutableStage Stage { get; init; }
		public required ColumnCleanupMode ColumnCleanupMode { get; init; }
	}

	private readonly IArea filterArea; // needed so that we don't put sea beyond the bedrock
	private readonly ushort topLayerBlockId;
	private readonly IMutableStage stage;
	private readonly int seaLevel;
	private readonly LiquidFamily liquidFamily;
	private readonly LiquidAmountIndex topLayerAmount;
	private readonly IPolicy policy;

	private RepairSeaOperation(Params p, IArea filterArea)
	{
		this.filterArea = filterArea;
		this.topLayerBlockId = p.SeaSurfaceType switch
		{
			LiquidAmountIndex.SurfaceLow => p.LiquidFamily.BlockIdSurfaceLow,
			LiquidAmountIndex.SurfaceHigh => p.LiquidFamily.BlockIdSurfaceHigh,
			LiquidAmountIndex.Subsurface => p.LiquidFamily.BlockIdSubsurface,
			_ => throw new ArgumentException($"Unsupported SeaSurfaceType: {p.SeaSurfaceType}"),
		};
		this.stage = p.Stage;
		this.seaLevel = p.SeaLevel;
		this.liquidFamily = p.LiquidFamily;
		this.topLayerAmount = p.SeaSurfaceType;
		this.policy = p.Policy;
	}

	public static void RepairSea(Params p)
	{
		p.Stage.PerformColumnCleanup(p.ColumnCleanupMode);

		var sw1 = System.Diagnostics.Stopwatch.StartNew();
		var startingPoints = FindStartingPoints(p.Stage, p.SeaLevel, p.Policy).ToList();
		sw1.Stop();

		var area = new LayerZeroArea(p.Stage);
		var me = new RepairSeaOperation(p, area);

		var sw2 = System.Diagnostics.Stopwatch.StartNew();
		var seaBlockCount = me.Execute3dFloodFill(startingPoints);
		sw2.Stop();

		RepairSeaMutation.DEBUG = $"{sw1.ElapsedMilliseconds} / {sw2.ElapsedMilliseconds} / {seaBlockCount}";
	}

	/// <summary>
	/// Flood fill that prioritizes vertical columns.
	/// Whenever a new point is added to the sea, we immediately process up and down
	/// its column as far as we can.
	/// </summary>
	/// <remarks>
	/// Performance doesn't matter for most use cases, but can really matter for some.
	/// (A naive implementation was about 8x slower than the current implementation,
	///  which can handle a sea having ~5M blocks in ~1 second.)
	/// </remarks>
	private int Execute3dFloodFill(IEnumerable<Point> startingPoints)
	{
		var bounds = this.filterArea.Bounds;
		// You would think that [x,z,y] would outperform [x,y,z] because we frequently
		// operate on y-adjacent points, but for some reason [x,y,z] performed better
		// in all my tests.
		var isSea = new bool[bounds.Size.X, seaLevel + 1, bounds.Size.Z];
		var queue = new Queue<Point>();
		int seaBlockCount = 0;

		foreach (var startPoint in startingPoints)
		{
			if (startPoint.Y <= 0 || startPoint.Y > seaLevel) continue;

			var ix = startPoint.xz.X - bounds.start.X;
			var iy = startPoint.Y;
			var iz = startPoint.xz.Z - bounds.start.Z;
			if (isSea[ix, iy, iz]) continue;

			isSea[ix, iy, iz] = true;
			seaBlockCount++;
			queue.Enqueue(startPoint);
		}


		while (queue.TryDequeue(out var currentPoint))
		{
			var ix = currentPoint.xz.X - bounds.start.X;
			var iz = currentPoint.xz.Z - bounds.start.Z;

			if (!stage.TryGetChunk(ChunkOffset.FromXZ(currentPoint.xz), out var columnChunk))
			{
				continue;
			}

			// Scan down
			var columnMinY = currentPoint.Y;
			while (true)
			{
				var next_y = columnMinY - 1;
				if (next_y <= 0) break;
				if (isSea[ix, next_y, iz]) break;
				if (!policy.CanBePartOfSea(columnChunk.GetBlock(new Point(currentPoint.xz, next_y)))) break;
				isSea[ix, next_y, iz] = true;
				seaBlockCount++;
				columnMinY = next_y;
			}

			// Scan up
			var columnMaxY = currentPoint.Y;
			while (true)
			{
				var next_y = columnMaxY + 1;
				if (next_y > seaLevel) break;
				if (isSea[ix, next_y, iz]) break;
				if (!policy.CanBePartOfSea(columnChunk.GetBlock(new Point(currentPoint.xz, next_y)))) break;
				isSea[ix, next_y, iz] = true;
				seaBlockCount++;
				columnMaxY = next_y;
			}

			// For the full vertical run, check horizontal neighbors
			for (int y = columnMinY; y <= columnMaxY; y++)
			{
				foreach (var neighborXz in currentPoint.xz.CardinalNeighbors())
				{
					// Given that the profiler reports InArea and TryGetChunk as the two slowest
					// methods in this algorithm, you would *really* think it would be faster to
					// rearrange these loops so that you only check InArea and TryGetChunk once per
					// neighbor XZ, and then you process each Y in the column.
					// But nope, "for Y in column, for neighbor XZ" is faster whether the isSea array
					// has an [x,y,z] or an [x,z,y] layout!
					// I have no idea why this is true...
					if (!filterArea.InArea(neighborXz)) continue;

					var neighbor_ix = neighborXz.X - bounds.start.X;
					var neighbor_iz = neighborXz.Z - bounds.start.Z;
					if (isSea[neighbor_ix, y, neighbor_iz]) continue;

					if (stage.TryGetChunk(ChunkOffset.FromXZ(neighborXz), out var neighborChunk))
					{
						var neighborPoint = new Point(neighborXz, y);
						var block = neighborChunk.GetBlock(neighborPoint);
						if (policy.CanBePartOfSea(block))
						{
							isSea[neighbor_ix, y, neighbor_iz] = true;
							seaBlockCount++;
							queue.Enqueue(neighborPoint);
						}
					}
				}
			}
		}

		// Parallelize the block replacement
		stage.ChunksInUse.AsParallel().ForAll(chunkOffset =>
		{
			if (stage.TryGetChunk(chunkOffset, out var chunk))
			{
				foreach (var xz in chunk.Offset.Bounds.Enumerate())
				{
					var ix = xz.X - bounds.start.X;
					var iz = xz.Z - bounds.start.Z;
					for (int y = 1; y <= seaLevel; y++)
					{
						if (isSea[ix, y, iz])
						{
							var point = new Point(xz, y);
							var block = Block.Lookup(chunk.GetBlock(point));
							if (policy.ShouldOverwriteWhenPartOfSea(block))
							{
								bool isTopLayer = point.Y == seaLevel;
								if (block.IsProp(out var prop))
								{
									var amount = isTopLayer ? topLayerAmount : LiquidAmountIndex.Subsurface;
									var newBlock = prop.SetLiquid(liquidFamily.LiquidFamilyId, amount);
									chunk.ReplaceProp(point, newBlock);
								}
								else
								{
									ushort simpleBlockId = isTopLayer ? topLayerBlockId : liquidFamily.BlockIdSubsurface;
									chunk.SetBlock(point, simpleBlockId);
								}
							}
						}
					}
				}
			}
		});

		return seaBlockCount;
	}

	private static IEnumerable<Point> FindStartingPoints(IStage stage, int seaLevel, IPolicy policy)
	{
		foreach (var chunk in stage.IterateChunks())
		{
			foreach (var xz in chunk.Offset.Bounds.Enumerate())
			{
				if (chunk.GetBlock(new Point(xz, 0)).IsEmptyBlock())
				{
					continue;
				}

				bool hasOutOfBoundsNeighbor = false;
				foreach (var neighbor in xz.CardinalNeighbors())
				{
					if (IsOutOfBounds(neighbor, stage))
					{
						hasOutOfBoundsNeighbor = true;
						break;
					}
				}

				if (hasOutOfBoundsNeighbor)
				{
					for (int y = 0; y <= seaLevel; y++)
					{
						var point = new Point(xz, y);
						var block = chunk.GetBlock(point);
						if (policy.CanBePartOfSea(block))
						{
							yield return point;
						}
					}
				}
			}
		}
	}

	private static bool IsOutOfBounds(XZ xz, IStage stage)
	{
		if (!stage.TryReadChunk(ChunkOffset.FromXZ(xz), out var chunk))
		{
			return true;
		}

		if (chunk.GetBlock(new Point(xz, 0)).IsEmptyBlock())
		{
			return true;
		}

		return false;
	}

	/// <summary>
	/// An area that simply checks whether there is a block at Y=0
	/// </summary>
	sealed class LayerZeroArea : IArea
	{
		private readonly IStage stage;
		public Rect Bounds { get; }

		public LayerZeroArea(IStage stage)
		{
			this.stage = stage;
			this.Bounds = Rect.Union(stage.ChunksInUse.Select(offset => offset.Bounds));
		}

		public bool InArea(XZ xz)
		{
			return stage.TryReadChunk(ChunkOffset.FromXZ(xz), out var chunk)
				&& !chunk.GetBlock(new Point(xz, 0)).IsEmptyBlock();
		}
	}
}
