using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

public interface IBlockdataColumn
{
	int YStart { get; }
	int YEnd { get; }

	ushort GetBlock(int y);
}

public sealed class Snippet : I2DSampler<IBlockdataColumn>
{
	private readonly int sizeX;
	private readonly int sizeZ;
	private readonly int sizeY;
	private readonly IReadOnlyList<ushort> blockdata;
	private readonly int sizePerLayer;

	public XZ SizeXZ => new XZ(sizeX, sizeZ);
	public int SizeY => sizeY;
	public Rect Bounds => new Rect(XZ.Zero, SizeXZ);

	private Snippet(IReadOnlyList<ushort> blockdata, XZ sizeXZ, int sizeY)
	{
		this.sizeX = sizeXZ.X;
		this.sizeZ = sizeXZ.Z;
		this.sizeY = sizeY;
		this.blockdata = blockdata;

		if (sizeX < 0 || sizeZ < 0 || sizeY < 0)
		{
			throw new ArgumentException("negative dimensions are not allowed");
		}
		int totalSize = sizeX * sizeZ * sizeY;
		if (blockdata.Count < totalSize)
		{
			throw new ArgumentException($"blockdata too short, needed {totalSize} but got {blockdata.Count}");
		}

		sizePerLayer = sizeX * sizeZ;
	}

	public static Snippet Create(IStage stage, Rect bounds, int floorY = 0)
	{
		if (floorY < 0 || floorY >= DQB2Constants.MaxElevation)
		{
			throw new ArgumentOutOfRangeException($"invalid {nameof(floorY)}: {floorY}");
		}

		int maxSize = bounds.Size.X * bounds.Size.Z * (DQB2Constants.MaxElevation - floorY);
		var blockdata = new ushort[maxSize];
		int numBlocks = 0;

		int y = floorY;
		while (y < DQB2Constants.MaxElevation)
		{
			bool empty = true;
			int numBlocksRewind = numBlocks;

			foreach (var xz in bounds.Enumerate())
			{
				var offset = ChunkOffset.FromXZ(xz);
				ushort block = 0;
				if (stage.TryReadChunk(offset, out var chunk))
				{
					block = chunk.GetBlock(new Point(xz, y));
				}
				blockdata[numBlocks++] = block;
				empty = empty && block == 0;
			}

			if (empty)
			{
				numBlocks = numBlocksRewind;
				break;
			}
			y++;
		}

		int actualSizeY = y - floorY;
		Array.Resize(ref blockdata, numBlocks);
		return new Snippet(blockdata, bounds.Size, actualSizeY);
	}

	IBlockdataColumn I2DSampler<IBlockdataColumn>.Sample(XZ xz)
	{
		if (Bounds.Contains(xz))
		{
			int index = xz.Z * SizeXZ.X + xz.X;
			return new Column(this, index);
		}
		throw new ArgumentOutOfRangeException(nameof(xz));
	}

	sealed class Column : IBlockdataColumn
	{
		private readonly Snippet snippet;
		private readonly int indexStart;

		public Column(Snippet snippet, int indexStart)
		{
			this.snippet = snippet;
			this.indexStart = indexStart;
		}

		public int YStart => 0;
		public int YEnd => snippet.sizeY;

		public ushort GetBlock(int y)
		{
			if (y >= YStart && y < snippet.sizeY)
			{
				int index = indexStart + y * snippet.sizePerLayer;
				return snippet.blockdata[index];
			}
			throw new ArgumentOutOfRangeException(nameof(y));
		}
	}
}
