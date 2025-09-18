using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

public abstract class StageMutation
{
	internal abstract void Apply(IMutableStage stage);

	protected static IEnumerable<IMutableChunk> Enumerate(Rect bounds, IMutableStage stage)
	{
		foreach (var offset in ChunkOffset.Covering(bounds))
		{
			if (stage.TryGetChunk(offset, out var chunk))
			{
				yield return chunk;
			}
		}
	}

	public static StageMutation CreateHills<T>(I2DSampler<T> sampler, ushort block) where T : IHaveElevation
	{
		return new Mutations.PutHillMutation<T>()
		{
			Block = block,
			Sampler = sampler,
		};
	}
}
