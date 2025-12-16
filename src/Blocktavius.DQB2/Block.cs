using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

public enum PropShellIndex
{
	None = 0,
	Fence = 1,
	FryingPan = 2,
	Track = 3,
	Roof = 4,
	Table = 5,
	Lighting = 6,
	Fixture = 7,
	Door = 8,
	Unknown = 9,
	Generic = 10,
	END = 11,
}

/// <summary>
/// Applies to simple blocks and to submerged props.
/// </summary>
public enum LiquidFamilyIndex
{
	None = 0,
	ClearWater = 1,
	HotWater = 2,
	Poison = 3,
	Lava = 4,
	BottomlessSwamp = 5,
	MuddyWater = 6,
	Seawater = 7,
	Plasma = 8,
	END = 9
}

/// <summary>
/// When a Prop is submerged in a liquid, it has a non-zero Immersion Index.
/// The hierarchy is Prop Shell -> Liquid Family -> Immersion Index.
/// TODO - These could use better names... Is "flowingness" is relevant here?
/// </summary>
public enum ImmersionIndex
{
	None = 0,
	Full1 = 1,
	Shallow2 = 2,
	Surface3 = 3,
	Small4 = 4,
	Surface5 = 5,
	Surface6 = 6,
	Surface7 = 7,
	Surface8 = 8,
	Surface9 = 9,
	Surface10 = 10,
	Full11 = 11,
	END = 12,
}

public readonly struct Block : IEquatable<Block>, IComparable<Block>
{
	/// <summary>
	/// This enum will be used to represent a packed integer having the following bit layout:
	/// * 16 bits for the ushort block ID
	/// * 4 bits for the <see cref="PropShellIndex"/>
	/// * 4 bits for the <see cref="LiquidFamilyIndex"/>
	/// * 4 bits for the <see cref="ImmersionIndex"/>
	/// * 1 bit for an "Is Prop?" flag (could easily reclaim this bit later if needed)
	/// </summary>
	enum PackedBlockInfo : int { }
	const int Mask_BlockId /*******/ = 0x0000_FFFF;
	const int Mask_PropShell /*****/ = 0x000F_0000;
	const int Mask_LiquidFamily /**/ = 0x00F0_0000;
	const int Mask_Immersion /*****/ = 0x0F00_0000;
	const int Mask_IsProp /********/ = 0x1000_0000;

	const int Shift_PropShell = 16;
	const int Shift_LiquidFamily = 20;
	const int Shift_Immersion = 24;

	internal const int Mask_CanonicalBlockId = 0x7FF;
	internal const int CanonicalBlockCount = 0x800;


	private readonly int val;

	private Block(ushort blockId, PackedBlockInfo blockInfo)
	{
		this.val = blockId | (int)blockInfo;
	}

	public static Block Lookup(ushort blockId)
	{
		PackedBlockInfo flags = lookup[blockId];
		return new Block(blockId, flags);
	}

	public ushort CompleteBlockId => (ushort)(val & Mask_BlockId);

	/// <summary>
	/// Removes the chisel status and the alleged "placed by player" bit.
	/// </summary>
	public ushort CanonicalBlockId => (ushort)(val & Mask_CanonicalBlockId);

	public PropShellIndex PropShellIndex => (PropShellIndex)((val & Mask_PropShell) >> Shift_PropShell);
	public LiquidFamilyIndex LiquidFamilyIndex => (LiquidFamilyIndex)((val & Mask_LiquidFamily) >> Shift_LiquidFamily);
	public ImmersionIndex ImmersionIndex => (ImmersionIndex)((val & Mask_Immersion) >> Shift_Immersion);
	public bool IsProp => (val & Mask_IsProp) != 0;



	private static readonly MaskedBlockLookup<PackedBlockInfo> lookup;

	static Block()
	{
		lookup = new();
		for (ushort blockId = 0; blockId < CanonicalBlockCount; blockId++)
		{
			var propShellIndex = blockId.GetPropShellIndex();
			var liquidIndex = blockId.GetLiquidFamilyIndex();
			var immersionIndex = blockId.GetImmersionIndex();

			int flags = 0;
			flags |= ((int)propShellIndex) << Shift_PropShell;
			flags |= ((int)liquidIndex) << Shift_LiquidFamily;
			flags |= ((int)immersionIndex) << Shift_Immersion;

			if (blockId.IsProp())
			{
				flags = flags | Mask_IsProp;
			}

			lookup[blockId] = (PackedBlockInfo)flags;
		}
	}

	public bool Equals(Block other) => this.val == other.val;
	public int CompareTo(Block other) => this.val.CompareTo(other.val);

	public override bool Equals([NotNullWhen(true)] object? obj)
	{
		return obj is Block other && this.val == other.val;
	}

	public override int GetHashCode() => val.GetHashCode();

	public static bool operator ==(Block left, Block right) => left.val == right.val;
	public static bool operator !=(Block left, Block right) => left.val != right.val;
}
