using Blocktavius.Core;
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

	/// <summary>
	/// Returns true if the chunk has a nonzero/nonempty block at Y=0 for the given <paramref name="xz"/>.
	/// </summary>
	/// <remarks>
	/// The nonzero block is probably bedrock, but any nonzero block is sufficient.
	/// </remarks>
	internal static bool HasFoundationAt(this IChunk chunk, XZ xz)
	{
		var blockId = chunk.GetBlock(new Point(xz, 0));
		return Block.MakeCanonical(blockId) != 0;
	}

	internal static Chisel GetDiagonalChisel(this Direction dir)
	{
		return dir.Index switch
		{
			Direction.IndexConstants.North => Chisel.DiagonalNorth,
			Direction.IndexConstants.East => Chisel.DiagonalEast,
			Direction.IndexConstants.South => Chisel.DiagonalSouth,
			Direction.IndexConstants.West => Chisel.DiagonalWest,
			Direction.IndexConstants.NorthEast => Chisel.DiagonalNorthEast,
			Direction.IndexConstants.SouthEast => Chisel.DiagonalSouthEast,
			Direction.IndexConstants.SouthWest => Chisel.DiagonalSouthWest,
			Direction.IndexConstants.NorthWest => Chisel.DiagonalNorthWest,
			_ => Chisel.None,
		};
	}
}
