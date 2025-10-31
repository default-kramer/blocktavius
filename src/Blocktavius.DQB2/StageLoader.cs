using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

static class StageLoader
{
	public sealed class LoadResult
	{
		public required LittleEndianStuff.ReadonlyBytes OriginalHeader { get; init; }
		public required LittleEndianStuff.ReadonlyBytes OriginalCompressedBody { get; init; }
		public required ChunkGrid<byte[]> ChunkGrid { get; init; }
		internal required IStageSaver Saver { get; init; }
	}

	const int headerLength = 0x110;
	const int chunkGridStart = 0x24C7C1;
	const int chunkGridDimension = 64;
	const int chunkGridLengthUInt16s = chunkGridDimension * chunkGridDimension;
	const int chunkGridLengthBytes = chunkGridLengthUInt16s * 2;

	public static LoadResult LoadStgdat(string stgdatFilePath)
	{
		// These should hold the original STGDAT file contents so that we can
		// make a byte-perfect backup later if we need to.
		(LittleEndianStuff.ReadonlyBytes origHeader, LittleEndianStuff.ReadonlyBytes origCompressedBody) = ReadStgdat(stgdatFilePath);

		// Sapphire: "0x010 in the header has the size of the full STGDAT file"
		// ... But wait, this is the size of the compressed file, right?
		// So we don't really care about it on read, only on write.
		// (It must be, because the uncompressed size didn't change when I was crashing it.)

		var body = new LittleEndianStuff.ReadonlyBytes(origCompressedBody.Decompress());

		// Chunkdata comes last, so this check ensures that everything else will be present
		if (body.Length < GetChunkStartAddress(0))
		{
			throw StgdatTooShort(stgdatFilePath);
		}

		var chunkOffsets = ReadChunkGrid(body.SliceUInt16(chunkGridStart).Slice(0, chunkGridLengthUInt16s));

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
		var chunks = GC.AllocateUninitializedArray<byte[]?>(chunkOffsets.Count);
		for (int i = 0; i < chunkOffsets.Count; i++)
		{
			byte[]? chunk;
			var item = chunkOffsets[i];
			if (item.HasValue)
			{
				int addr = GetChunkStartAddress(item.Value.chunkId);
				chunk = GC.AllocateUninitializedArray<byte>(ChunkMath.BytesPerChunk);
				body.AsSpan.Slice(addr, ChunkMath.BytesPerChunk).CopyTo(chunk);
			}
			else
			{
				chunk = null;
			}
			chunks[i] = chunk;
		}

		var chunkGrid = new ChunkGrid<byte[]>(chunks);

		var saver = new StageSaver
		{
			OriginalFilename = Path.GetFileName(stgdatFilePath),
			OrigHeader = origHeader,
			OrigUncompressedBody = body,
		};

		return new LoadResult()
		{
			OriginalHeader = origHeader,
			OriginalCompressedBody = origCompressedBody,
			ChunkGrid = chunkGrid,
			Saver = saver,
		};
	}

