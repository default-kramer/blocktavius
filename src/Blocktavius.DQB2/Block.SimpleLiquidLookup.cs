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
		// Here "stable" could also be called "source".
		// It seems the game performs the runoff logic when you place or remove a block
		// adjacent to some liquid. If you play the game normally, it is impossible to
		// have runoff that isn't coming from some source block.
		// But if you use save file editing, you can create disconnected runoff that will
		// disappear whenever its runoff logic is recalculated.
		//
		// The shallow/zero runoff renders strangely when it is not adjacent to a deeper
		// block of the same liquid (which should be impossible when playing normally).
		// For example, if you place some disconnected shallow/zero Lava it barely renders
		// at all (but still creates splashes and burns the builder).

		switch (blockId)
		{
			case 199: // stable, subsurface
			case 347: // stable, surface low
			case 387: // stable, surface high
			case 191: // runoff, shallow/zero
			case 192: // runoff, deep...
			case 193:
			case 194:
			case 195:
			case 196:
			case 197:
			case 198: // runoff, full
				return LiquidFamilyIndex.BottomlessSwamp;

			case 128: // stable, subsurface
			case 343: // stable, surface low
			case 383: // stable, surface high
			case 145: // runoff, shallow/zero
			case 121: // runoff, deep (unconfirmed) ...
			case 122:
			case 123:
			case 142:
			case 143:
			case 144:
			case 120: // runoff, full
				return LiquidFamilyIndex.ClearWater;

			case 231: // stable, subsurface
			case 344: // stable, surface low
			case 384: // stable, surface high
			case 223: // runoff, shallow/zero
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
			case 259: // runoff, shallow/zero
			case 260: // runoff, deep...
			case 261:
			case 262:
			case 263:
			case 264:
			case 265:
			case 266: // runoff, full
				return LiquidFamilyIndex.Lava;

			case 208: // stable, subsurface
			case 348: // stable, surface low
			case 388: // stable, surface high
			case 200: // runoff, shallow/zero
			case 201: // runoff, deep...
			case 202:
			case 203:
			case 204:
			case 205:
			case 206:
			case 207: // runoff, full
				return LiquidFamilyIndex.MuddyWater;

			case 398: // stable, subsurface
			case 399: // stable, surface low
			case 400: // stable, surface high
			case 390: // runoff, shallow/zero
			case 391: // runoff, deep...
			case 392:
			case 393:
			case 394:
			case 395:
			case 396:
			case 397: // runoff, full
				return LiquidFamilyIndex.Plasma;

			case 190: // stable, subsurface
			case 345: // stable, surface low
			case 385: // stable, surface high
			case 182: // runoff, shallow/zero
			case 183: // runoff, deep...
			case 184:
			case 185:
			case 186:
			case 187:
			case 188:
			case 189: // runoff, full
				return LiquidFamilyIndex.Poison;

			case 341: // stable, subsurface
			case 349: // stable, surface low
			case 389: // stable, surface high
			case 333: // runoff, shallow/zero
			case 334: // runoff, deep...
			case 335:
			case 336:
			case 337:
			case 338:
			case 339:
			case 340: // runoff, full
			case 420: // 420 is a special variant of 349 (stable, surface low) which causes the minimap
					  // to render deep sea. This also enables the seagull and ocean noises.
				return LiquidFamilyIndex.Seawater;

			default: return LiquidFamilyIndex.None;
		}
	}
}
