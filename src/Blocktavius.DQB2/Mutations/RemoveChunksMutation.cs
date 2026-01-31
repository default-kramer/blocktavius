using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2.Mutations;

/// <remarks>
/// Seems solid.
/// If your Builder is standing on a chunk that gets removed, you will start
/// at the dock instead when you load the save.
/// </remarks>
public sealed class RemoveChunksMutation : StageMutation
{
	/// <summary>
	/// Any chunk which contains this block at Y=1 (just above the bedrock) will be eligible for removal.
	/// Eligible chunks must have zero props in order for removal to succeed.
	/// </summary>
	public ushort? FlagBlockId { get; init; }

	internal override void Apply(IMutableStage stage)
	{
		if (!FlagBlockId.HasValue)
		{
			return;
		}
		var flag = Block.MakeCanonical(FlagBlockId.Value);

		List<ChunkOffset> offsetsToRemove = new();
		foreach (var offset in stage.ChunksInUse)
		{
			if (stage.TryReadChunk(offset, out var chunk) && HasFlagBlock(chunk, flag))
			{
				offsetsToRemove.Add(offset);
			}
		}

		stage.RemoveChunksWhenPropless(offsetsToRemove);
	}

	private static bool HasFlagBlock(IChunk chunk, int flagBlock)
	{
		var offset = chunk.Offset;
		foreach (var xz in offset.Bounds.Enumerate())
		{
			var block = chunk.GetBlock(new Point(xz, 1));
			var canonical = Block.MakeCanonical(block);
			if (canonical == flagBlock)
			{
				return true;
			}
		}
		return false;
	}
}
