using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

public static class DQB2Constants
{
	public const int MaxElevation = 96;
	public const int ChunkSizeXZ = 32;

	public static class BlockId
	{
		public const ushort Empty = 0;
		public const ushort Bedrock = 1;
	}

	const ushort CanonicalBlockMask = Block.Mask_CanonicalBlockId;
	const int FirstPropId = 1158;
	const int LiquidsPerShell = 8;
	const int ImmersionsPerLiquid = 11;
	const int PropShellSize = LiquidsPerShell * ImmersionsPerLiquid + 1; // last one is for the "not submerged" case

	internal static bool IsSimple(this ushort block) => (block & CanonicalBlockMask) < FirstPropId; // equivalent to HH `simple?` proc
	internal static bool IsProp(this ushort block) => (block & CanonicalBlockMask) >= FirstPropId;
	internal static bool IsEmptyBlock(this ushort block) => block == BlockId.Empty;

	internal static Block ToBlock(this ushort block) => Block.Lookup(block);

	internal static PropShellIndex GetPropShellIndex(this ushort blockId)
	{
		int x = blockId & CanonicalBlockMask;
		x = x + (PropShellSize - FirstPropId); // Add PropShellSize because the enum starts at 1
		x = Math.Max(0, x);
		x = x / PropShellSize;
		return (PropShellIndex)x;
	}

	internal static LiquidFamilyIndex GetLiquidFamilyIndex(this ushort blockId)
	{
		if (blockId.IsSimple())
		{
			return GetLiquidFamily_ForSimpleBlocksOnly(blockId);
		}

		int offsetWithinShell = (blockId & CanonicalBlockMask) - FirstPropId;
		offsetWithinShell = offsetWithinShell % PropShellSize;
		// assert: 0 <= offsetWithinShell < 89
		int liquidId = offsetWithinShell / ImmersionsPerLiquid;
		liquidId += 1; // because the enum is 1-based

		// The "not submerged" case naturally lands on END. Remap it to None:
		liquidId = liquidId % (int)LiquidFamilyIndex.END;

		return (LiquidFamilyIndex)liquidId;
	}

	internal static ImmersionIndex GetImmersionIndex(this ushort blockId)
	{
		if (blockId.IsSimple())
		{
			return ImmersionIndex.None;
		}

		int offset = (blockId & CanonicalBlockMask) - FirstPropId;
		offset = offset % PropShellSize; // offset within shell
		int decrement = offset / (PropShellSize - 1); // will be 1 if this is the last value of the shell, else 0
		offset = offset % ImmersionsPerLiquid; // offset within liquid family
		offset += 1; // because the enum is 1-based
		offset -= decrement;

		return (ImmersionIndex)offset;
	}

