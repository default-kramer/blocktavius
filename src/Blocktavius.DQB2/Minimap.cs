using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

/// <remarks>
/// Based on https://github.com/Sapphire645/DQB2MinimapExporter
/// </remarks>
public readonly record struct MinimapTile
{
	public static readonly MinimapTile Empty = new MinimapTile { TileValue = -1 };

	public required int TileValue { get; init; }

	public int TileId => TileValue / 11;
	public int TileType => TileValue % 11;
	public bool IsVisible => (TileValue & 0x8000) != 0;
	public bool IsMountain => (TileValue & 0x4000) != 0;
	public int TileIndex => (TileValue & 0x3FFF) - 1;
}

/// <remarks>
/// Based on https://github.com/Sapphire645/DQB2MinimapExporter
/// </remarks>
public class Minimap
{
	private readonly ReadOnlyMemory<byte> data;

	private Minimap(ReadOnlyMemory<byte> data)
	{
		this.data = data;
	}

	const int MAP_DIMENSION = 256;

	const int INTRO_SIZE = 512;
	const int TILE_DATA_SIZE = 256 * 256 * 2;
	const int OUTRO_SIZE = 4;
	const int ISLAND_DATA_SIZE = INTRO_SIZE + TILE_DATA_SIZE + OUTRO_SIZE;

	public I2DSampler<MinimapTile> ReadMap(int islandId)
	{
		int start = INTRO_SIZE + ISLAND_DATA_SIZE * islandId;
		var slice = data.Slice(start, TILE_DATA_SIZE);
		return new IslandTileSampler(slice);
	}

	public static Minimap FromCmndatFile(FileInfo cmndatFile)
	{
		using var cmndatStream = new FileStream(cmndatFile.FullName, FileMode.Open, FileAccess.Read);
		cmndatStream.Position = 0x2A444; // skip to start of compressed area
		using var zlib = new System.IO.Compression.ZLibStream(cmndatStream, System.IO.Compression.CompressionMode.Decompress);

		using var decompressedStream = new MemoryStream();
		zlib.CopyTo(decompressedStream);
		zlib.Flush();
		decompressedStream.Flush();

		Memory<byte> data;
		if (decompressedStream.TryGetBuffer(out var buffer))
		{
			data = buffer.AsMemory();
		}
		else
		{
			data = new Memory<byte>(decompressedStream.ToArray());
		}

		return new Minimap(data.Slice(2401803)); // skip to start of first island's minimap data
	}

	class IslandTileSampler : I2DSampler<MinimapTile>
	{
		private readonly ReadOnlyMemory<byte> data;

		public IslandTileSampler(ReadOnlyMemory<byte> data)
		{
			this.data = data;
			if (data.Length != TILE_DATA_SIZE)
			{
				throw new ArgumentException($"Wrong size, got {data.Length}, expected {TILE_DATA_SIZE}");
			}
		}

		public Rect Bounds => new Rect(XZ.Zero, new XZ(MAP_DIMENSION, MAP_DIMENSION));

		public MinimapTile Sample(XZ xz)
		{
			if (!Bounds.Contains(xz))
			{
				throw new ArgumentOutOfRangeException(nameof(xz));
			}

			int index = xz.Z * MAP_DIMENSION + xz.X;
			index *= 2;
			byte byte1 = data.Span[index];
			byte byte2 = data.Span[index + 1];
			var tile = new MinimapTile
			{
				TileValue = byte1 | (byte2 << 8)
			};
			return tile.TileIndex < 0 ? MinimapTile.Empty : tile;
		}
	}
}
