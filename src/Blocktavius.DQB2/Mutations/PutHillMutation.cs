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

public sealed class PutInvertedHillMutation : StageMutation
{
	public required I2DSampler<int> Sampler { get; init; }
	public required ushort Block { get; init; }

	/// <summary>
	/// Max value that the <see cref="Sampler"/> will return
	/// </summary>
	public required int MaxElevation { get; init; }

	public required int YFloor { get; init; }

	internal override void Apply(IMutableStage stage)
	{
		int start = YFloor + MaxElevation;

		foreach (var chunk in Enumerate(Sampler.Bounds, stage))
		{
			foreach (var xz in chunk.Offset.Bounds.Intersection(Sampler.Bounds).Enumerate())
			{
				var elevation = Sampler.Sample(xz);
				if (elevation > 0)
				{
					int end = start - elevation;
					for (int y = start; y >= end; y--)
					{
						chunk.SetBlock(new Point(xz, y), Block);
					}
				}
			}
		}
	}
}
