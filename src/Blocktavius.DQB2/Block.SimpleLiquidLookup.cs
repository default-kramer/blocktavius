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

			case 128: // stable, subsurface
			case 343: // stable, surface low
			case 383: // stable, surface high
			case 145: // runoff, shallow (also zero)
			case 121: // runoff, deep (unconfirmed) ...
			case 122:
			case 123:
			case 142:
			case 143:
			case 144:
			case 120: // runoff, full
				return LiquidFamilyIndex.ClearWater;

			// Confirmed that when you pour shallow hot water from the pot,
			// you get 344 for the main area with a border of 223 (border thickness = 1).
			//
			// I think 223 is expected to always be adjacent to at least one 344 (or 224, maybe)
			// in which case it looks normal. If you use modding to violate this,
			// you can see 233 render strangely as having zero height.
			case 231: // stable, subsurface
			case 344: // stable, surface low
			case 384: // stable, surface high
			case 223: // runoff, shallow (can be "zero height", see comment above)
			case 224: // runoff, deep...
			case 225:
			case 226:
			case 227:
			case 228:
			case 229:
			case 230: // runoff, full
				return LiquidFamilyIndex.HotWater;

			case 267: // stable, subsurface
			case 346: // stable, surface low
			case 386: // stable, surface high
			case 259: // runoff, shallow (mostly invisible when zero, still burns the builder)
			case 260: // runoff, deep...
			case 261:
			case 262:
			case 263:
			case 264:
			case 265:
			case 266: // runoff, full
				return LiquidFamilyIndex.Lava;

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

			case 190: // stable, subsurface
			case 345: // stable, surface low
			case 385: // stable, surface high
			case 182: // runoff, shallow (also zero)
			case 183: // runoff, deep...
			case 184:
			case 185:
			case 186:
			case 187:
			case 188:
			case 189: // runoff, full
				return LiquidFamilyIndex.Poison;

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
