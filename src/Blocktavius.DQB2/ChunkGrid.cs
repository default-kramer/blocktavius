using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

/// <summary>
/// Sapphire:
///    The chunk grid starts at 0x24C7C1.
///    Empty chunks are 0xFFFF.
///    Valid chunks have the value be their index in the block data area.
///    There's space for 0x1000 chunks, or a grid of 64x64 chunks.
/// In Blocktavius, we work with <see cref="ChunkOffset"/>s most of the time,
/// and only deal with the integral Chunk ID/Index when we read or write a STGDAT file.
/// </summary>
class ChunkGrid<TChunk> where TChunk : class
{
	private const int i64 = 64;

	private readonly IReadOnlyList<TChunk?> chunkGrid;

	/// <summary>
	/// Sorted list of offsets which have chunks
	/// </summary>
	public readonly IReadOnlyList<ChunkOffset> chunksInUse;

	public ChunkGrid(IReadOnlyList<TChunk?> chunkGrid)
	{
		if (chunkGrid.Count != i64 * i64)
		{
			throw new ArgumentException($"{nameof(chunkGrid)} has wrong size: {chunkGrid.Count}");
		}
		this.chunkGrid = chunkGrid;

		var inUse = new List<ChunkOffset>();
		foreach (var offset in AllOffsets())
		{
			if (GridLookup(offset, chunkGrid) != null)
			{
				inUse.Add(offset);
			}
		}
		this.chunksInUse = inUse;
	}

	private static IEnumerable<ChunkOffset> AllOffsets()
	{
		for (int z = 0; z < i64; z++)
		{
			for (int x = 0; x < i64; x++)
			{
				yield return new ChunkOffset(x, z);
			}
		}
	}

	public TChunk? GetChunkOrNull(XZ xz) => GridLookup(ChunkOffset.FromXZ(xz), chunkGrid);

	public TChunk? GetChunkOrNull(ChunkOffset offset) => GridLookup(offset, chunkGrid);

	private static TChunk? GridLookup(ChunkOffset offset, IReadOnlyList<TChunk?> chunkGrid)
	{
		int index = offset.OffsetZ * i64 + offset.OffsetX;
		if (index < 0 || index >= chunkGrid.Count)
		{
			return default;
		}
		return chunkGrid[index];
	}

	public ChunkGrid<TNewChunk> Clone<TNewChunk>(Func<ChunkOffset, TChunk, TNewChunk> chunkCloner) where TNewChunk : class
	{
		var newGrid = new List<TNewChunk?>(chunkGrid.Count);
		foreach (var item in chunkGrid.Zip(AllOffsets()))
		{
			if (item.First == null)
			{
				newGrid.Add(null);
			}
			else
			{
				var newChunk = chunkCloner(item.Second, item.First);
				newGrid.Add(newChunk);
			}
		}
		return new ChunkGrid<TNewChunk>(newGrid);
	}

	/// <summary>
	/// Does not remove any chunks.
	/// Resulting grid is the union of the current grid plus the <paramref name="includeChunks"/>,
	/// using <paramref name="chunkCreator"/> wherever the current grid was empty.
	/// </summary>
	public ChunkGrid<TChunk> Expand(IReadOnlySet<ChunkOffset> includeChunks, Func<ChunkOffset, TChunk> chunkCreator)
	{
		var newGrid = new List<TChunk?>(chunkGrid.Count);
		foreach (var item in chunkGrid.Zip(AllOffsets()))
		{
			TChunk? chunk;
			if (item.First != null)
			{
				chunk = item.First; // keep existing
			}
			else if (includeChunks.Contains(item.Second))
			{
				chunk = chunkCreator(item.Second); // create new
			}
			else
			{
				chunk = null;
			}
			newGrid.Add(chunk);
		}
		return new ChunkGrid<TChunk>(newGrid);
	}
}
