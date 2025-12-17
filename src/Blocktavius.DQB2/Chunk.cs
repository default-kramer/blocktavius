using Blocktavius.Core;

namespace Blocktavius.DQB2;

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

	ChunkInternals Internals { get; }
}

/// <summary>
/// See <see cref="ICloneableStage"/>.
/// Mutable chunks are not cloneable for the same reason.
/// </summary>
interface ICloneableChunk : IChunk
{
	IMutableChunk Clone();
}

public interface IMutableChunk : IChunk
{
	void SetBlock(Point point, ushort block);

	void ReplaceProp(Point point, Block prop);

	internal void PerformColumnCleanup(ColumnCleanupMode mode);
}

static class ChunkMath
{
	internal const int maxY = DQB2Constants.MaxElevation;
	internal const int i32 = DQB2Constants.ChunkSizeXZ;

	public const int ShortsPerChunk = maxY * i32 * i32;

	public const int BytesPerChunk = ShortsPerChunk * 2;

	public static int GetUshortIndex(Point point)
	{
		int x = point.xz.X % i32;
		int z = point.xz.Z % i32;
		int y = point.Y % maxY;
		return 0
			+ y * i32 * i32
			+ z * i32
			+ x;
	}

	public static int GetByteIndex(Point point) => GetUshortIndex(point) * 2;

	public static void ValidateLength(IReadOnlyList<byte> blockdata, string argName)
	{
		if (blockdata.Count != BytesPerChunk)
		{
			throw new ArgumentException($"{argName} must have exactly {BytesPerChunk} elements, but got {blockdata}");
		}
	}

	private static readonly byte[] _emptyChunkdata = new byte[BytesPerChunk];
	public static ReadOnlyMemory<byte> EmptyChunkdata => _emptyChunkdata;
}

/// <summary>
/// Contains non-public stuff that would otherwise belong to <see cref="IChunk"/>.
/// </summary>
public abstract class ChunkInternals
{
	internal abstract ValueTask WriteBlockdataAsync(Stream stream);

	internal abstract bool IsEmpty();
}

sealed class ImmutableChunk<TBlockdata> : ChunkInternals, ICloneableChunk where TBlockdata : struct, IBlockdata
{
	private readonly ChunkOffset offset;
	private readonly TBlockdata blockdata;

	public ImmutableChunk(ChunkOffset offset, TBlockdata blockdata)
	{
		this.offset = offset;
		this.blockdata = blockdata;
	}

	public ChunkOffset Offset => offset;

	public ChunkInternals Internals => this;

	public ushort GetBlock(Point point) => blockdata.GetBlock(point);

	public IMutableChunk Clone()
	{
		return MutableChunk<TBlockdata>.Create_CopyOnWrite(offset, blockdata);
	}

	internal override ValueTask WriteBlockdataAsync(Stream stream) => blockdata.WriteAsync(stream);

	internal override bool IsEmpty() => blockdata.IsEmpty();
}

sealed class MutableChunk<TReadBlockdata> : ChunkInternals, IMutableChunk where TReadBlockdata : struct, IBlockdata
{
	private readonly ChunkOffset offset;
	private TReadBlockdata readSource;
	private LittleEndianStuff.ByteArrayBlockdata writeSource;

	private MutableChunk(ChunkOffset offset, TReadBlockdata readSource)
	{
		this.offset = offset;
		this.readSource = readSource;
		this.writeSource = LittleEndianStuff.ByteArrayBlockdata.Nothing;
	}

	private MutableChunk(ChunkOffset offset, TReadBlockdata readSource, LittleEndianStuff.ByteArrayBlockdata writeSource)
	{
		this.offset = offset;
		this.readSource = readSource;
		this.writeSource = writeSource;
	}

	internal static MutableChunk<TReadBlockdata> Create_CopyOnWrite(ChunkOffset offset, TReadBlockdata blockdata)
	{
		return new MutableChunk<TReadBlockdata>(offset, blockdata);
	}

	internal static IMutableChunk CreateFresh(ChunkOffset offset, byte[] bytes)
	{
		var writeSource = new LittleEndianStuff.ByteArrayBlockdata(bytes);
		var readSource = writeSource.HackySelfCast<TReadBlockdata>();
		return new MutableChunk<TReadBlockdata>(offset, readSource, writeSource);
	}

	public ChunkOffset Offset => offset;

	public ChunkInternals Internals => this;

	public ushort GetBlock(Point point) => readSource.GetBlock(point);

	public void SetBlock(Point point, ushort block)
	{
		if (writeSource.IsNothing)
		{
			var prevVal = readSource.GetBlock(point);
			if (block == prevVal)
			{
				return; // no change
			}

			// copy on write:
			var clone = readSource.Clone();
			writeSource = clone;
			readSource = clone.HackySelfCast<TReadBlockdata>();
		}
		writeSource.SetBlock(point, block);
	}

	public void ReplaceProp(Point point, Block prop)
	{
		ushort block = prop.BlockIdComplete;

		if (writeSource.IsNothing)
		{
			var prevVal = readSource.GetBlock(point);
			if (block == prevVal)
			{
				return; // no change
			}

			// copy on write:
			var clone = readSource.Clone();
			writeSource = clone;
			readSource = clone.HackySelfCast<TReadBlockdata>();
		}
		writeSource.ReplaceProp(point, block);
	}

	internal override ValueTask WriteBlockdataAsync(Stream stream) => readSource.WriteAsync(stream);

	internal override bool IsEmpty() => readSource.IsEmpty();

	void IMutableChunk.PerformColumnCleanup(ColumnCleanupMode mode)
	{
		if (writeSource.IsNothing)
		{
			return;
		}
		writeSource.PerformColumnCleanup(mode);
	}
}

/// <summary>
/// A mutable chunk that starts empty.
/// Won't allocate space until needed.
/// </summary>
sealed class MutableEmptyChunk : ChunkInternals, IMutableChunk
{
	private LittleEndianStuff.ByteArrayBlockdata? bytes = null;
	public ChunkOffset Offset { get; }

	public MutableEmptyChunk(ChunkOffset offset)
	{
		this.Offset = offset;
	}

	public ChunkInternals Internals => this;

	public ushort GetBlock(Point point)
	{
		const ushort empty = 0;
		return bytes.HasValue ? bytes.Value.GetBlock(point) : empty;
	}

	public void SetBlock(Point point, ushort block)
	{
		bytes = bytes ?? new LittleEndianStuff.ByteArrayBlockdata(new byte[ChunkMath.BytesPerChunk]);
		bytes.Value.SetBlock(point, block);
	}

	public void ReplaceProp(Point point, Block block)
	{
		bytes = bytes ?? new LittleEndianStuff.ByteArrayBlockdata(new byte[ChunkMath.BytesPerChunk]);
		bytes.Value.ReplaceProp(point, block.BlockIdComplete);
	}

	internal override ValueTask WriteBlockdataAsync(Stream stream)
	{
		if (bytes.HasValue)
		{
			return bytes.Value.WriteAsync(stream);
		}
		else
		{
			return stream.WriteAsync(ChunkMath.EmptyChunkdata);
		}
	}

	internal override bool IsEmpty() => bytes?.IsEmpty() ?? true;

	void IMutableChunk.PerformColumnCleanup(ColumnCleanupMode mode)
	{
		if (bytes.HasValue)
		{
			bytes.Value.PerformColumnCleanup(mode);
		}
	}
}
