using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2.Mutations;

public sealed class DangerouslyClearBlocksMutation : StageMutation
{
	public required IReadOnlyList<Point> Points { get; init; }
	public ushort BlockValue { get; init; } = 0;

	internal override void Apply(IMutableStage stage)
	{
		ushort block = this.BlockValue;

		foreach (var point in Points)
		{
			if (stage.TryGetChunk(ChunkOffset.FromXZ(point.xz), out var chunk))
			{
				chunk.DangerouslySetBlock(point, block);
			}
		}
	}
}
