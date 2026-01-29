using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

public sealed class LiquidFamily : RepairSea.ILiquid
{
	private LiquidFamily() { }

	public required LiquidFamilyIndex LiquidFamilyId { get; init; }
	public required ushort BlockIdSubsurface { get; init; }
	public required ushort BlockIdSurfaceHigh { get; init; }
	public required ushort BlockIdSurfaceLow { get; init; }

	public required IReadOnlyList<ushort> SimpleBlockIds { get; init; }

	public static bool TryGet(ushort blockId, out LiquidFamily liquidFamily)
	{
		var fam = blockId.GetLiquidFamilyIndex();
		return TryGetByIndex(fam, out liquidFamily);
	}

	public static bool TryGetByIndex(LiquidFamilyIndex fam, out LiquidFamily liquidFamily)
	{
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

	private static LiquidFamily Create(LiquidFamilyIndex fam, ushort subsurface, ushort low, ushort high)
	{
		var simpleIds = Block.IterateSimpleBlocks()
			.Where(x => x.LiquidFamilyIndex == fam)
			.Select(b => b.BlockIdCanonical)
			.ToList();

		bool ok = simpleIds.Contains(subsurface) && simpleIds.Contains(low) && simpleIds.Contains(high);
		if (!ok) { throw new Exception("Assert fail (contains)"); }

		// seawater has an extra one for some reason
		int expectedCount = (fam == LiquidFamilyIndex.Seawater) ? 12 : 11;
		if (simpleIds.Count != expectedCount) { throw new Exception("Assert fail (count)"); }

		return new LiquidFamily
		{
			LiquidFamilyId = fam,
			BlockIdSubsurface = subsurface,
			BlockIdSurfaceLow = low,
			BlockIdSurfaceHigh = high,
			SimpleBlockIds = simpleIds,
		};
	}

	public static readonly LiquidFamily ClearWater = Create(LiquidFamilyIndex.ClearWater, 128, 343, 383);

	public static readonly LiquidFamily HotWater = Create(LiquidFamilyIndex.HotWater, 231, 344, 384);

	public static readonly LiquidFamily Poison = Create(LiquidFamilyIndex.Poison, 190, 345, 385);

	public static readonly LiquidFamily Lava = Create(LiquidFamilyIndex.Lava, 267, 346, 386);

	// "This water can't be scooped up."
	public static readonly LiquidFamily BottomlessSwamp = Create(LiquidFamilyIndex.BottomlessSwamp, 199, 347, 387);

	public static readonly LiquidFamily MuddyWater = Create(LiquidFamilyIndex.MuddyWater, 208, 348, 388);

	// It seems best to choose 349 (minimap normal sea) over 420 (minimap deep sea) here:
	public static readonly LiquidFamily Seawater = Create(LiquidFamilyIndex.Seawater, 341, 349, 389);

	public static readonly LiquidFamily Plasma = Create(LiquidFamilyIndex.Plasma, 398, 399, 400);

	Block RepairSea.ILiquid.ChangePropShell(ref Block.Prop prop, LiquidAmountIndex amount)
	{
		return prop.SetLiquid(this.LiquidFamilyId, amount);
	}
}
