using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

readonly struct ReadonlyBytes
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

	// WARNING - The following probably won't work correctly if BitConvert.IsLittleEndian==false
	// ... but for now they are convenient
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
}