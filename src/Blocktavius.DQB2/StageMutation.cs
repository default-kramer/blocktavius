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

	public static StageMutation CreateHills(I2DSampler<int> sampler, ushort block)
	{
		return new Mutations.PutHillMutation()
		{
			Block = block,
			Sampler = sampler,
		};
	}

	public static StageMutation Combine(IReadOnlyList<StageMutation> mutations)
	{
		return new CompositeMutation { Mutations = mutations };
	}

	public static StageMutation Combine(params StageMutation[] mutations)
	{
		return new CompositeMutation { Mutations = mutations };
	}

	sealed class CompositeMutation : StageMutation
	{
		public required IReadOnlyList<StageMutation> Mutations { get; init; }

		internal override void Apply(IMutableStage stage)
		{
			foreach (var m in Mutations)
			{
				m.Apply(stage);
			}
		}
	}
}
