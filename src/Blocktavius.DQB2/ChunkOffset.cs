using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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