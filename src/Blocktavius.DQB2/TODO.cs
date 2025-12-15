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

	private static bool IsLiquid(ushort block) => LiquidFamily.GetFamilyId(block) != LiquidFamilyId.None;
}

public enum LiquidFamilyId
{
	None,
	BottomlessSwamp,
	ClearWater,
	HotWater,
	Lava,
	MuddyWater,
	Plasma,
	Poison,
	Seawater,
}

public sealed class LiquidFamily
{
	private LiquidFamily() { }

	public required LiquidFamilyId LiquidFamilyId { get; init; }
	public required ushort BlockIdFull { get; init; }
	public required ushort BlockIdSurfaceDeep { get; init; }
	public required ushort BlockIdSurfaceShallow { get; init; }

	public required IReadOnlyList<ushort> SimpleBlockIds { get; init; }

	public static bool TryGet(ushort blockId, out LiquidFamily liquidFamily)
	{
		var fam = GetFamilyId(blockId);
		switch (fam)
		{
			case LiquidFamilyId.Seawater:
				liquidFamily = Seawater;
				return true;
			case LiquidFamilyId.Plasma:
				liquidFamily = Plasma;
				return true;
			// TODO NOMERGE need to define the other families here!
			default:
				liquidFamily = null!;
				return false;
		}
	}

	public static readonly LiquidFamily Seawater = new()
	{
		LiquidFamilyId = LiquidFamilyId.Seawater,
		BlockIdFull = 341,
		// fresh IoA/Moonbrooke also use 420 in places, but 349 is most common:
		BlockIdSurfaceShallow = 349,
		BlockIdSurfaceDeep = 389, // TODO unconfirmed...
		SimpleBlockIds = [
			340, 341, // Sea-water-full-block
			333, 349, 420, // Sea-water-shallow-block
			334, 335, 336, 337, 338, 339, 389], // Sea-water-surface-block
	};

	public static readonly LiquidFamily Plasma = new()
	{
		LiquidFamilyId = LiquidFamilyId.Plasma,
		BlockIdFull = 397,
		BlockIdSurfaceShallow = 390,
		BlockIdSurfaceDeep = 400,
		SimpleBlockIds = [
			397, 398, // Plasma-full-block
			390, 399, // Plasma-shallow-block
			391, 392, 393, 394, 395, 396, 400], // Plasma-surface-block
	};

	// For simple blocks only:
	internal static LiquidFamilyId GetFamilyId(ushort blockId)
	{
		switch (blockId)
		{
			case 198: return LiquidFamilyId.BottomlessSwamp;
			case 199: return LiquidFamilyId.BottomlessSwamp;
			case 347: return LiquidFamilyId.BottomlessSwamp;
			case 191: return LiquidFamilyId.BottomlessSwamp;
			case 192: return LiquidFamilyId.BottomlessSwamp;
			case 193: return LiquidFamilyId.BottomlessSwamp;
			case 194: return LiquidFamilyId.BottomlessSwamp;
			case 195: return LiquidFamilyId.BottomlessSwamp;
			case 196: return LiquidFamilyId.BottomlessSwamp;
			case 197: return LiquidFamilyId.BottomlessSwamp;
			case 387: return LiquidFamilyId.BottomlessSwamp;
			case 120: return LiquidFamilyId.ClearWater;
			case 128: return LiquidFamilyId.ClearWater;
			case 145: return LiquidFamilyId.ClearWater;
			case 343: return LiquidFamilyId.ClearWater;
			case 121: return LiquidFamilyId.ClearWater;
			case 122: return LiquidFamilyId.ClearWater;
			case 123: return LiquidFamilyId.ClearWater;
			case 142: return LiquidFamilyId.ClearWater;
			case 143: return LiquidFamilyId.ClearWater;
			case 144: return LiquidFamilyId.ClearWater;
			case 383: return LiquidFamilyId.ClearWater;
			case 230: return LiquidFamilyId.HotWater;
			case 231: return LiquidFamilyId.HotWater;
			case 223: return LiquidFamilyId.HotWater;
			case 224: return LiquidFamilyId.HotWater;
			case 225: return LiquidFamilyId.HotWater;
			case 226: return LiquidFamilyId.HotWater;
			case 227: return LiquidFamilyId.HotWater;
			case 228: return LiquidFamilyId.HotWater;
			case 229: return LiquidFamilyId.HotWater;
			case 344: return LiquidFamilyId.HotWater;
			case 384: return LiquidFamilyId.HotWater;
			case 259: return LiquidFamilyId.Lava;
			case 346: return LiquidFamilyId.Lava;
			case 260: return LiquidFamilyId.Lava;
			case 261: return LiquidFamilyId.Lava;
			case 262: return LiquidFamilyId.Lava;
			case 263: return LiquidFamilyId.Lava;
			case 264: return LiquidFamilyId.Lava;
			case 265: return LiquidFamilyId.Lava;
			case 266: return LiquidFamilyId.Lava;
			case 267: return LiquidFamilyId.Lava;
			case 386: return LiquidFamilyId.Lava;
			case 207: return LiquidFamilyId.MuddyWater;
			case 208: return LiquidFamilyId.MuddyWater;
			case 200: return LiquidFamilyId.MuddyWater;
			case 348: return LiquidFamilyId.MuddyWater;
			case 201: return LiquidFamilyId.MuddyWater;
			case 202: return LiquidFamilyId.MuddyWater;
			case 203: return LiquidFamilyId.MuddyWater;
			case 204: return LiquidFamilyId.MuddyWater;
			case 205: return LiquidFamilyId.MuddyWater;
			case 206: return LiquidFamilyId.MuddyWater;
			case 388: return LiquidFamilyId.MuddyWater;
			case 397: return LiquidFamilyId.Plasma;
			case 398: return LiquidFamilyId.Plasma;
			case 390: return LiquidFamilyId.Plasma;
			case 399: return LiquidFamilyId.Plasma;
			case 391: return LiquidFamilyId.Plasma;
			case 392: return LiquidFamilyId.Plasma;
			case 393: return LiquidFamilyId.Plasma;
			case 394: return LiquidFamilyId.Plasma;
			case 395: return LiquidFamilyId.Plasma;
			case 396: return LiquidFamilyId.Plasma;
			case 400: return LiquidFamilyId.Plasma;
			case 189: return LiquidFamilyId.Poison;
			case 190: return LiquidFamilyId.Poison;
			case 182: return LiquidFamilyId.Poison;
			case 345: return LiquidFamilyId.Poison;
			case 183: return LiquidFamilyId.Poison;
			case 184: return LiquidFamilyId.Poison;
			case 185: return LiquidFamilyId.Poison;
			case 186: return LiquidFamilyId.Poison;
			case 187: return LiquidFamilyId.Poison;
			case 188: return LiquidFamilyId.Poison;
			case 385: return LiquidFamilyId.Poison;
			case 340: return LiquidFamilyId.Seawater;
			case 341: return LiquidFamilyId.Seawater;
			case 333: return LiquidFamilyId.Seawater;
			case 349: return LiquidFamilyId.Seawater;
			case 420: return LiquidFamilyId.Seawater;
			case 334: return LiquidFamilyId.Seawater;
			case 335: return LiquidFamilyId.Seawater;
			case 336: return LiquidFamilyId.Seawater;
			case 337: return LiquidFamilyId.Seawater;
			case 338: return LiquidFamilyId.Seawater;
			case 339: return LiquidFamilyId.Seawater;
			case 389: return LiquidFamilyId.Seawater;
			default: return LiquidFamilyId.None;
		}
	}
}
