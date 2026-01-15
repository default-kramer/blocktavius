using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2.Mutations;

public sealed class PutSnippetMutation : StageMutation
{
	public required I2DSampler<IBlockdataColumn> Snippet { get; init; }
	public MaskedBlockLookup<bool> BlocksToCopy { get; init; } = DefaultBlocksToCopy;
	public int AdjustY { get; set; } = 0;
	public bool OverwriteBedrock { get; set; } = false;

	// TODO what to name this? Should probably be an enum... Or maybe a flag in a MaskedBlockLookup
	public bool AgressiveFilldown { get; set; } = false;

	internal override void Apply(IMutableStage stage)
	{
		int floorY = OverwriteBedrock ? 0 : 1;
		bool agressiveFilldown = this.AgressiveFilldown;

		var bounds = Snippet.Bounds;
		foreach (var offset in stage.ChunksInUse)
		{
			var intersection = Snippet.Bounds.Intersection(offset.Bounds);
			if (intersection.IsZero || !stage.TryGetChunk(offset, out var chunk))
			{
				continue;
			}

			foreach (var xz in intersection.Enumerate())
			{
				var column = Snippet.Sample(xz);

				int fillDownStart = -1;
				ushort fillDownBlock = 0;

				for (int y = column.YEnd - 1; y >= column.YStart; y--)
				{
					int adjustedY = y + AdjustY;
					if (adjustedY >= floorY && adjustedY < DQB2Constants.MaxElevation)
					{
						ushort block = column.GetBlock(y);
						if (BlocksToCopy[block])
						{
							chunk.SetBlock(new Point(xz, adjustedY), block);
							fillDownBlock = (ushort)(block & Block.Mask_CanonicalBlockId); // mask off chisel during filldown
							fillDownStart = adjustedY - 1;
						}
						else if (agressiveFilldown && fillDownStart >= 0)
						{
							// The previous block (above) passed the filter, this block did not.
							// This specific pattern is what AgressiveFilldown acts on.
							break;
						}
					}
				}

				for (int y = fillDownStart; y >= 1; y--) // filldown does not touch the Y=0 layer
				{
					chunk.SetBlock(new Point(xz, y), fillDownBlock);
				}
			}
		}
	}

	private static readonly MaskedBlockLookup<bool> DefaultBlocksToCopy = new();
	static PutSnippetMutation()
	{
		foreach (var block in Block.IterateSimpleBlocks())
		{
			DefaultBlocksToCopy[block.BlockIdCanonical] = true;
		}
		DefaultBlocksToCopy[0] = false; // exclude vacancy
		DefaultBlocksToCopy[1] = false; // exclude bedrock
	}
}
