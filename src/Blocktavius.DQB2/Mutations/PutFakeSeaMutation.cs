using Blocktavius.Core;

namespace Blocktavius.DQB2.Mutations;

public sealed class PutFakeSeaMutation : StageMutation
{
	public required I2DSampler<bool> Area { get; init; }
	public required int SeaLevel { get; init; }

	internal override void Apply(IMutableStage stage)
	{
		ushort seaBlockId = LiquidFamily.Seawater.BlockIdSurfaceLow;
		const ushort navyBlockId = 103;
		const ushort blueBlockId = 104;

		foreach (var chunk in Enumerate(Area.Bounds, stage))
		{
			bool toggle = (chunk.Offset.OffsetX + chunk.Offset.OffsetZ) % 2 == 0;
			ushort blockId = toggle ? navyBlockId : blueBlockId;

			foreach (var xz in chunk.Offset.Bounds.Intersection(Area.Bounds).Enumerate())
			{
				if (Area.Sample(xz))
				{
					for (int y = 1; y < SeaLevel; y++)
					{
						chunk.SetBlock(new Point(xz, y), blockId);
					}
					chunk.SetBlock(new Point(xz, SeaLevel), seaBlockId);
				}
			}
		}
	}
}
