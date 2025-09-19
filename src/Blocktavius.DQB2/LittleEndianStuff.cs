using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

/// <summary>
/// Dumping ground for code that depends on <see cref="BitConverter.IsLittleEndian"/>.
/// </summary>
static class LittleEndianStuff
{
	static LittleEndianStuff()
	{
		if (!BitConverter.IsLittleEndian)
		{
			throw new Exception("TODO - support Big Endian systems??");
		}
	}

	public readonly struct ReadonlyBytes
	{
		private readonly byte[] bytes;

		public ReadonlyBytes(byte[] bytes)
		{
			this.bytes = bytes;
		}

		public int Length => bytes.Length;

		public byte[] Decompress()
		{
			var stream = new MemoryStream(bytes, writable: false);
			using var zlib = new System.IO.Compression.ZLibStream(stream, System.IO.Compression.CompressionMode.Decompress);
			var decomp = new MemoryStream();
			zlib.CopyTo(decomp);
			return decomp.ToArray();
		}

		public ushort GetUInt16(int addr) => BitConverter.ToUInt16(bytes, addr);

		public uint GetUInt32(int addr) => BitConverter.ToUInt32(bytes, addr);

		/// <summary>
		/// Warning: <paramref name="addr"/> is in bytes (not "count of ushorts to skip")
		/// </summary>
		public ReadOnlySpan<ushort> SliceUInt16(int addr)
		{
			var slice = bytes.AsSpan().Slice(addr);
			return MemoryMarshal.Cast<byte, ushort>(slice);
		}

		public ReadOnlySpan<byte> AsSpan => bytes.AsSpan();
	}

	public readonly struct ByteArrayBlockdata : IMutableBlockdata
	{
		private readonly byte[] array;

		public ByteArrayBlockdata(byte[] array)
		{
			ChunkMath.ValidateLength(array, nameof(array));
			this.array = array;
		}

		public static ByteArrayBlockdata Nothing => default;
		public bool IsNothing => array == null;

		public ushort GetBlock(Point point) => BitConverter.ToUInt16(array, ChunkMath.GetByteIndex(point));

		public void SetBlock(Point point, ushort block)
		{
			var shorts = MemoryMarshal.Cast<byte, ushort>(array);
			shorts[ChunkMath.GetUshortIndex(point)] = block;
		}

		public ByteArrayBlockdata Clone()
		{
			var copy = GC.AllocateUninitializedArray<byte>(ChunkMath.BytesPerChunk);
			this.array.CopyTo(copy.AsMemory());
			return new ByteArrayBlockdata(copy);
		}

		public ValueTask WriteAsync(Stream stream) => stream.WriteAsync(array);

		public TSelf HackySelfCast<TSelf>() where TSelf : struct, IBlockdata
		{
			if (this is TSelf me)
			{
				return me;
			}
			throw new Exception($"Assert fail -- cannot cast {this.GetType().FullName} to {typeof(TSelf).FullName}");
		}
	}
}
