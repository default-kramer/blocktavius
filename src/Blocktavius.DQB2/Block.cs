using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

public enum LiquidDepthIndex
{
	None = 0,
	Full = 1,
	SurfaceShallow = 2,
	SurfaceDeep = 3,
}

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

public readonly partial struct Block : IEquatable<Block>, IComparable<Block>
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

	private const int FirstPropId = 1158;
	private const int ImmersionsPerLiquid = 11;
	private const int LiquidsPerShell = 8;
	private const int PropShellSize = LiquidsPerShell * ImmersionsPerLiquid + 1; // last one is for the "not submerged" case


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

	public ushort BlockIdComplete => (ushort)(val & Mask_BlockId);

	/// <summary>
	/// Removes the chisel status and the alleged "placed by player" bit.
	/// </summary>
	public ushort BlockIdCanonical => (ushort)(val & Mask_CanonicalBlockId);

	public PropShellIndex PropShellIndex => (PropShellIndex)((val & Mask_PropShell) >> Shift_PropShell);
	public LiquidFamilyIndex LiquidFamilyIndex => (LiquidFamilyIndex)((val & Mask_LiquidFamily) >> Shift_LiquidFamily);
	public ImmersionIndex ImmersionIndex => (ImmersionIndex)((val & Mask_Immersion) >> Shift_Immersion);
	public bool IsProp => (val & Mask_IsProp) != 0;

	/// <summary>
	/// DOES NOT VALIDATE THE ARGS.
	/// Returns the canonical block ID for the given shell+liquid+immersion.
	/// </summary>
	private static int RecomputeProp(PropShellIndex propShell, LiquidFamilyIndex requestedFamily, ImmersionIndex immersion)
	{
		int iProp = propShell - (PropShellIndex.None + 1);
		int iLiquid = requestedFamily - (LiquidFamilyIndex.None + 1);
		int iImmersion = immersion - (ImmersionIndex.None + 1);

		int shellStart = FirstPropId + iProp * PropShellSize;
		if (requestedFamily == LiquidFamilyIndex.None)
		{
			// "not submerged" case is the last value of the shell
			return shellStart + PropShellSize - 1;
		}
		else
		{
			return shellStart + iLiquid * ImmersionsPerLiquid + iImmersion;
		}
	}

	private Block PreserveChisel(int newId)
	{
		newId &= Mask_CanonicalBlockId;
		newId |= this.val & (Mask_BlockId & ~Mask_CanonicalBlockId);
		return Lookup((ushort)newId);
	}

	public bool TryChangeLiquidFamily(LiquidFamilyIndex requestedFamily, out Block changedBlock)
	{
		if (this.LiquidFamilyIndex == LiquidFamilyIndex.None)
		{
			if (requestedFamily == LiquidFamilyIndex.None)
			{
				changedBlock = this;
				return true;
			}
			// Cannot change, don't know what depth/immersion to use
			changedBlock = default;
			return false;
		}

		if (!IsProp)
		{
			throw new Exception("TODO - need to handle simple blocks...");
		}

		int newId = RecomputeProp(this.PropShellIndex, requestedFamily, this.ImmersionIndex);
		changedBlock = PreserveChisel(newId);
		return true;
	}

	public Block SetLiquid(LiquidFamilyIndex liquid, LiquidDepthIndex depth)
	{
		if (liquid == LiquidFamilyIndex.None || depth == LiquidDepthIndex.None)
		{
			throw new ArgumentException("Must specify liquid and depth");
		}

		if (IsProp)
		{
			// This Depth -> Immersion case looks correct for Full and SurfaceShallow,
			// but still untested for SurfaceDeep.
			var immersion = (ImmersionIndex)depth;
			int newId = RecomputeProp(this.PropShellIndex, liquid, immersion);
			return PreserveChisel(newId);
		}
		else
		{
			throw new NotImplementedException("TODO??");
		}
	}

	public static IEnumerable<Block> IterateSimpleBlocks()
	{
		for (ushort i = 0; i < FirstPropId; i++)
		{
			yield return Lookup(i);
		}
	}

	private static readonly MaskedBlockLookup<PackedBlockInfo> lookup;

	static Block()
	{
		lookup = new();

		// Add simple blocks
		ushort blockId;
		for (blockId = 0; blockId < FirstPropId; blockId++)
		{
			var liquid = GetLiquidFamily_ForSimpleBlocksOnly(blockId);
			lookup[blockId] = Pack(PropShellIndex.None, liquid, ImmersionIndex.None, isProp: false);
		}
		blockId--; // undo the final ++ that exited the loop

		void AddProp(PropShellIndex propShell, LiquidFamilyIndex liquid, ImmersionIndex immersion)
		{
			int id = RecomputeProp(propShell, liquid, immersion);
			// validate that the loop covers all the block IDs in the order we expect:
			if (id == blockId + 1) { blockId++; }
			else { throw new Exception("Assert fail - out of order"); }
			lookup[blockId] = Pack(propShell, liquid, immersion, isProp: true);
		}

		// Add props
		for (var propShell = PropShellIndex.None + 1; propShell < PropShellIndex.END; propShell++)
		{
			for (var liquid = LiquidFamilyIndex.None + 1; liquid < LiquidFamilyIndex.END; liquid++)
			{
				for (var immersion = ImmersionIndex.None + 1; immersion < ImmersionIndex.END; immersion++)
				{
					AddProp(propShell, liquid, immersion);
				}
			}
			AddProp(propShell, LiquidFamilyIndex.None, ImmersionIndex.None);
		}

		if (blockId != 0x7FF)
		{
			throw new Exception("Assert fail - didn't cover all block IDs");
		}
	}

	private static PackedBlockInfo Pack(PropShellIndex propShell, LiquidFamilyIndex liquid, ImmersionIndex immersion, bool isProp)
	{
		int flags = 0;
		flags |= (int)propShell << Shift_PropShell;
		flags |= (int)liquid << Shift_LiquidFamily;
		flags |= (int)immersion << Shift_Immersion;
		if (isProp)
		{
			flags |= Mask_IsProp;
		}
		return (PackedBlockInfo)flags;
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
