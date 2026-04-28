using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

public static class JauntParser
{
	/// <summary>
	/// Follows all cardinal neighbors (keeping Y constant) which match the block at the given <paramref name="point"/>.
	/// Returns true if these points define a <see cref="Jaunt"/> having the given <paramref name="outsideDir"/>.
	/// </summary>
	public static bool TryParseJaunt(IStage stage, Point point, CardinalDirection outsideDir, out PositionedJaunt result)
	{
		var xzs = CollectXZs(stage, point);
		var bounds = new Rect.BoundsFinder().IncludeAll(xzs).CurrentBounds();
		if (bounds == null)
		{
			result = null!;
			return false;
		}

		var area = new SetArea { Bounds = bounds, XZs = xzs };
		return Jaunt.TryParse(area, outsideDir, out result);
	}

	sealed class SetArea : I2DSampler<bool>
	{
		public required IReadOnlySet<XZ> XZs { get; init; }
		public required Rect Bounds { get; init; }
		public bool Sample(XZ xz) => XZs.Contains(xz);
	}

	private static HashSet<XZ> CollectXZs(IStage stage, Point point)
	{
		var xzs = new HashSet<XZ>();
		int y = point.Y;

		var offset = ChunkOffset.FromXZ(point.xz);
		if (!stage.TryReadBlock(point, out var rawBlock) || rawBlock.IsEmptyBlock())
		{
			return xzs;
		}
		int canonicalBlock = Block.MakeCanonical(rawBlock);
		Queue<XZ> pending = new();

		xzs.Add(point.xz);
		pending.EnqueueAll(point.xz.CardinalNeighbors());

		while (pending.TryDequeue(out var xz))
		{
			if (xzs.Contains(xz))
			{
				continue;
			}
			if (stage.TryReadBlock(new Point(xz, y), out var block) && Block.MakeCanonical(block) == canonicalBlock)
			{
				xzs.Add(xz);
				pending.EnqueueAll(xz.CardinalNeighbors());
			}
		}

		return xzs;
	}
}