	private static (LittleEndianStuff.ReadonlyBytes header, LittleEndianStuff.ReadonlyBytes compressedBody) ReadStgdat(string stgdatFilePath)
	{
		using var stgdatStream = File.OpenRead(stgdatFilePath);

		byte[] header = new byte[headerLength];
		stgdatStream.ReadExactly(header);
		if (header.Length != headerLength)
		{
			throw StgdatTooShort(stgdatFilePath);
		}

		using var bodyStream = new MemoryStream();
		stgdatStream.CopyTo(bodyStream);
		var compressedBody = bodyStream.ToArray();

		return (new LittleEndianStuff.ReadonlyBytes(header), new LittleEndianStuff.ReadonlyBytes(compressedBody));
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
		const int i64 = chunkGridDimension;
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

	class StageSaver : IStageSaver
	{
		public required string OriginalFilename { get; init; }
		public required LittleEndianStuff.ReadonlyBytes OrigHeader { get; init; }
		public required LittleEndianStuff.ReadonlyBytes OrigUncompressedBody { get; init; }

		public bool CanSave => true;

		public void Save(IWritableSaveSlot slot, IStage stage)
		{
			using var uncompressedBody = new MemoryStream();
			WriteBodyUncompressed(uncompressedBody, stage);
			uncompressedBody.Flush();

			using var compressedBody = new MemoryStream();
			using var zlib = new ZLibStream(compressedBody, CompressionMode.Compress);
			uncompressedBody.Seek(0, SeekOrigin.Begin);
			uncompressedBody.CopyTo(zlib);
			zlib.Flush();
			compressedBody.Flush();

			// Sapphire: STGDAT size: 0x10 - 0x14 (This value is the one the game uses for the malloc of the file. If too small the island will truncate.)
			// Me: Nice, it does match the compressed file size exactly!
			//   example: 39 9d 4a 00 -> 4a9d39 == 4,889,913
			//                and `ls -l` outputs "4889913 Apr 18  2024 STGDAT01.BIN"
			int totalFileSize = Convert.ToInt32(compressedBody.Length) + headerLength;
			byte[] header = OrigHeader.AsSpan.ToArray();
			if (header.Length != headerLength)
			{
				throw new Exception($"Assert fail: wrong header length {header.Length}");
			}

			header[0x10] = (byte)(totalFileSize & 0xFF);
			header[0x11] = (byte)((totalFileSize >> 8) & 0xFF);
			header[0x12] = (byte)((totalFileSize >> 16) & 0xFF);
			header[0x13] = (byte)((totalFileSize >> 24) & 0xFF);

			using var stream = new FileStream(Path.Combine(slot.Directory.FullName, OriginalFilename), FileMode.Create, FileAccess.Write, FileShare.None);
			stream.Write(header);
			compressedBody.Seek(0, SeekOrigin.Begin);
			compressedBody.CopyTo(stream);
			stream.Flush();
			stream.Close();
		}

		private void WriteBodyUncompressed(Stream stream, IStage stage)
		{
			// Sapphire: https://github.com/Sapphire645/DQB2IslandEditor/wiki/Info-on-all-memory-allocations-on-the-STGDATs

			int blockdataStart = GetChunkStartAddress(0);
			var chunkGridData = CreateChunkGrid(stage, out var chunks).AsSpan();
			ushort chunkCount = (ushort)chunks.Count;
			var origBody = OrigUncompressedBody.AsSpan;

			// [[from start to Chunk Count]]
			int position = 0;
			const int chunkCountAddr = 0x1451AF;
			stream.WriteSlice(origBody, ref position, chunkCountAddr);

			// Sapphire: Chunk Count: 0x1451AF - 0x1451B1 (Size: 0x2)
			stream.WriteUInt16(chunkCount);
			position += 2;

			// [[from Chunk Count to Chunk Grid]]
			stream.WriteSlice(origBody, ref position, chunkGridStart);

			// Sapphire: Virtual Grid/ Chunk Grid: 0x24C7C1 - 0x24E7C1 (Size: 64*64 chunks * 2 bytes = 0x2000)
			stream.Write(chunkGridData);
			position += chunkGridData.Length;

			// [[from Chunk Grid to Virtual Chunk Count]]
			const int virtualChunkCountAddr = 0x24E7C5;
			stream.WriteSlice(origBody, ref position, virtualChunkCountAddr);

			// Sapphire: Virtual Chunk Count: 0x24E7C5 - 0x24E7C6 (Size: 0x2)
			stream.WriteUInt16(chunkCount);
			position += 2;

			// [[from Virtual Chunk Count to start of blockdata]]
			stream.WriteSlice(origBody, ref position, blockdataStart);

			// blockdata
			foreach (var chunk in chunks)
			{
				chunk.Internals.WriteBlockdataAsync(stream).AsTask().Wait();
			}
		}

		private static byte[] CreateChunkGrid(IStage stage, out IReadOnlyList<IChunk> chunks)
		{
			const int i64 = chunkGridDimension;
			var bytes = new byte[chunkGridLengthBytes];
			using var stream = new MemoryStream(bytes);
			var chunklist = new List<IChunk>();

			for (int z = 0; z < i64; z++)
			{
				for (int x = 0; x < i64; x++)
				{
					ushort val;

					if (stage.TryReadChunk(new ChunkOffset(x, z), out var chunk))
					{
						int chunkId = chunklist.Count;
						val = (ushort)chunkId;
						chunklist.Add(chunk);
					}
					else
					{
						val = 0xFFFF;
					}

					stream.WriteUInt16(val);
				}
			}

			chunks = chunklist;
			return bytes;
		}
	}

	private static void WriteUInt16(this Stream stream, ushort val)
	{
		var lo = (byte)(val & 0xFF);
		var hi = (byte)((val >> 8) & 0xFF);
		stream.WriteByte(lo);
		stream.WriteByte(hi);
	}

	private static void WriteSlice(this Stream stream, ReadOnlySpan<byte> span, ref int start, int end)
	{
		int length = end - start;
		if (length < 0)
		{
			throw new Exception($"Assert fail, invalid span: {start} to {end}");
		}
		stream.Write(span.Slice(start, length));
		start = end;
	}
}
