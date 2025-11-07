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

	public int TileId => TileIndex / 11;
	public int TileType => TileIndex % 11;
	public bool IsVisible => (TileValue & 0x8000) != 0;
	public bool IsMountain => (TileValue & 0x4000) != 0;
	public int TileIndex => (TileValue & 0x3FFF) - 1;
}

/// <remarks>
/// Based on https://github.com/Sapphire645/DQB2MinimapExporter
/// </remarks>
public class Minimap
{
	/// <summary>
	/// Should be sliced to start at the first island (including its <see cref="INTRO_SIZE"/>).
	/// </summary>
	private readonly ReadOnlyMemory<byte> data;

	private Minimap(ReadOnlyMemory<byte> data)
	{
		this.data = data;
	}

	const int MAP_DIMENSION = 256;

	// Each island has an "intro", tile data, and "outro".
	// I'm not sure if there's anything useful in the intro or the outro.
	const int INTRO_SIZE = 512;
	const int TILE_DATA_SIZE = 256 * 256 * 2;
	const int OUTRO_SIZE = 4;
	const int ISLAND_DATA_SIZE = INTRO_SIZE + TILE_DATA_SIZE + OUTRO_SIZE;

	/* Sapphire's Python script has this for island IDs:
Island_names = ((0,"Isle of\nAwakening\n\nからっぽ島","aqua"),(1,"Furrowfield\n\nモンゾーラ島","medium sea green"),
                (2,"Khrumbul-Dun\nオッカムル島","orange"),(20,"Upper level","#C47400"),(21,"Lower level","#944D00"),
                (3,"Moonbrooke\n\nムーンブルク島","#00FFC0"),(4,"Malhalla\n\n破壊天体シドー","#BBBBFF"),
                (8,"Skelkatraz\n\n監獄島","grey"),(10,"Buildertopia 1\n\nかいたく島1","#FFDDDD"),(11,"Buildertopia 2\n\nかいたく島2","#FFE6DD"),
                (13,"Buildertopia 3\n\nかいたく島3","#FFEEDD"),(7,"Angler's Isle\n\nツリル島","#DDF0FF"),(12,"Battle Atoll   バトル島","#FF9999"))
	*/

	public I2DSampler<MinimapTile> ReadMap(int islandId)
	{
		int start = INTRO_SIZE + ISLAND_DATA_SIZE * islandId;
		var slice = data.Slice(start, TILE_DATA_SIZE);
		return new IslandTileSampler(slice);
	}

	public I2DSampler<MinimapTile> ReadMapCropped(int islandId, IStage cropper)
	{
		var sampler = ReadMap(islandId);

		// Minimap tile grid is 256x256; chunk grid is 64x64.
		// This means there are 4x4 map tiles in each chunk.
		const int scale = 4;
		var gridBounds = cropper.ChunkGridCropped.Bounds;
		var scaledBounds = new Rect(gridBounds.start.Scale(scale), gridBounds.end.Scale(scale));
		return sampler.Crop(scaledBounds);
	}

	public static Minimap FromCmndatFile(FileInfo cmndatFile)
	{
		using var cmndatStream = new FileStream(cmndatFile.FullName, FileMode.Open, FileAccess.Read);
		cmndatStream.Position = 0x2A444; // skip to start of compressed area
		using var zlib = new System.IO.Compression.ZLibStream(cmndatStream, System.IO.Compression.CompressionMode.Decompress);

		const int decompressedLength = 5627194; // hypothesis: The decompressed buffer will always have this length
		using var decompressedStream = new MemoryStream(decompressedLength);
		zlib.CopyTo(decompressedStream);
		zlib.Flush();
		decompressedStream.Flush();

		if (decompressedStream.Length != decompressedLength && System.Diagnostics.Debugger.IsAttached)
		{
			System.Diagnostics.Debugger.Break(); // decompressedLength hypothesis invalidated?
		}

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
		/// <summary>
		/// Should be sliced to contain only the <see cref="TILE_DATA_SIZE"/> range of bytes
		/// for the desired map.
		/// </summary>
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
