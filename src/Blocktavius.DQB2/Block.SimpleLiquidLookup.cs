using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

partial struct Block
{
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
