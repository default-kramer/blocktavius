using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

public static class FOO
{
	readonly record struct Sample
	{
		public required ushort DeepBlock { get; init; }
		public required ushort SurfaceBlock { get; init; }
		public required int SurfaceY { get; init; }
	}

	public interface IAnalysis { }

	sealed class Analysis : IAnalysis
	{
		public required IReadOnlyList<(XZ, Sample)> Samples { get; init; }

		public required (Sample sample, double frequency) MostCommonSample { get; init; }
	}

	public static IAnalysis CreateAnalysis(IStage stage)
	{
		List<(XZ, Sample)> samples = new();
		IReadOnlyList<Direction> dirs = [Direction.North, Direction.East, Direction.South, Direction.West];

		foreach (var chunk in stage.IterateChunks())
		{
			foreach (var dir in dirs)
			{
				if (!stage.TryReadChunk(chunk.Offset.GetNeighbor(dir), out _))
				{
					// No neighbor, take a sample.
					// TODO pretty ugly logic to find the starting point here:
					var where = chunk.Offset.NorthwestCorner.Add(15, 15);
					while (chunk.Offset.Bounds.Contains(where))
					{
						where = where.Add(dir.Step);
					}
					where = where.Add(dir.Step.Scale(-1));
					var sample = FindSample(chunk, where, dir.Step.Scale(-1));
					if (sample.HasValue)
					{
						samples.Add(sample.Value);
					}
				}
			}
		}

		var mostCommon = samples.Select(item => item.Item2)
			.GroupBy(x => x)
			.Select(grp =>
			{
				double count = grp.Count();
				return (grp.First(), frequency: count / samples.Count);
			})
			.OrderByDescending(x => x.frequency)
			.ToList();

		return new Analysis
		{
			Samples = samples,
			MostCommonSample = mostCommon.First(),
		};
	}

	private static (XZ, Sample)? FindSample(IChunk chunk, XZ xz, XZ step)
	{
		var bounds = chunk.Offset.Bounds;
		while (bounds.Contains(xz))
		{
			ushort block0 = chunk.GetBlock(new Point(xz, 0));
			switch (block0)
			{
				case DQB2Constants.BlockId.Empty:
					xz = xz.Add(step);
					break;
				case DQB2Constants.BlockId.Bedrock:
					var sample = SampleFromColumn(chunk, xz, startY: 1);
					return sample.HasValue ? (xz, sample.Value) : null;
				default:
					return null;
			}
		}
		return null;
	}

	private static Sample? SampleFromColumn(IChunk chunk, XZ xz, int startY)
	{
		var points = Enumerable.Range(0, DQB2Constants.MaxElevation).Select(y => new Point(xz, y));
		var cells = points.Select(point => (point, block: chunk.GetBlock(point)));

		// The column can be divided into "stacks" from yStart to yEnd such that
		// 1) yEnd > yStart (stack goes upwards and has nonzero length)
		// 2) all blocks in the stack share the same "Is Liquid?" flag
		// Items would complicate this logic, so we will filter them out ahead of time.
		// (A more robust analysis could look at the type of liquid in which the item
		//  is submerged, but this doesn't seem necessary yet.)
		var seaStack = cells
			.Where(x => !IsItem(x.block))
			.SkipWhile(i => i.block != 0 && !IsLiquid(i.block)) // skip seafloor stack
			.TakeWhile(i => IsLiquid(i.block)) // take sea stack
			.ToList();

		if (seaStack.Count == 0)
		{
			return null;
		}

		var deepBlocks = seaStack.SkipLast(1).Select(i => i.block).Distinct().ToList();
		if (deepBlocks.Count != 1)
		{
			return null; // deep block is ambiguous, discard this sample
		}
		var surface = seaStack[^1];
		return new Sample
		{
			DeepBlock = deepBlocks.Single(),
			SurfaceBlock = surface.block,
			SurfaceY = surface.point.Y,
		};
	}

	private static bool IsItem(ushort block) => (block & 0x7FF) >= 1158; // TODO duplicate, grep 1158

	private static bool IsLiquid(ushort block) => block.GetLiquidFamilyIndex() != LiquidFamilyIndex.None;
}

public sealed class LiquidFamily
{
	private LiquidFamily() { }

	public required LiquidFamilyIndex LiquidFamilyId { get; init; }
	public required ushort BlockIdFull { get; init; }
	public required ushort BlockIdSurfaceDeep { get; init; }
	public required ushort BlockIdSurfaceShallow { get; init; }

	public required IReadOnlyList<ushort> SimpleBlockIds { get; init; }

