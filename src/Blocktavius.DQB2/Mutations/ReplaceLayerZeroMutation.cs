using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2.Mutations;

public sealed class ReplaceLayerZeroMutation : StageMutation
{
	/// <summary>
	/// The mutation will only visit columns in this area.
	/// </summary>
	public required IArea Area { get; init; }

	/// <summary>
	/// If not null, the mutation will only visit colums having this block at Y=0.
	/// Defaults to null.
	/// </summary>
	public ushort? BlockIdToReplace { get; init; } = null;

	/// <summary>
	/// Should the mutation attempt to clear visited columns?
	/// Even if this is true, the current implementation cannot clear props.
	/// </summary>
	public bool AttemptToClearVisitedColumns { get; init; } = false;

	/// <summary>
	/// If <see cref="AttemptToClearVisitedColumns"/> is true, that will happen first.
	/// Either way, the column might or might not be empty.
	/// Should we perform the mutation on non-empty columns?
	/// Setting this to true can create a situation where blocks become
	/// indestructible because there is no bedrock at Y=0.
	/// </summary>
	public bool ProcessNonEmptyColumns { get; init; } = false;

	/// <summary>
	/// The block ID to set at Y=0. Defaults to emptiness.
	/// </summary>
	public ushort ReplacementBlockId { get; init; } = DQB2Constants.BlockId.Empty;

	internal override void Apply(IMutableStage stage)
	{
		foreach (var chunk in Enumerate(Area.Bounds, stage))
		{
			foreach (var xz in chunk.Offset.Bounds.Intersection(Area.Bounds).Enumerate())
			{
				var point = new Point(xz, 0);
				if (!BlockIdToReplace.HasValue || chunk.GetBlock(point) == BlockIdToReplace.Value)
				{
					bool isClear = AttemptToClearVisitedColumns ? ClearColumn(chunk, xz) : IsColumnEmpty(chunk, xz);
					if (isClear || ProcessNonEmptyColumns)
					{
						chunk.SetLayerZero(xz, ReplacementBlockId);
					}
				}
			}
		}
	}

	private static bool IsColumnEmpty(IMutableChunk chunk, XZ xz)
	{
		for (int y = 1; y < DQB2Constants.MaxElevation; y++)
		{
			if (chunk.GetBlock(new Point(xz, y)) != DQB2Constants.BlockId.Empty)
			{
				return false;
			}
		}

		return true;
	}

	private static bool ClearColumn(IMutableChunk chunk, XZ xz)
	{
		bool isEmpty = true;

		for (int y = 1; y < DQB2Constants.MaxElevation; y++)
		{
			var point = new Point(xz, y);
			if (chunk.GetBlock(point).IsProp())
			{
				isEmpty = false;
			}
			else
			{
				chunk.SetBlock(point, DQB2Constants.BlockId.Empty);
			}
		}

		return isEmpty;
	}
}
