using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

public static class JauntExtractor
{
	public static bool TryExtractJaunt(IStage stage, Point point, CardinalDirection outsideDir, out PositionedJaunt result)
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

		/*
		int rotation = outsideDir switch
		{
			CardinalDirection.South => 0,
			CardinalDirection.East => 90,
			CardinalDirection.North => 180,
			CardinalDirection.West => 270,
			_ => throw new ArgumentException(nameof(outsideDir)),
		};

		if (Jaunt.TryParse(area.Rotate(rotation), out var jaunt))
		{
			result = new ExtractedJaunt
			{
				Area = area,
				Jaunt = jaunt,
				Rotation = (360 - rotation) % 360,
			};
			return true;
		}
		result = null!;
		return false;
		*/
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
		if (!stage.TryGetBlock(point, out var rawBlock) || rawBlock.IsEmptyBlock())
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
			if (stage.TryGetBlock(new Point(xz, y), out var block) && Block.MakeCanonical(block) == canonicalBlock)
			{
				xzs.Add(xz);
				pending.EnqueueAll(xz.CardinalNeighbors());
			}
		}

		return xzs;
	}
}