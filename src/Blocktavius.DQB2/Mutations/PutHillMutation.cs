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
