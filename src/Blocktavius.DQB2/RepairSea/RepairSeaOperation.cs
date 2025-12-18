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
		var points = me.Execute3dFloodFill(startingPoints);
		sw2.Stop();

		RepairSeaMutation.DEBUG = $"{sw1.ElapsedMilliseconds} / {sw2.ElapsedMilliseconds} / {points.Count}";
	}

	private HashSet<Point> Execute3dFloodFill(IEnumerable<Point> startingPoints)
	{
		var bounds = this.filterArea.Bounds;
		var visited = new bool[bounds.Size.X, seaLevel + 1, bounds.Size.Z];
		var seaBlocks = new HashSet<Point>();
		var queue = new Queue<Point>();

		foreach (var startPoint in startingPoints)
		{
			// The user-provided FindStartingPoints can yield points for y=0, which we must ignore.
			if (startPoint.Y <= 0 || startPoint.Y > seaLevel) continue;

			var ix = startPoint.xz.X - bounds.start.X;
			var iy = startPoint.Y;
			var iz = startPoint.xz.Z - bounds.start.Z;
			if (visited[ix, iy, iz]) continue;

			visited[ix, iy, iz] = true;
			queue.Enqueue(startPoint);
		}


		while (queue.TryDequeue(out var currentPoint))
		{
			var ix = currentPoint.xz.X - bounds.start.X;
			var iz = currentPoint.xz.Z - bounds.start.Z;

			// We have found a new unvisited point. Scan its vertical run.
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
				if (!policy.CanBePartOfSea(Block.Lookup(columnChunk.GetBlock(new Point(currentPoint.xz, next_y))))) break;
				visited[ix, next_y, iz] = true;
				y_min = next_y;
			}

			// Scan up
			var y_max = currentPoint.Y;
			while (true)
			{
				var next_y = y_max + 1;
				if (next_y > seaLevel) break;
				if (visited[ix, next_y, iz]) break;
				if (!policy.CanBePartOfSea(Block.Lookup(columnChunk.GetBlock(new Point(currentPoint.xz, next_y))))) break;
				visited[ix, next_y, iz] = true;
				y_max = next_y;
			}

			// For the full vertical run, add to sea blocks and check horizontal neighbors
			for (int y = y_min; y <= y_max; y++)
			{
				var runPoint = new Point(currentPoint.xz, y);
				seaBlocks.Add(runPoint);

				// Enqueue horizontal neighbors
				foreach (var neighborXz in runPoint.xz.CardinalNeighbors())
				{
					if (!filterArea.InArea(neighborXz)) continue;

					var neighbor_ix = neighborXz.X - bounds.start.X;
					var neighbor_iz = neighborXz.Z - bounds.start.Z;
					if (visited[neighbor_ix, y, neighbor_iz]) continue;

					if (stage.TryGetChunk(ChunkOffset.FromXZ(neighborXz), out var neighborChunk))
					{
						var neighborPoint = new Point(neighborXz, y);
						var block = Block.Lookup(neighborChunk.GetBlock(neighborPoint));
						if (policy.CanBePartOfSea(block))
						{
							visited[neighbor_ix, y, neighbor_iz] = true;
							queue.Enqueue(neighborPoint);
						}
					}
				}
			}
		}

		// Parallelize the block replacement
		seaBlocks.AsParallel().GroupBy(p => ChunkOffset.FromXZ(p.xz)).ForAll(group =>
		{
			if (stage.TryGetChunk(group.Key, out var chunk))
			{
				foreach (var point in group)
				{
					var block = Block.Lookup(chunk.GetBlock(point));
					if (policy.ShouldOverwriteWhenPartOfSea(Block.Lookup(chunk.GetBlock(point))))
					{
						bool isTopLayer = point.Y == seaLevel;
						if (block.IsProp)
						{
							var amount = isTopLayer ? topLayerAmount : LiquidAmountIndex.Subsurface;
							var newBlock = block.SetLiquid(liquidFamily.LiquidFamilyId, amount);
							chunk.ReplaceProp(point, newBlock);
						}
						else
						{
							ushort simpleBlockId = (point.Y == seaLevel) ? topLayerBlockId : liquidFamily.BlockIdSubsurface;
							chunk.SetBlock(point, simpleBlockId);
						}
					}
				}
			}
		});

		return seaBlocks;
	}

	private void ReplaceBlock(IMutableChunk chunk, Point point, ushort simpleBlockId, LiquidAmountIndex amount)
	{
		var existingBlock = Block.Lookup(chunk.GetBlock(point));
		if (existingBlock.IsProp)
		{
			var newBlock = existingBlock.SetLiquid(liquidFamily.LiquidFamilyId, amount);
			chunk.ReplaceProp(point, newBlock);
		}
		else
		{
			chunk.SetBlock(point, simpleBlockId);
		}
	}

	private static IEnumerable<Point> FindStartingPoints(IStage stage, int seaLevel, IPolicy policy)
	{
		var stageBounds = Rect.Union(stage.ChunksInUse.Select(offset => offset.Bounds));

		foreach (var chunk in stage.IterateChunks())
		{
			foreach (var xz in chunk.Offset.Bounds.Enumerate())
			{
				if (chunk.GetBlock(new Point(xz, 0)).IsEmptyBlock())
				{
					continue;
				}

				//var point = new Point(xz, seaLevel);
				//var block = Block.Lookup(chunk.GetBlock(point));
				//if (!policy.CanBePartOfSea(block))
				{
					//continue;
				}

				bool hasOutOfBoundsNeighbor = false;
				foreach (var neighbor in xz.CardinalNeighbors())
				{
					if (IsOutOfBounds(neighbor, stage, stageBounds))
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
						var block = Block.Lookup(chunk.GetBlock(point));
						if (policy.CanBePartOfSea(block))
						{
							yield return point;
						}
					}
				}
			}
		}
	}

	private static bool IsOutOfBounds(XZ xz, IStage stage, Rect stageBounds)
	{
		// A tile is "out of bounds" if it's outside the Rect containing all chunks,
		// or if there is no chunk there, or if the chunk has no solid ground at Y=0.
		if (!stageBounds.Contains(xz))
		{
			return true;
		}

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


	#region Old Implementation
	private readonly IReadOnlyList<XZ>? edgeLocs;

	private RepairSeaOperation(Params p, IReadOnlyList<XZ> edgeLocs, IArea filterArea)
	{
		// This constructor is only used by the old implementation.
		this.edgeLocs = edgeLocs;

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

	private static IEnumerable<XZ> FindEdgeLocs(IStage stage, LayerZeroArea area)
	{
		var shells = ShellLogic.ComputeShells(area);

		XZ prev = new XZ(int.MaxValue, int.MaxValue); // for simple deduplication
		foreach (var item in shells.SelectMany(s => s.ShellItems))
		{
			var xz = item.XZ.Step(item.InsideDirection);
			if (xz != prev)
			{
				prev = xz;
				yield return xz;
			}
		}
	}

	private void Execute()
	{
		for (int y = 1; y < seaLevel; y++)
		{
			RepairLayer(y, liquidFamily.BlockIdSubsurface, LiquidAmountIndex.Subsurface);
		}
		RepairLayer(seaLevel, topLayerBlockId, topLayerAmount);
	}

	private void RepairLayer(int y, ushort simpleBlockId, LiquidAmountIndex amount)
	{
		var sea = FindSea(y);
		ReplaceBlocks(sea, y, amount, simpleBlockId);
	}

	private IReadOnlySet<XZ> FindSea(int y)
	{
		HashSet<XZ> sea = new();
		Queue<XZ> pending = new(edgeLocs ?? Enumerable.Empty<XZ>());
		while (pending.TryDequeue(out var xz))
		{
			if (stage.TryGetChunk(ChunkOffset.FromXZ(xz), out var chunk))
			{
				var blockId = chunk.GetBlock(new Point(xz, y));
				var block = Block.Lookup(blockId);
				if (policy.CanBePartOfSea(block) && sea.Add(xz))
				{
					foreach (var neighbor in xz.CardinalNeighbors())
					{
						if (!sea.Contains(neighbor) && filterArea.InArea(neighbor))
						{
							pending.Enqueue(neighbor);
						}
					}
				}
			}
		}
		return sea;
	}

	private void ReplaceBlocks(IReadOnlySet<XZ> sea, int y, LiquidAmountIndex amount, ushort simpleBlockId)
	{
		foreach (var xz in sea)
		{
			if (!stage.TryGetChunk(ChunkOffset.FromXZ(xz), out var chunk))
			{
				throw new Exception("Assert fail"); // should never have been added to the set
			}
			var point = new Point(xz, y);
			var existingBlock = Block.Lookup(chunk.GetBlock(point));
			if (policy.ShouldOverwriteWhenPartOfSea(existingBlock))
			{
				if (existingBlock.IsProp)
				{
					var newBlock = existingBlock.SetLiquid(liquidFamily.LiquidFamilyId, amount);
					chunk.ReplaceProp(point, newBlock);
				}
				else
				{
					chunk.SetBlock(point, simpleBlockId);
				}
			}
		}
	}
	#endregion

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
