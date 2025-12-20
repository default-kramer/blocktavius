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
	const int FirstPropId = Block.FirstPropId;

	internal static bool IsSimple(this ushort block) => (block & CanonicalBlockMask) < FirstPropId; // equivalent to HH `simple?` proc
	internal static bool IsProp(this ushort block) => (block & CanonicalBlockMask) >= FirstPropId;
	internal static bool IsEmptyBlock(this ushort block) => block == BlockId.Empty;

	internal static Block ToBlock(this ushort block) => Block.Lookup(block);

	internal static LiquidFamilyIndex GetLiquidFamilyIndex(this ushort blockId) => blockId.ToBlock().LiquidFamilyIndex;
}