	public static bool TryGet(ushort blockId, out LiquidFamily liquidFamily)
	{
		var fam = blockId.GetLiquidFamilyIndex();
		switch (fam)
		{
			case LiquidFamilyIndex.ClearWater:
				liquidFamily = ClearWater;
				return true;
			case LiquidFamilyIndex.HotWater:
				liquidFamily = HotWater;
				return true;
			case LiquidFamilyIndex.Poison:
				liquidFamily = Poison;
				return true;
			case LiquidFamilyIndex.Lava:
				liquidFamily = Lava;
				return true;
			case LiquidFamilyIndex.BottomlessSwamp:
				liquidFamily = BottomlessSwamp;
				return true;
			case LiquidFamilyIndex.MuddyWater:
				liquidFamily = MuddyWater;
				return true;
			case LiquidFamilyIndex.Seawater:
				liquidFamily = Seawater;
				return true;
			case LiquidFamilyIndex.Plasma:
				liquidFamily = Plasma;
				return true;
			default:
				liquidFamily = null!;
				return false;
		}
	}

	public static readonly LiquidFamily ClearWater = new()
	{
		LiquidFamilyId = LiquidFamilyIndex.ClearWater,
		BlockIdFull = 128,
		BlockIdSurfaceShallow = 343,
		BlockIdSurfaceDeep = 383,
		SimpleBlockIds = [
			120, 128, // Clear-water-full-block
			145, 343, // Clear-water-shallow-block
			121, 122, 123, 142, 143, 144, 383 // Clear-water-surface-block
			],
	};

	public static readonly LiquidFamily HotWater = new()
	{
		LiquidFamilyId = LiquidFamilyIndex.HotWater,
		BlockIdFull = 231,
		BlockIdSurfaceShallow = 344,
		BlockIdSurfaceDeep = 384,
		SimpleBlockIds = [
			230, 231, // Hot-Water-full-block
			223, 344, // Hot-Water-shallow-block
			224, 225, 226, 227, 228, 229, 384 // Hot-Water-surface-block
			],
	};

	public static readonly LiquidFamily Poison = new()
	{
		LiquidFamilyId = LiquidFamilyIndex.Poison,
		BlockIdFull = 190,
		BlockIdSurfaceShallow = 345,
		BlockIdSurfaceDeep = 385,
		SimpleBlockIds = [
			189, 190, // Poison-full-block
			182, 345, // Poison-shallow-block
			183, 184, 185, 186, 187, 188, 385 // Poison-surface-block
			],
	};

	public static readonly LiquidFamily Lava = new()
	{
		LiquidFamilyId = LiquidFamilyIndex.Lava,
		BlockIdFull = 267,
		BlockIdSurfaceShallow = 346,
		BlockIdSurfaceDeep = 386,
		SimpleBlockIds = [
			266, 267, // confirmed 267 is full+stable, and 266 is full+unstable
			259, 346, // Lava-shallow-block
			260, 261, 262, 263, 264, 265, 386 // Lava-surface-block
			],
	};

	public static readonly LiquidFamily BottomlessSwamp = new()
	{
		LiquidFamilyId = LiquidFamilyIndex.BottomlessSwamp,
		BlockIdFull = 199,
		BlockIdSurfaceShallow = 347,
		BlockIdSurfaceDeep = 387,
		SimpleBlockIds = [
			198, 199, // Bottomless-Swamp-full-block
			191, 347, // Bottomless-Swamp-shallow-block
			192, 193, 194, 195, 196, 197, 387 // Bottomless-Swamp-surface-block
			],
	};

	public static readonly LiquidFamily MuddyWater = new()
	{
		LiquidFamilyId = LiquidFamilyIndex.MuddyWater,
		BlockIdFull = 208,
		BlockIdSurfaceShallow = 348,
		BlockIdSurfaceDeep = 388,
		SimpleBlockIds = [
			207, 208, // Muddy-Water-full-block
			200, 348, // Muddy-Water-shallow-block
			201, 202, 203, 204, 205, 206, 388 // Muddy-Water-surface-block
			],
	};

	public static readonly LiquidFamily Seawater = new()
	{
		LiquidFamilyId = LiquidFamilyIndex.Seawater,
		BlockIdFull = 341,
		// fresh IoA/Moonbrooke also use 420 in places, but 349 is most common:
		BlockIdSurfaceShallow = 349,
		BlockIdSurfaceDeep = 389,
		SimpleBlockIds = [
			340, 341, // Sea-water-full-block
			333, 349, 420, // Sea-water-shallow-block
			334, 335, 336, 337, 338, 339, 389], // Sea-water-surface-block
	};

	public static readonly LiquidFamily Plasma = new()
	{
		LiquidFamilyId = LiquidFamilyIndex.Plasma,
		BlockIdFull = 397,
		BlockIdSurfaceShallow = 399,
		BlockIdSurfaceDeep = 400,
		SimpleBlockIds = [
			397, 398, // Plasma-full-block
			390, 399, // Plasma-shallow-block
			391, 392, 393, 394, 395, 396, 400], // Plasma-surface-block
	};
}
