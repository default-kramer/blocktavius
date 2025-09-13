using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

static class StageLoader
{
	public sealed class LoadResult
	{
		public required ReadonlyBytes OriginalHeader { get; init; }
		public required ReadonlyBytes OriginalCompressedBody { get; init; }
		public required ChunkGrid<ushort[]> ChunkGrid { get; init; }
	}

	public static LoadResult LoadStgdat(string stgdatFilePath)
	{
		// These should hold the original STGDAT file contents so that we can
		// make a byte-perfect backup later if we need to.
		(ReadonlyBytes origHeader, ReadonlyBytes origCompressedBody) = ReadStgdat(stgdatFilePath);

		// Sapphire: "0x010 in the header has the size of the full STGDAT file"
		// ... But wait, this is the size of the compressed file, right?
		// So we don't really care about it on read, only on write.
		// (It must be, because the uncompressed size didn't change when I was crashing it.)

		// We don't really need ReadonlyBytes here, but it has some convenient methods on it:
		var body = new ReadonlyBytes(origCompressedBody.Decompress());

		// Chunkdata comes last, so this check ensures that everything else will be present
		if (body.Length < GetChunkStartAddress(0))
		{
			throw StgdatTooShort(stgdatFilePath);
		}

		var chunkOffsets = ReadChunkGrid(body.SliceUInt16(0x24C7C1).Slice(0, 64 * 64));

		// Our code assumes that chunk IDs always go from 0..N-1
		// so validate that this is true, otherwise things could go really haywire.
		var chunkIds = chunkOffsets.Where(x => x.HasValue).Select(x => x!.Value.chunkId).ToList();
		if (!chunkIds.SequenceEqual(Enumerable.Range(0, chunkIds.Count)))
		{
			throw new ArgumentException($"Chunk IDs are not sorted! {stgdatFilePath}");
		}
		if (chunkIds.Count == 0)
		{
			throw new ArgumentException($"Stage has no chunks! {stgdatFilePath}");
		}

		// Ensure the file is big enough for all chunks declared by the grid
		int lastChunkEnd = GetChunkStartAddress(chunkIds.Max() + 1);
		if (body.Length < lastChunkEnd)
		{
			throw StgdatTooShort(stgdatFilePath);
		}

		// Now load the chunks
		var chunks = GC.AllocateUninitializedArray<ushort[]?>(chunkOffsets.Count);
		for (int i = 0; i < chunkOffsets.Count; i++)
		{
			ushort[]? chunk;
			var item = chunkOffsets[i];
			if (item.HasValue)
			{
				int addr = GetChunkStartAddress(item.Value.chunkId);
				chunk = GC.AllocateUninitializedArray<ushort>(ChunkMath.ShortsPerChunk);
				body.SliceUInt16(addr).Slice(0, ChunkMath.ShortsPerChunk).CopyTo(chunk);
			}
			else
			{
				chunk = null;
			}
			chunks[i] = chunk;
		}

		var chunkGrid = new ChunkGrid<ushort[]>(chunks);
		return new LoadResult()
		{
			OriginalHeader = origHeader,
			OriginalCompressedBody = origCompressedBody,
			ChunkGrid = chunkGrid,
		};
	}

	private static (ReadonlyBytes header, ReadonlyBytes compressedBody) ReadStgdat(string stgdatFilePath)
	{
		using var stgdatStream = File.OpenRead(stgdatFilePath);

		const int headerLength = 0x110;

		byte[] header = new byte[headerLength];
		stgdatStream.ReadExactly(header);
		if (header.Length != headerLength)
		{
			throw StgdatTooShort(stgdatFilePath);
		}

		using var bodyStream = new MemoryStream();
		stgdatStream.CopyTo(bodyStream);
		var compressedBody = bodyStream.ToArray();

		return (new ReadonlyBytes(header), new ReadonlyBytes(compressedBody));
	}

	private static ArgumentException StgdatTooShort(string stgdatFilePath)
	{
		return new ArgumentException($"STGDAT file is too short: {stgdatFilePath}");
	}

	/// <summary>
	/// Returns a list containing 64*64 items.
	/// Null indicates the chunk is not in use.
	/// </summary>
	private static List<(ChunkOffset offset, int chunkId)?> ReadChunkGrid(ReadOnlySpan<ushort> stgdatSlice)
	{
		const int i64 = 64;
		const ushort notUsed = 0xFFFF;

		var list = new List<(ChunkOffset offset, int chunkId)?>(i64 * i64);

		int index = 0;
		for (int zOffset = 0; zOffset < i64; zOffset++)
		{
			for (int xOffset = 0; xOffset < i64; xOffset++)
			{
				ushort chunkId = stgdatSlice[index];
				index++;
				if (chunkId == notUsed)
				{
					list.Add(null);
				}
				else
				{
					list.Add((new ChunkOffset(xOffset, zOffset), chunkId));
				}
			}
		}

		return list;
	}

	private static int GetChunkStartAddress(int chunkId)
	{
		const int chunkDataOffset = 0x183FEF0;
		return chunkDataOffset + chunkId * ChunkMath.BytesPerChunk;
	}
}
