using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

public abstract class StageMutation
{
	public abstract void Apply(IMutableStage stage);

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

	public static StageMutation Combine(IReadOnlyList<StageMutation> mutations, ColumnCleanupMode? columnCleanupMode = null)
	{
		return new CompositeMutation
		{
			Mutations = mutations,
			ColumnCleanupMode = columnCleanupMode,
		};
	}

	sealed class CompositeMutation : StageMutation
	{
		public required IReadOnlyList<StageMutation> Mutations { get; init; }
		public required ColumnCleanupMode? ColumnCleanupMode { get; init; }

		public override void Apply(IMutableStage stage)
		{
			foreach (var m in Mutations)
			{
				m.Apply(stage);
			}

			if (ColumnCleanupMode.HasValue)
			{
				stage.PerformColumnCleanup(ColumnCleanupMode.Value);
			}
		}
	}
}
