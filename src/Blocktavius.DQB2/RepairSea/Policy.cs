using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2.RepairSea;

sealed class Policy : IPolicy
{
	[Flags]
	enum Flags
	{
		CanBePartOfSea = 1,
		ShouldOverwrite = 2,
	}

	private readonly MaskedBlockLookup<Flags> lookup;
	private Policy(MaskedBlockLookup<Flags> lookup)
	{
		this.lookup = lookup;
	}

	public bool CanBePartOfSea(ushort blockId) => lookup[blockId].HasFlag(Flags.CanBePartOfSea);
	public bool ShouldOverwriteWhenPartOfSea(ushort blockId) => lookup[blockId].HasFlag(Flags.ShouldOverwrite);

	public static Policy TODO_DefaultPolicy() // NOMERGE
	{
		var lookup = new MaskedBlockLookup<Flags>();
		foreach (ushort blockId in lookup.Keys)
		{
			if (blockId.IsProp())
			{
				// All item IDs can be included in the sea, but they cannot be overwritten.
				// (More advanced analysis might consider whether the item is porous,
				//  but that doesn't seem necessary yet.)
				lookup[blockId] = Flags.CanBePartOfSea;
			}
			else if (blockId == DQB2Constants.BlockId.Empty || blockId.GetLiquidFamilyIndex() != LiquidFamilyIndex.None)
			{
				lookup[blockId] = Flags.CanBePartOfSea | Flags.ShouldOverwrite;
			}
		}

		return new Policy(lookup);
	}
}
