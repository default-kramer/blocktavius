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
interface IStage
{
	bool TryReadChunk(ChunkOffset offset, out IChunk chunk);

	IReadOnlyList<ChunkOffset> ChunksInUse { get; }
}

interface IMutableStage : IStage
{
	bool TryGetChunk(ChunkOffset offset, out IMutableChunk chunk);
}

/// <summary>
/// This interface is deliberately NOT inherited by <see cref="IMutableStage"/>
/// to make copy-on-write easier. (So we don't have to worry about the original
/// instance getting mutated before the copy-on-write happens.)
/// </summary>
interface ICloneableStage : IStage
{
	IMutableStage Clone();
}

class ImmutableStage : ICloneableStage
{
	private readonly ChunkGrid<ImmutableChunk> chunkGrid;

	private ImmutableStage(ChunkGrid<ImmutableChunk> chunkGrid)
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
		var chunkGrid = result.ChunkGrid.Clone((offset, array) => new ImmutableChunk(offset, array));
		return new ImmutableStage(chunkGrid);
	}
}

class MutableStage : IMutableStage
{
	private readonly ChunkGrid<MutableChunk> chunkGrid;

	private MutableStage(ChunkGrid<MutableChunk> chunkGrid)
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

	public static IMutableStage LoadStgdat(string stgdatFilePath)
	{
		var result = StageLoader.LoadStgdat(stgdatFilePath);
		var chunkGrid = result.ChunkGrid.Clone(MutableChunk.Create);
		return new MutableStage(chunkGrid);
	}

	internal static IMutableStage CopyOnWrite(ChunkGrid<ImmutableChunk> grid)
	{
		var newGrid = grid.Clone((_, chunk) => chunk.Clone_CopyOnWrite());
		return new MutableStage(newGrid);
	}
}
