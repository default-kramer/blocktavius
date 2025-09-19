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
	bool TryReadChunk(ChunkOffset offset, out IChunk chunk);

	IReadOnlyList<ChunkOffset> ChunksInUse { get; }

	IEnumerable<IChunk> IterateChunks() => ChunksInUse
		.Select(offset => TryReadChunk(offset, out var chunk) ? chunk : null)
		.WhereNotNull();
}

public interface IMutableStage : IStage
{
	bool TryGetChunk(ChunkOffset offset, out IMutableChunk chunk);

	void Mutate(StageMutation mutation);
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

	private ImmutableStage(ChunkGrid<ICloneableChunk> chunkGrid)
	{
		this.chunkGrid = chunkGrid;
	}

	public IReadOnlyList<ChunkOffset> ChunksInUse => chunkGrid.chunksInUse;

	public bool TryReadChunk(ChunkOffset offset, out IChunk chunk)
	{
		chunk = chunkGrid.GetChunkOrNull(offset)!;
		return chunk != null;
	}

	public IMutableStage Clone() => MutableStage.CopyOnWrite(this.chunkGrid);

	public static ICloneableStage LoadStgdat(string stgdatFilePath)
	{
		var result = StageLoader.LoadStgdat(stgdatFilePath);
		var chunkGrid = result.ChunkGrid.Clone((offset, array) =>
		{
			var blockdata = new LittleEndianStuff.ByteArrayBlockdata(array);
			ICloneableChunk chunk = new ImmutableChunk<LittleEndianStuff.ByteArrayBlockdata>(offset, blockdata);
			return chunk;
		});
		return new ImmutableStage(chunkGrid);
	}
}

sealed class MutableStage : IMutableStage
{
	private readonly ChunkGrid<IMutableChunk> chunkGrid;

	private MutableStage(ChunkGrid<IMutableChunk> chunkGrid)
	{
		this.chunkGrid = chunkGrid;
	}

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

	internal static IMutableStage CopyOnWrite(ChunkGrid<ICloneableChunk> grid)
	{
		var newGrid = grid.Clone((_, chunk) => chunk.Clone());
		return new MutableStage(newGrid);
	}

	public void Mutate(StageMutation mutation)
	{
		mutation.Apply(this);
	}

	public static IMutableStage LoadStgdat(string stgdatFilePath)
	{
		var result = StageLoader.LoadStgdat(stgdatFilePath);
		var chunkGrid = result.ChunkGrid.Clone(MutableChunk<LittleEndianStuff.ByteArrayBlockdata>.CreateFresh);
		return new MutableStage(chunkGrid);
	}
}
