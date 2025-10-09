using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2.Mutations;

class PutHillMutation : StageMutation
{
	public required I2DSampler<int> Sampler { get; init; }
	public required ushort Block { get; init; }

	internal override void Apply(IMutableStage stage)
	{
		foreach (var chunk in Enumerate(Sampler.Bounds, stage))
		{
			foreach (var xz in chunk.Offset.Bounds.Intersection(Sampler.Bounds).Enumerate())
			{
				var elevation = Sampler.Sample(xz);
				if (elevation > 0)
				{
					for (int y = 0; y < elevation; y++)
					{
						chunk.SetBlock(new Point(xz, y), Block);
					}
				}
			}
		}
	}
}
