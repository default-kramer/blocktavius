using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2.RepairSea;

sealed class RepairSeaOperation
{
	public sealed class Params
	{
		public required LiquidFamily LiquidFamily { get; init; }
		public required int SeaLevel { get; init; }
		public required LiquidDepthIndex SeaSurfaceType { get; init; }
		public required IPolicy Policy { get; init; }
		public required IMutableStage Stage { get; init; }
		public required ColumnCleanupMode ColumnCleanupMode { get; init; }
	}

	private readonly IReadOnlyList<XZ> edgeLocs;
	private readonly IArea filterArea; // needed so that we don't put sea beyond the bedrock
	private readonly ushort topLayerBlockId;
	private readonly IMutableStage stage;
	private readonly int seaLevel;
	private readonly LiquidFamily liquidFamily;
	private readonly LiquidDepthIndex topLayerDepth;
	private readonly IPolicy policy;

	private RepairSeaOperation(Params p, IReadOnlyList<XZ> edgeLocs, IArea filterArea)
	{
		this.edgeLocs = edgeLocs;
		this.filterArea = filterArea;
		this.topLayerBlockId = p.SeaSurfaceType switch
		{
			LiquidDepthIndex.SurfaceShallow => p.LiquidFamily.BlockIdSurfaceShallow,
			LiquidDepthIndex.SurfaceDeep => p.LiquidFamily.BlockIdSurfaceDeep,
			LiquidDepthIndex.Full => p.LiquidFamily.BlockIdFull,
			_ => throw new ArgumentException($"Unsupported SeaSurfaceType: {p.SeaSurfaceType}"),
		};
		this.stage = p.Stage;
		this.seaLevel = p.SeaLevel;
		this.liquidFamily = p.LiquidFamily;
		this.topLayerDepth = p.SeaSurfaceType;
		this.policy = p.Policy;
		this.filterArea = filterArea;
	}

	public static void RepairSea(Params p)
	{
		// We perform Column Cleanup so that we can use Layer 0 for reliable edge detection.
		p.Stage.PerformColumnCleanup(p.ColumnCleanupMode);
		var area = new LayerZeroArea(p.Stage);
		var edgeLocs = FindEdgeLocs(p.Stage, area).ToList();
		var me = new RepairSeaOperation(p, edgeLocs, area);
		me.Execute();
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
			RepairLayer(y, liquidFamily.BlockIdFull, LiquidDepthIndex.Full);
		}
		RepairLayer(seaLevel, topLayerBlockId, topLayerDepth);
	}

	private void RepairLayer(int y, ushort simpleBlockId, LiquidDepthIndex depth)
	{
		var sea = FindSea(y);
		ReplaceBlocks(sea, y, depth, simpleBlockId);
	}

	private IReadOnlySet<XZ> FindSea(int y)
	{
		HashSet<XZ> sea = new();
		Queue<XZ> pending = new(edgeLocs);
		while (pending.TryDequeue(out var xz))
		{
			if (stage.TryGetChunk(ChunkOffset.FromXZ(xz), out var chunk))
			{
				var blockId = chunk.GetBlock(new Point(xz, y));
				if (policy.CanBePartOfSea(blockId) && sea.Add(xz))
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

	private void ReplaceBlocks(IReadOnlySet<XZ> sea, int y, LiquidDepthIndex depth, ushort simpleBlockId)
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
					var newBlock = existingBlock.SetLiquid(liquidFamily.LiquidFamilyId, depth);
					chunk.ReplaceProp(point, newBlock);
				}
				else
				{
					chunk.SetBlock(point, simpleBlockId);
				}
			}
		}
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
