using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibDQB.B2;

/// <summary>
/// Provides direct, low-level access to a CMNDAT file.
/// </summary>
public sealed class RawCmndat
{
	const int headerLength = 0x2A444;
	const int decompressedBodyLength = 5627194; // The decompressed body will always have this length

	private readonly Memory<byte> _header;
	private readonly Memory<byte> _body;
	private Span<byte> Header => _header.Span;
	private Span<byte> Body => _body.Span;

	private RawCmndat(Memory<byte> header, Memory<byte> body)
	{
		this._header = header;
		this._body = body;
	}

	/// <summary>
	/// See <see cref="LibDQB.B2.SaveFileKey"/>
	/// </summary>
	public SaveFileKey SaveFileKey
	{
		get => new(BinaryPrimitives.ReadUInt32LittleEndian(Header.Slice(0x80)));
		set => BinaryPrimitives.WriteUInt32LittleEndian(Header.Slice(0x80), value.Value);
	}

	/// <summary>
	/// When sailing, indicates the arrival island.
	/// When not sailing, indicates the island the builder is on.
	/// </summary>
	public IslandId ToIslandId
	{
		get => new IslandId(Header[0xC8]);
		set => Header[0xC8] = value.Value;
	}

	/// <summary>
	/// When sailing, indicates the departure island.
	/// When not sailing, indicates the island the builder is on.
	/// </summary>
	public IslandId FromIslandId
	{
		get => new IslandId(Header[0xC9]);
		set => Header[0xC9] = value.Value;
	}

	/// <summary>
	/// The timestamp shown by the game when you load the file.
	/// </summary>
	/// <remarks>
	/// The save file wants UTC.
	/// The game adjusts to the user's time zone when displaying the value.
	/// </remarks>
	public DateTime LastSaveTime
	{
		// Signed because that's what DateTime likes to use.
		// Unconfirmed if the game cares, but it won't matter for any reasonable value.
		get => DateTime.FromFileTimeUtc(BinaryPrimitives.ReadInt64LittleEndian(Header.Slice(0x2A40D)));
		set => BinaryPrimitives.WriteInt64LittleEndian(Header.Slice(0x2A40D), value.ToFileTimeUtc());
	}

	public static Task<RawCmndat> LoadAsync(FileInfo file) => LoadAsync(file, new CmndatReadOptions());

	public static async Task<RawCmndat> LoadAsync(FileInfo file, CmndatReadOptions options)
	{
		using var readStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, options.FileShare);
		return await LoadAsync(readStream, options);
	}

	public static async Task<RawCmndat> LoadAsync(Stream readStream, CmndatReadOptions options)
	{
		var header = new byte[headerLength];
		await readStream.ReadExactlyAsync(header, 0, headerLength);

		if (!IsHeaderValid(header, 0x61, 0x65, 0x72, 0x43))
		{
			throw new ArgumentException($"Not a valid CMNDAT file (magic number check failed)");
		}

		var body = await DecompressAndValidateLength(readStream);
		return new RawCmndat(header, body);
	}

	private static bool IsHeaderValid(ReadOnlySpan<byte> header, params byte[] check)
	{
		for (int i = 0; i < check.Length; i++)
		{
			if (header[i] != check[i])
			{
				return false;
			}
		}
		return true;
	}

	private static async Task<byte[]> DecompressAndValidateLength(Stream readStream)
	{
		using var zlib = new ZLibStream(readStream, CompressionMode.Decompress, leaveOpen: true);
		var body = new byte[decompressedBodyLength];
		using var bodyStream = new MemoryStream(body);
		await zlib.CopyToAsync(bodyStream);

		if (bodyStream.Position != decompressedBodyLength)
		{
			throw new ArgumentException("Not a valid CMNDAT file (decompressed length check failed)");
		}

		return body;
	}
}
