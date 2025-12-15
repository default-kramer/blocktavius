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

	internal const ushort blockMask = 0x7FF; // masks off the chisel and the alleged "placed by player" bit
	internal const ushort numDistinctMaskedBlocks = blockMask + 1;

	internal static bool IsSimple(this ushort block) => (block & blockMask) < 1158; // equivalent to HH `simple?` proc
	internal static bool IsItem(this ushort block) => !IsSimple(block);
	internal static bool IsEmptyBlock(this ushort block) => block == BlockId.Empty;
}
