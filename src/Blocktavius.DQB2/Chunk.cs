using Blocktavius.Core;
using System.Runtime.InteropServices;

namespace Blocktavius.DQB2;

public record struct ChunkOffset(int OffsetX, int OffsetZ)
{
	public XZ NorthwestCorner => new XZ(OffsetX * ChunkMath.i32, OffsetZ * ChunkMath.i32);

	public Rect Bounds => new Rect(NorthwestCorner, NorthwestCorner.Add(ChunkMath.i32, ChunkMath.i32));

	public static ChunkOffset FromXZ(XZ xz)
	{
		return new ChunkOffset(xz.X / ChunkMath.i32, xz.Z / ChunkMath.i32);
	}

	public static IEnumerable<ChunkOffset> Covering(Rect bounds)
	{
		if (bounds.IsZero)
		{
			yield break;
		}

		var start = FromXZ(bounds.start);
		var end = FromXZ(bounds.end.Add(-1, -1));
		for (int oz = start.OffsetZ; oz <= end.OffsetZ; oz++)
		{
			for (int ox = start.OffsetX; ox <= end.OffsetX; ox++)
			{
				yield return new ChunkOffset(ox, oz);
			}
		}
	}
}

/// <summary>
/// Provide immutable access to a possibly-mutable chunk.
/// </summary>
public interface IChunk
{
	ChunkOffset Offset { get; }

	/// <summary>
	/// Behavior is undefined if <paramref name="point"/> is not actually within this chunk,
	/// but it will probably just do the modulo arithmetic and return that block without complaining.
	/// </summary>
	ushort GetBlock(Point point);

	Task WriteBlockdataAsync(Stream stream);
}

public interface IMutableChunk : IChunk
{
	void SetBlock(Point point, ushort block);
}

static class ChunkMath
{
	internal const int maxY = DQB2Constants.MaxElevation;
	internal const int i32 = DQB2Constants.ChunkSizeXZ;

	public const int ShortsPerChunk = maxY * i32 * i32;

	public const int BytesPerChunk = ShortsPerChunk * 2;

	public static int GetIndex(Point point)
	{
		int x = point.xz.X % i32;
		int z = point.xz.Z % i32;
		int y = point.Y % maxY;
		return 0
			+ y * i32 * i32
			+ z * i32
			+ x;
	}

	public static void ValidateLength(IReadOnlyList<ushort> chunkData, string argName)
	{
		if (chunkData.Count != ShortsPerChunk)
		{
			throw new ArgumentException($"{argName} must have exactly {ShortsPerChunk} elements, but got {chunkData.Count}");
		}
	}
}

abstract class Chunk : IChunk
{
	protected readonly ChunkOffset offset;
	protected abstract IReadOnlyList<ushort> ReadSource { get; }

	public Chunk(ChunkOffset offset)
	{
		this.offset = offset;
	}

	public ChunkOffset Offset => offset;

	public ushort GetBlock(Point point)
	{
		return ReadSource[ChunkMath.GetIndex(point)];
	}

	public virtual void WriteBlockdata(Stream stream)
	{
		var shorts = ReadSource as ushort[] ?? ReadSource.ToArray();
		var bytes = MemoryMarshal.Cast<ushort, byte>(shorts);
		stream.Write(bytes);
	}

	public virtual async Task WriteBlockdataAsync(Stream stream)
	{
		var shorts = ReadSource as ushort[] ?? ReadSource.ToArray();
		var bytes = MemoryMarshal.Cast<ushort, byte>(shorts);
		await stream.WriteAsync(bytes.ToArray()).ConfigureAwait(false);
	}
}

sealed class ImmutableChunk : Chunk
{
	private readonly IReadOnlyList<ushort> chunkData;
	public ImmutableChunk(ChunkOffset offset, IReadOnlyList<ushort> chunkData) : base(offset)
	{
		ChunkMath.ValidateLength(chunkData, nameof(chunkData));
		this.chunkData = chunkData;
	}

	protected override IReadOnlyList<ushort> ReadSource => chunkData;

	public MutableChunk Clone_CopyOnWrite() => MutableChunk.CopyOnWrite(offset, chunkData);
}

sealed class MutableChunk : Chunk, IMutableChunk
{
	// Copy on write. When writeSource is not null, we ensure
	// that object.ReferenceEquals(readSource, writeSource)
	private IReadOnlyList<ushort> readSource;
	private ushort[]? writeSource;
	protected override IReadOnlyList<ushort> ReadSource => readSource;

	private MutableChunk(ChunkOffset offset, IReadOnlyList<ushort> readSource) : base(offset)
	{
		this.readSource = readSource;
		this.writeSource = null;
	}

	private MutableChunk(ChunkOffset offset, ushort[] readWriteSource) : base(offset)
	{
		this.readSource = readWriteSource;
		this.writeSource = readWriteSource;
	}

	public static MutableChunk CopyOnWrite(ChunkOffset offset, IReadOnlyList<ushort> copyOnWriteFrom)
	{
		ChunkMath.ValidateLength(copyOnWriteFrom, nameof(copyOnWriteFrom));
		return new MutableChunk(offset, readSource: copyOnWriteFrom);
	}

	public static MutableChunk Create(ChunkOffset offset, ushort[] chunkData)
	{
		ChunkMath.ValidateLength(chunkData, nameof(chunkData));
		return new MutableChunk(offset, readWriteSource: chunkData);
	}

	public void SetBlock(Point point, ushort block)
	{
		int index = ChunkMath.GetIndex(point);

		if (writeSource == null)
		{
			var prevVal = readSource[index];
			if (block == prevVal)
			{
				return; // no change
			}

			// copy on write:
			writeSource = GC.AllocateUninitializedArray<ushort>(ChunkMath.ShortsPerChunk);
			if (readSource is ushort[] readArray)
			{
				readArray.CopyTo(writeSource, 0);
			}
			else
			{
				for (int i = 0; i < writeSource.Length; i++)
				{
					writeSource[i] = readSource[i];
				}
			}
			readSource = writeSource;
		}

		writeSource[index] = block;
	}
}
