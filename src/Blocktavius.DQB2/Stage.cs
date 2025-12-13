using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

/// <summary>
/// Provides immutable access to a possibly-mutable stage.
/// </summary>
public interface IStage
{
	/// <summary>
	/// Returns a sampler with max possible bounds extending from (0,0) to (64,64).
	/// The sampler's actual bounds will be cropped to the smallest possible rect.
	/// Null indicates the chunk is not in use.
	/// CAUTION! Notice how the XZ you pass into this sampler does not mean
	/// what XZ usually means. Possibly confusing, but I don't think it's enough
	/// to justify creating an I2DSampler{TIndexer, TResult} yet...
	/// </summary>
	I2DSampler<ChunkOffset?> ChunkGridCropped { get; }

	bool TryReadChunk(ChunkOffset offset, out IChunk chunk);

	/// <summary>
	/// Here "original" approximately means "existed when the STGDAT file was loaded".
	/// Differs from <see cref="ChunksInUse"/> only when the chunk grid is modified.
	/// </summary>
	IReadOnlyList<ChunkOffset> OriginalChunksInUse { get; }

	IReadOnlyList<ChunkOffset> ChunksInUse { get; }

	IEnumerable<IChunk> IterateChunks() => ChunksInUse
		.Select(offset => TryReadChunk(offset, out var chunk) ? chunk : null)
		.WhereNotNull();

	IStageSaver Saver { get; }
}

public interface IMutableStage : IStage
{
	bool TryGetChunk(ChunkOffset offset, out IMutableChunk chunk);

	void Mutate(StageMutation mutation);

	void ExpandChunks(IReadOnlySet<ChunkOffset> includeChunks);

	void PerformColumnCleanup(ColumnCleanupMode mode);
}

/// <summary>
/// Blocktavius denies "Mutations" (eg "put hill") from writing to the Y=0 layer.
/// This means that Mutations can unconditionally write outside the bounds
/// of the existing bedrock, and we can decide later whether to
/// * use <see cref="ExpandBedrock"/> to accept the out-of-bounds blocks by placing
///   new bedrock below them, or to
/// * use <see cref="ConstrainToBedrock"/> to undo the out-of-bounds placements.
///
/// For performance reasons, Mutations might want to attempt avoiding to write columns
/// that will be cleared later, but this column cleanup mechanism is a good way to allow the user
/// to defer the decision and to guarantee predictable bedrock behavior no matter what the Mutation does.
/// </summary>
public enum ColumnCleanupMode
{
	Unset,

	/// <summary>
	/// Puts bedrock at Y=0 into any column that is not empty.
	/// </summary>
	ExpandBedrock,

	/// <summary>
	/// Clears out any column which doesn't have bedrock at Y=0.
	/// </summary>
	ConstrainToBedrock,
}

/// <summary>
/// This interface is deliberately NOT inherited by <see cref="IMutableStage"/>
/// to make copy-on-write easier. (So we don't have to worry about the original
/// instance getting mutated before the copy-on-write happens.)
/// </summary>
public interface ICloneableStage : IStage
{
	IMutableStage Clone();
}

public sealed class ImmutableStage : ICloneableStage
{
	private readonly ChunkGrid<ICloneableChunk> chunkGrid;
	public required IStageSaver Saver { get; init; }

	private ImmutableStage(ChunkGrid<ICloneableChunk> chunkGrid)
	{
		this.chunkGrid = chunkGrid;
	}

	public I2DSampler<ChunkOffset?> ChunkGridCropped => chunkGrid.croppedOffsetSampler;
	public IReadOnlyList<ChunkOffset> OriginalChunksInUse => ChunksInUse;
	public IReadOnlyList<ChunkOffset> ChunksInUse => chunkGrid.chunksInUse;

	public bool TryReadChunk(ChunkOffset offset, out IChunk chunk)
	{
		chunk = chunkGrid.GetChunkOrNull(offset)!;
		return chunk != null;
	}

	public IMutableStage Clone() => MutableStage.CopyOnWrite(this.chunkGrid, this);

	public static ICloneableStage LoadStgdat(string stgdatFilePath)
	{
		var result = StageLoader.LoadStgdat(stgdatFilePath);
		var chunkGrid = result.ChunkGrid.Clone((offset, array) =>
		{
			var blockdata = new LittleEndianStuff.ByteArrayBlockdata(array);
			ICloneableChunk chunk = new ImmutableChunk<LittleEndianStuff.ByteArrayBlockdata>(offset, blockdata);
			return chunk;
		});

		return new ImmutableStage(chunkGrid) { Saver = result.Saver };
	}
}

sealed class MutableStage : IMutableStage
{
	private ChunkGrid<IMutableChunk> chunkGrid;
	public IReadOnlyList<ChunkOffset> OriginalChunksInUse { get; }
	public required IStageSaver Saver { get; init; }

	private MutableStage(ChunkGrid<IMutableChunk> chunkGrid, IReadOnlyList<ChunkOffset> originalChunksInUse)
	{
		this.chunkGrid = chunkGrid;
		this.OriginalChunksInUse = originalChunksInUse;
	}

	public I2DSampler<ChunkOffset?> ChunkGridCropped => chunkGrid.croppedOffsetSampler;
	public IReadOnlyList<ChunkOffset> ChunksInUse => chunkGrid.chunksInUse;

	public bool TryGetChunk(ChunkOffset offset, out IMutableChunk chunk)
	{
		chunk = chunkGrid.GetChunkOrNull(offset)!;
		return chunk != null;
	}

	public bool TryReadChunk(ChunkOffset offset, out IChunk chunk)
	{
		chunk = chunkGrid.GetChunkOrNull(offset)!;
		return chunk != null;
	}

	internal static IMutableStage CopyOnWrite(ChunkGrid<ICloneableChunk> grid, IStage stage)
	{
		var newGrid = grid.Clone((_, chunk) => chunk.Clone());
		return new MutableStage(newGrid, stage.OriginalChunksInUse) { Saver = stage.Saver };
	}

	public void Mutate(StageMutation mutation)
	{
		mutation.Apply(this);
	}

	public static IMutableStage LoadStgdat(string stgdatFilePath)
	{
		var result = StageLoader.LoadStgdat(stgdatFilePath);
		var chunkGrid = result.ChunkGrid.Clone(MutableChunk<LittleEndianStuff.ByteArrayBlockdata>.CreateFresh);
		return new MutableStage(chunkGrid, chunkGrid.chunksInUse) { Saver = result.Saver };
	}

	public void ExpandChunks(IReadOnlySet<ChunkOffset> includeChunks)
	{
		this.chunkGrid = chunkGrid.Expand(includeChunks, offset => new MutableEmptyChunk(offset));
	}

	public void PerformColumnCleanup(ColumnCleanupMode mode)
	{
		foreach (var chunk in chunkGrid.IterateChunks())
		{
			chunk.PerformColumnCleanup(mode);
		}
	}
}
