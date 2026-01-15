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

	internal override void Apply(IMutableStage stage)
	{
		int floorY = OverwriteBedrock ? 0 : 1;

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
				for (int y = column.YStart; y < column.YEnd; y++)
				{
					int adjustedY = y + AdjustY;
					if (adjustedY >= floorY && adjustedY < DQB2Constants.MaxElevation)
					{
						ushort block = column.GetBlock(y);
						if (BlocksToCopy[block])
						{
							chunk.SetBlock(new Point(xz, adjustedY), block);
						}
					}
				}
			}
		}
	}

	private static readonly MaskedBlockLookup<bool> DefaultBlocksToCopy = new();
	static PutSnippetMutation()
	{
		foreach (var key in DefaultBlocksToCopy.Keys)
		{
			DefaultBlocksToCopy[key] = true;
		}
		DefaultBlocksToCopy[0] = false; // exclude vacancy
		DefaultBlocksToCopy[1] = false; // exclude bedrock
	}
}
