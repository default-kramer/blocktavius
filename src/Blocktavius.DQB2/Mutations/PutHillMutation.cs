using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2.Mutations;

public sealed class PutHillMutation : StageMutation
{
	public required I2DSampler<int> Sampler { get; init; }
	public required ushort Block { get; init; }
	public int? YFloor { get; init; } = null;

	internal override void Apply(IMutableStage stage)
	{
		int yFloor = this.YFloor ?? 1;

		foreach (var chunk in Enumerate(Sampler.Bounds, stage))
		{
			foreach (var xz in chunk.Offset.Bounds.Intersection(Sampler.Bounds).Enumerate())
			{
				var elevation = Sampler.Sample(xz);
				if (elevation > 0)
				{
					for (int y = yFloor; y <= elevation; y++)
					{
						chunk.SetBlock(new Point(xz, y), Block);
					}
				}
			}
		}
	}
}

public sealed class PutHillMutation2 : StageMutation
{
	public required I2DSampler<(int, ushort)> Sampler { get; init; }
	public int? YFloor { get; init; } = null;

	internal override void Apply(IMutableStage stage)
	{
		int yFloor = this.YFloor ?? 1;

		foreach (var chunk in Enumerate(Sampler.Bounds, stage))
		{
			foreach (var xz in chunk.Offset.Bounds.Intersection(Sampler.Bounds).Enumerate())
			{
				var (elevation, block) = Sampler.Sample(xz);
				if (elevation > 0)
				{
					var unchiseled = (ushort)Block.MakeCanonical(block);
					for (int y = yFloor; y < elevation; y++)
					{
						chunk.SetBlock(new Point(xz, y), unchiseled);
					}
					chunk.SetBlock(new Point(xz, elevation), block);
				}
			}
		}
	}
}

public sealed class ClearEverythingMutation : StageMutation
{
	public int StartY { get; init; } = 1;

	internal override void Apply(IMutableStage stage)
	{
		int startY = this.StartY;

		foreach (var offset in stage.ChunksInUse)
		{
			if (!stage.TryGetChunk(offset, out var chunk))
			{
				continue;
			}

			foreach (var xz in chunk.Offset.Bounds.Enumerate())
			{
				if (chunk.GetBlock(new Point(xz, 0)) == 0) // TODO this is unsound, I think
				{
					continue;
				}
				for (int y = StartY; y < DQB2Constants.MaxElevation; y++)
				{
					chunk.SetBlock(new Point(xz, y), 0);
				}
			}
		}
	}
}

public sealed class PutLakeMutation : StageMutation
{
	public required I2DSampler<int> LakebedElevation { get; init; }
	public required LiquidFamily Liquid { get; init; }
	public required LiquidAmountIndex TopLayerAmount { get; init; }
	public required int TopLayerY { get; init; }

	internal override void Apply(IMutableStage stage)
	{
		ushort subsurfaceId = Liquid.BlockIdSubsurface;
		ushort topLayerId = TopLayerAmount switch
		{
			LiquidAmountIndex.Subsurface => subsurfaceId,
			LiquidAmountIndex.SurfaceHigh => Liquid.BlockIdSurfaceHigh,
			_ => Liquid.BlockIdSurfaceLow,
		};

		// TESTING:
		//subsurfaceId = 0;
		//topLayerId = 0;

		foreach (var offset in stage.ChunksInUse)
		{
			if (!stage.TryGetChunk(offset, out var chunk))
			{
				continue;
			}

			var intersection = LakebedElevation.Bounds.Intersection(offset.Bounds);
			foreach (var xz in intersection.Enumerate())
			{
				int lakebed = LakebedElevation.Sample(xz);
				if (lakebed > 0 && lakebed < TopLayerY)
				{
					chunk.SetBlock(new Point(xz, TopLayerY), topLayerId);
					for (int y = TopLayerY - 1; y > lakebed; y--)
					{
						chunk.SetBlock(new Point(xz, y), subsurfaceId);
					}
				}
			}
		}
	}
}
