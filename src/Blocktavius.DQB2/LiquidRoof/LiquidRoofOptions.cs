using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2.LiquidRoof;

public sealed record LiquidRoofOptions
{
	/// <summary>
	/// The roof will be divided into segments of NxN chunks;
	/// this property specifies that N.
	/// The primary reason for segmentation is because the amount of in-game
	/// lag increases with larger segments. At extreme sizes, the segment
	/// might not even be fully removed when you touch it.
	/// </summary>
	public int SegmentSizeInChunks { get; init; } = 3;

	/// <summary>
	/// In each segment, the roof will be placed this many voxels
	/// above the max elevation found in that segment.
	/// </summary>
	public int YBoost { get; init; } = 2;

	/// <summary>
	/// Block ID to use for the roof.
	/// Should be a "runoff" liquid ID which disappears when touched.
	/// Defaults to Poison, a good choice due to its high visibility.
	/// </summary>
	/// <remarks>
	/// You could choose a non-liquid roof, but I can't imagine why.
	/// </remarks>
	public ushort RoofBlockId { get; init; } = 183;

	/// <summary>
	/// When not null, the roof may not exceed this area.
	/// </summary>
	public I2DSampler<bool>? FilterArea { get; init; } = null;

	public static LiquidRoofOptions Default { get; } = new();
}
