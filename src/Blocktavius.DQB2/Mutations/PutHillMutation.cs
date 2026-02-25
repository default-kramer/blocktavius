using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2.Mutations;

public sealed class PutHillMutation : StageMutation
{
	public required I2DSampler<int> Sampler { get; init; }
	public required ushort Block { get; init; }
	public int? YFloor { get; init; } = null;
	public bool RespectExistingBedrock { get; init; } = false;

	internal override void Apply(IMutableStage stage)
	{
		int yFloor = this.YFloor ?? 1;
		bool checkBedrock = RespectExistingBedrock;

		foreach (var chunk in Enumerate(Sampler.Bounds, stage))
		{
			foreach (var xz in chunk.Offset.Bounds.Intersection(Sampler.Bounds).Enumerate())
			{
				var elevation = Sampler.Sample(xz);
				if (elevation > 0)
				{
					if (checkBedrock && chunk.GetBlock(new Point(xz, 0)) == 0)
					{
						continue;
					}
					for (int y = yFloor; y <= elevation; y++)
					{
						chunk.SetBlock(new Point(xz, y), Block);
					}
				}
			}
		}
	}
}

public sealed class PutHillMutation2 : StageMutation
{
	public required I2DSampler<(int, ushort)> Sampler { get; init; }
	public int? YFloor { get; init; } = null;

	internal override void Apply(IMutableStage stage)
	{
		int yFloor = this.YFloor ?? 1;

		foreach (var chunk in Enumerate(Sampler.Bounds, stage))
		{
			foreach (var xz in chunk.Offset.Bounds.Intersection(Sampler.Bounds).Enumerate())
			{
				var (elevation, block) = Sampler.Sample(xz);
				if (elevation > 0)
				{
					var unchiseled = (ushort)Block.MakeCanonical(block);
					for (int y = yFloor; y < elevation; y++)
					{
						chunk.SetBlock(new Point(xz, y), unchiseled);
					}
					chunk.SetBlock(new Point(xz, elevation), block);
				}
			}
		}
	}
}

public sealed class ClearEverythingMutation : StageMutation
{
	public int StartY { get; init; } = 1;
	public Rect? Where { get; init; } = null;

	internal override void Apply(IMutableStage stage)
	{
		int startY = this.StartY;

		foreach (var offset in stage.ChunksInUse)
		{
			if (!stage.TryGetChunk(offset, out var chunk))
			{
				continue;
			}

			var where = this.Where ?? offset.Bounds;

			foreach (var xz in chunk.Offset.Bounds.Intersection(where).Enumerate())
			{
				if (chunk.GetBlock(new Point(xz, 0)) == 0) // TODO this is unsound, I think
				{
					continue;
				}
				for (int y = StartY; y < DQB2Constants.MaxElevation; y++)
				{
					chunk.SetBlock(new Point(xz, y), 0);
				}
			}
		}
	}
}

public sealed class PutLakeMutation : StageMutation
{
	public required I2DSampler<int> LakebedElevation { get; init; }
	public required LiquidFamily Liquid { get; init; }
	public required LiquidAmountIndex TopLayerAmount { get; init; }
	public required int TopLayerY { get; init; }

	internal override void Apply(IMutableStage stage)
	{
		ushort subsurfaceId = Liquid.BlockIdSubsurface;
		ushort topLayerId = TopLayerAmount switch
		{
			LiquidAmountIndex.Subsurface => subsurfaceId,
			LiquidAmountIndex.SurfaceHigh => Liquid.BlockIdSurfaceHigh,
			_ => Liquid.BlockIdSurfaceLow,
		};

		// TESTING:
		//subsurfaceId = 0;
		//topLayerId = 0;

		foreach (var offset in stage.ChunksInUse)
		{
			if (!stage.TryGetChunk(offset, out var chunk))
			{
				continue;
			}

			var intersection = LakebedElevation.Bounds.Intersection(offset.Bounds);
			foreach (var xz in intersection.Enumerate())
			{
				int lakebed = LakebedElevation.Sample(xz);
				if (lakebed > 0 && lakebed < TopLayerY)
				{
					chunk.SetBlock(new Point(xz, TopLayerY), topLayerId);
					for (int y = TopLayerY - 1; y > lakebed; y--)
					{
						chunk.SetBlock(new Point(xz, y), subsurfaceId);
					}

					for (int y = lakebed; y > 0; y--)
					{
						chunk.SetBlock(new Point(xz, y), 21);
					}
				}
			}
		}
	}
}

public sealed class PutTreeMutation : StageMutation
{
	public required XZ Center { get; init; }
	public required int Elevation { get; init; }
	public required PRNG Prng { get; init; }

	internal override void Apply(IMutableStage stage)
	{
		const ushort bark = 139; // "Bark"
		const ushort leaves = 492; // "Teal Leaves"

		bool layer3Leaves = Prng.NextBool();
		bool tallCorner = Prng.NextBool();
		bool peak = Prng.NextBool();

		// layer 1
		int y = Elevation;
		PutBlock(stage, Center, y, bark);
		foreach (var dir in Direction.CardinalDirections())
		{
			PutBlock(stage, Center.Step(dir), y, SetChisel(bark, dir));
		}

		// layer 2
		y++;
		PutBlock(stage, Center, y, bark);

		// layer 3
		y++;
		PutBlock(stage, Center, y, bark);
		foreach (var dir in Direction.CardinalDirections())
		{
			PutBlock(stage, Center.Step(dir), y, SetChisel(leaves, Chisel.TopHalf));
			PutBlock(stage, Center.Step(dir, 2), y, leaves);
		}
		if (layer3Leaves)
		{
			foreach (var dir in Direction.OrdinalDirections())
			{
				PutBlock(stage, Center.Step(dir), y, SetChisel(leaves, Chisel.TopHalf));
			}
		}

		// layer 4
		y++;
		PutBlock(stage, Center, y, bark);
		foreach (var dir in Direction.CardinalDirections())
		{
			PutBlock(stage, Center.Step(dir), y, leaves);
		}
		foreach (var dir in Direction.OrdinalDirections())
		{
			ushort block = tallCorner ? leaves : SetChisel(leaves, dir);
			PutBlock(stage, Center.Step(dir), y, block);
		}

		// layer 5
		y++;
		PutBlock(stage, Center, y, bark);
		foreach (var dir in Direction.CardinalDirections())
		{
			PutBlock(stage, Center.Step(dir), y, leaves);
		}
		if (tallCorner)
		{
			foreach (var dir in Direction.OrdinalDirections())
			{
				PutBlock(stage, Center.Step(dir), y, SetChisel(leaves, dir));
			}
		}

		// layer 6
		y++;
		PutBlock(stage, Center, y, leaves);
		foreach (var dir in Direction.CardinalDirections())
		{
			PutBlock(stage, Center.Step(dir), y, SetChisel(leaves, dir));
		}

		// layer 7
		y++;
		if (peak)
		{
			PutBlock(stage, Center, y, SetChisel(leaves, Chisel.BottomHalf));
		}
	}

	private void PutBlock(IMutableStage stage, XZ xz, int y, ushort block)
	{
		if (stage.TryGetChunk(ChunkOffset.FromXZ(xz), out var chunk))
		{
			chunk.SetBlock(new Point(xz, y), block);
		}
	}

	private static ushort SetChisel(ushort block, Direction dir) => SetChisel(block, dir.Turn180.GetDiagonalChisel());

	private static ushort SetChisel(ushort block, Chisel chisel)
	{
		return Block.Lookup(block).SetChisel(chisel).BlockIdComplete;
	}
}