	private static LiquidFamilyIndex GetLiquidFamily_ForSimpleBlocksOnly(ushort blockId)
	{
		switch (blockId)
		{
			case 198: return LiquidFamilyIndex.BottomlessSwamp;
			case 199: return LiquidFamilyIndex.BottomlessSwamp;
			case 347: return LiquidFamilyIndex.BottomlessSwamp;
			case 191: return LiquidFamilyIndex.BottomlessSwamp;
			case 192: return LiquidFamilyIndex.BottomlessSwamp;
			case 193: return LiquidFamilyIndex.BottomlessSwamp;
			case 194: return LiquidFamilyIndex.BottomlessSwamp;
			case 195: return LiquidFamilyIndex.BottomlessSwamp;
			case 196: return LiquidFamilyIndex.BottomlessSwamp;
			case 197: return LiquidFamilyIndex.BottomlessSwamp;
			case 387: return LiquidFamilyIndex.BottomlessSwamp;
			case 120: return LiquidFamilyIndex.ClearWater;
			case 128: return LiquidFamilyIndex.ClearWater;
			case 145: return LiquidFamilyIndex.ClearWater;
			case 343: return LiquidFamilyIndex.ClearWater;
			case 121: return LiquidFamilyIndex.ClearWater;
			case 122: return LiquidFamilyIndex.ClearWater;
			case 123: return LiquidFamilyIndex.ClearWater;
			case 142: return LiquidFamilyIndex.ClearWater;
			case 143: return LiquidFamilyIndex.ClearWater;
			case 144: return LiquidFamilyIndex.ClearWater;
			case 383: return LiquidFamilyIndex.ClearWater;
			case 230: return LiquidFamilyIndex.HotWater;
			case 231: return LiquidFamilyIndex.HotWater;
			case 223: return LiquidFamilyIndex.HotWater;
			case 224: return LiquidFamilyIndex.HotWater;
			case 225: return LiquidFamilyIndex.HotWater;
			case 226: return LiquidFamilyIndex.HotWater;
			case 227: return LiquidFamilyIndex.HotWater;
			case 228: return LiquidFamilyIndex.HotWater;
			case 229: return LiquidFamilyIndex.HotWater;
			case 344: return LiquidFamilyIndex.HotWater;
			case 384: return LiquidFamilyIndex.HotWater;
			case 259: return LiquidFamilyIndex.Lava;
			case 346: return LiquidFamilyIndex.Lava;
			case 260: return LiquidFamilyIndex.Lava;
			case 261: return LiquidFamilyIndex.Lava;
			case 262: return LiquidFamilyIndex.Lava;
			case 263: return LiquidFamilyIndex.Lava;
			case 264: return LiquidFamilyIndex.Lava;
			case 265: return LiquidFamilyIndex.Lava;
			case 266: return LiquidFamilyIndex.Lava;
			case 267: return LiquidFamilyIndex.Lava;
			case 386: return LiquidFamilyIndex.Lava;
			case 207: return LiquidFamilyIndex.MuddyWater;
			case 208: return LiquidFamilyIndex.MuddyWater;
			case 200: return LiquidFamilyIndex.MuddyWater;
			case 348: return LiquidFamilyIndex.MuddyWater;
			case 201: return LiquidFamilyIndex.MuddyWater;
			case 202: return LiquidFamilyIndex.MuddyWater;
			case 203: return LiquidFamilyIndex.MuddyWater;
			case 204: return LiquidFamilyIndex.MuddyWater;
			case 205: return LiquidFamilyIndex.MuddyWater;
			case 206: return LiquidFamilyIndex.MuddyWater;
			case 388: return LiquidFamilyIndex.MuddyWater;
			case 397: return LiquidFamilyIndex.Plasma;
			case 398: return LiquidFamilyIndex.Plasma;
			case 390: return LiquidFamilyIndex.Plasma;
			case 399: return LiquidFamilyIndex.Plasma;
			case 391: return LiquidFamilyIndex.Plasma;
			case 392: return LiquidFamilyIndex.Plasma;
			case 393: return LiquidFamilyIndex.Plasma;
			case 394: return LiquidFamilyIndex.Plasma;
			case 395: return LiquidFamilyIndex.Plasma;
			case 396: return LiquidFamilyIndex.Plasma;
			case 400: return LiquidFamilyIndex.Plasma;
			case 189: return LiquidFamilyIndex.Poison;
			case 190: return LiquidFamilyIndex.Poison;
			case 182: return LiquidFamilyIndex.Poison;
			case 345: return LiquidFamilyIndex.Poison;
			case 183: return LiquidFamilyIndex.Poison;
			case 184: return LiquidFamilyIndex.Poison;
			case 185: return LiquidFamilyIndex.Poison;
			case 186: return LiquidFamilyIndex.Poison;
			case 187: return LiquidFamilyIndex.Poison;
			case 188: return LiquidFamilyIndex.Poison;
			case 385: return LiquidFamilyIndex.Poison;
			case 340: return LiquidFamilyIndex.Seawater;
			case 341: return LiquidFamilyIndex.Seawater;
			case 333: return LiquidFamilyIndex.Seawater;
			case 349: return LiquidFamilyIndex.Seawater;
			case 420: return LiquidFamilyIndex.Seawater;
			case 334: return LiquidFamilyIndex.Seawater;
			case 335: return LiquidFamilyIndex.Seawater;
			case 336: return LiquidFamilyIndex.Seawater;
			case 337: return LiquidFamilyIndex.Seawater;
			case 338: return LiquidFamilyIndex.Seawater;
			case 339: return LiquidFamilyIndex.Seawater;
			case 389: return LiquidFamilyIndex.Seawater;
			default: return LiquidFamilyIndex.None;
		}
	}
}
