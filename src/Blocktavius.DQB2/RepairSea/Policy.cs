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

	public bool CanBePartOfSea(ushort block) => lookup[block].HasFlag(Flags.CanBePartOfSea);
	public bool ShouldOverwriteWhenPartOfSea(Block block) => lookup[block.BlockIdCanonical].HasFlag(Flags.ShouldOverwrite);

	internal static Policy DefaultPolicy()
	{
		var lookup = new MaskedBlockLookup<Flags>();
		foreach (ushort blockId in lookup.Keys)
		{
			if (blockId.IsProp())
			{
				// More advanced analysis might consider whether the item is porous,
				// but that doesn't seem necessary yet.
				lookup[blockId] = Flags.CanBePartOfSea | Flags.ShouldOverwrite;
			}
			else if (blockId == DQB2Constants.BlockId.Empty || blockId.GetLiquidFamilyIndex() != LiquidFamilyIndex.None)
			{
				lookup[blockId] = Flags.CanBePartOfSea | Flags.ShouldOverwrite;
			}
		}

		return new Policy(lookup);
	}
}
