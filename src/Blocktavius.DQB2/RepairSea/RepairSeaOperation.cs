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
		this.filterArea = filterArea;
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

	private int Execute3dFloodFill(IEnumerable<Point> startingPoints)
	{
		var bounds = this.filterArea.Bounds;
		var visited = new bool[bounds.Size.X, seaLevel + 1, bounds.Size.Z];
		var queue = new Queue<Point>();
		int seaBlockCount = 0;

		foreach (var startPoint in startingPoints)
		{
			if (startPoint.Y <= 0 || startPoint.Y > seaLevel) continue;

			var ix = startPoint.xz.X - bounds.start.X;
			var iy = startPoint.Y;
			var iz = startPoint.xz.Z - bounds.start.Z;
			if (visited[ix, iy, iz]) continue;

			visited[ix, iy, iz] = true;
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
			var y_min = currentPoint.Y;
			while (true)
			{
				var next_y = y_min - 1;
				if (next_y <= 0) break;
				if (visited[ix, next_y, iz]) break;
				if (!policy.CanBePartOfSea(columnChunk.GetBlock(new Point(currentPoint.xz, next_y)))) break;
				visited[ix, next_y, iz] = true;
				seaBlockCount++;
				y_min = next_y;
			}

			// Scan up
			var y_max = currentPoint.Y;
			while (true)
			{
				var next_y = y_max + 1;
				if (next_y > seaLevel) break;
				if (visited[ix, next_y, iz]) break;
				if (!policy.CanBePartOfSea(columnChunk.GetBlock(new Point(currentPoint.xz, next_y)))) break;
				visited[ix, next_y, iz] = true;
				seaBlockCount++;
				y_max = next_y;
			}

			// For the full vertical run, check horizontal neighbors
			for (int y = y_min; y <= y_max; y++)
			{
				var runPoint = new Point(currentPoint.xz, y);

				foreach (var neighborXz in runPoint.xz.CardinalNeighbors())
				{
					if (!filterArea.InArea(neighborXz)) continue;

					var neighbor_ix = neighborXz.X - bounds.start.X;
					var neighbor_iz = neighborXz.Z - bounds.start.Z;
					if (visited[neighbor_ix, y, neighbor_iz]) continue;

					if (stage.TryGetChunk(ChunkOffset.FromXZ(neighborXz), out var neighborChunk))
					{
						var neighborPoint = new Point(neighborXz, y);
						var block = neighborChunk.GetBlock(neighborPoint);
						if (policy.CanBePartOfSea(block))
						{
							visited[neighbor_ix, y, neighbor_iz] = true;
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
						if (visited[ix, y, iz])
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
