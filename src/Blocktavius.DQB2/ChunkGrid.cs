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

	// Keep our internal usage of ChunkOffset.RawUnscaledOffset as private as possible.
	private readonly I2DSampler<TChunk?> chunkSampler;

	// Too bad, we can't keep this one private:
	public readonly I2DSampler<ChunkOffset?> croppedOffsetSampler;

	/// <summary>
	/// Sorted list of offsets which have chunks
	/// </summary>
	public readonly IReadOnlyList<ChunkOffset> chunksInUse;

	internal ChunkGrid(MutableChunkGrid<TChunk> grid)
	{
		(this.chunkSampler, this.croppedOffsetSampler, this.chunksInUse) = grid.Finish();
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

	public IEnumerable<TChunk> IterateChunks()
	{
		return chunksInUse.Select(offset => GetChunkOrNull(offset) ?? throw new Exception("Assert fail"));
	}

	public TChunk? GetChunkOrNull(XZ xz) => chunkSampler.Sample(ChunkOffset.FromXZ(xz).RawUnscaledOffset);

	public TChunk? GetChunkOrNull(ChunkOffset offset) => chunkSampler.Sample(offset.RawUnscaledOffset);

	public ChunkGrid<TNewChunk> Clone<TNewChunk>(Func<ChunkOffset, TChunk, TNewChunk> chunkCloner) where TNewChunk : class
	{
		var newGrid = new MutableChunkGrid<TNewChunk>();
		foreach (var offset in AllOffsets())
		{
			var existing = GetChunkOrNull(offset);
			if (existing != null)
			{
				var replacement = chunkCloner(offset, existing);
				newGrid.SetUsed(offset, replacement);
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
		var newGrid = new MutableChunkGrid<TChunk>();
		foreach (var offset in AllOffsets())
		{
			var existingChunk = GetChunkOrNull(offset);
			if (existingChunk != null)
			{
				newGrid.SetUsed(offset, existingChunk);
			}
			else if (includeChunks.Contains(offset))
			{
				newGrid.SetUsed(offset, chunkCreator(offset));
			}
		}
		return new ChunkGrid<TChunk>(newGrid);
	}

	/// <summary>
	/// CALLER MUST VERIFY that this won't delete any props or anything else that might be bad.
	/// </summary>
	internal ChunkGrid<TChunk> RemoveChunks(IReadOnlySet<ChunkOffset> offsetsToRemove)
	{
		var newGrid = new MutableChunkGrid<TChunk>();
		foreach (var offset in this.chunksInUse)
		{
			if (!offsetsToRemove.Contains(offset))
			{
				var chunk = GetChunkOrNull(offset)
					?? throw new Exception("Assert fail - chunksInUse contained a null chunk");
				newGrid.SetUsed(offset, chunk);
			}
		}
		return new ChunkGrid<TChunk>(newGrid);
	}
}

class MutableChunkGrid<TChunk> where TChunk : class
{
	private const int i64 = 64;

	private readonly MutableArray2D<TChunk?> array;
	private readonly HashSet<ChunkOffset> inUse = new();
	private readonly Rect.BoundsFinder croppedBoundsFinder = new();
	private bool finished = false;

	public MutableChunkGrid()
	{
		array = new MutableArray2D<TChunk?>(new Rect(XZ.Zero, new XZ(i64, i64)), null);
	}

	public void SetUsed(ChunkOffset offset, TChunk chunk)
	{
		if (finished)
		{
			throw new InvalidOperationException("Cannot mutate after Finish()");
		}

		var xz = offset.RawUnscaledOffset;
		array.Put(xz, chunk);
		inUse.Add(offset);
		croppedBoundsFinder.Include(xz);
	}

	public (I2DSampler<TChunk?> tSampler, I2DSampler<ChunkOffset?> croppedSampler, IReadOnlyList<ChunkOffset> inUse) Finish()
	{
		finished = true;

		var cropped = array.Crop(croppedBoundsFinder.CurrentBounds() ?? Rect.Zero)
			.Project((chunk, xz) =>
			{
				ChunkOffset? offset = (chunk == null) ? null : new ChunkOffset(xz.X, xz.Z);
				return offset;
			});

		var sortedOffsets = inUse.OrderBy(o => o.OffsetZ).ThenBy(o => o.OffsetX).ToList();

		return (array, cropped, sortedOffsets);
	}
}
