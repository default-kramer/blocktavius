using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2.LiquidRoof;

/// <summary>
/// Creates a liquid roof; touching it with a block in-game will update the minimap.
/// NOMERGE - Write a snapshot test before merging this.
/// </summary>
/// <remarks>
/// Separated into a "plan" and "apply" phases.
/// The plan is created by <see cref="Create(IStage)"/>, which locks in the elevation
/// to be used for each roof segment.
/// Applying the mutation (returned by <see cref="GetMutation"/>) will only
/// overwrite Points which are empty.
/// This allows you to place more blocks between the plan and apply which may
/// pierce through the roof, which could be good for sparse tall things like trees.
/// </remarks>
public sealed class LiquidRoofPlan
{
	private readonly IReadOnlyList<Entry> entries;

	private LiquidRoofPlan(IReadOnlyList<Entry> entries)
	{
		this.entries = entries;
	}

	sealed record Entry
	{
		public required IReadOnlyList<ChunkOffset> Offsets { get; init; }
		public required I2DSampler<bool> Area { get; init; }
		public required int Elevation { get; init; }
	}

	public StageMutation GetMutation() => new PutRoofMutation { Plan = this };

	public static LiquidRoofPlan Create(IStage stage)
	{
		const int entryDimension = 3; // 3x3 chunks per entry
		const int yBoost = 2; // how far off the (maximum) ground should we put the roof?

		var entries = new List<Entry>();

		var offsetRect = new Rect.BoundsFinder()
			.IncludeAll(stage.ChunksInUse.Select(o => o.RawUnscaledOffset))
			.CurrentBounds() ?? Rect.Zero;

		for (int jumpZ = offsetRect.start.Z; jumpZ <= offsetRect.end.Z; jumpZ += entryDimension)
		{
			for (int jumpX = offsetRect.start.X; jumpX <= offsetRect.end.X; jumpX += entryDimension)
			{
				// create an Entry
				int elevation = -1;
				List<ChunkOffset> offsets = new();
				for (int z = jumpZ; z < jumpZ + entryDimension; z++)
				{
					for (int x = jumpX; x < jumpX + entryDimension; x++)
					{
						var offset = new ChunkOffset(x, z);
						if (stage.TryReadChunk(offset, out var chunk))
						{
							offsets.Add(offset);
							elevation = Math.Max(elevation, GetMaxElevation(chunk));
						}
					}
				}
				if (offsets.Count > 0)
				{
					var areaRect = new Rect.BoundsFinder()
						.IncludeAll(offsets.Select(o => o.Bounds.start))
						.IncludeAll(offsets.Select(o => o.Bounds.end))
						.CurrentBounds() ?? Rect.Zero;

					// create a 1-voxel buffer between regions
					areaRect = areaRect.Expand(-1);

					// make sure we don't exceed max elevation
					elevation = Math.Min(elevation + yBoost, DQB2Constants.MaxElevation - 1);

					entries.Add(new Entry
					{
						Area = areaRect.AsArea().AsSampler(),
						Elevation = elevation,
						Offsets = offsets,
					});
				}
			}
		}

		return new LiquidRoofPlan(entries);
	}

	private static int GetMaxElevation(IChunk chunk)
	{
		int max = 0;
		foreach (var xz in chunk.Offset.Bounds.Enumerate())
		{
			for (int y = DQB2Constants.MaxElevation - 1; y > 0; y--)
			{
				if (chunk.GetBlock(new Point(xz, y)) != 0)
				{
					max = Math.Max(max, y);
					break;
				}
			}
		}
		return max;
	}

	class PutRoofMutation : StageMutation
	{
		public required LiquidRoofPlan Plan { get; init; }

		internal override void Apply(IMutableStage stage)
		{
			ushort block = 183; // poison runoff, very visible

			foreach (var entry in Plan.entries)
			{
				var area = entry.Area;

				foreach (var offset in entry.Offsets)
				{
					if (stage.TryGetChunk(offset, out var chunk))
					{
						foreach (var xz in offset.Bounds.Enumerate().Where(area.InArea))
						{
							var p = new Point(xz, entry.Elevation);
							if (chunk.GetBlock(p) == 0 && chunk.GetBlock(new Point(xz, 0)) != 0)
							{
								chunk.SetBlock(p, block);
							}
						}
					}
				}
			}
		}
	}
}
