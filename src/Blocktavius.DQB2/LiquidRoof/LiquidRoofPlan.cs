using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2.LiquidRoof;

/// <summary>
/// A clever minimap update trick, thanks to Sapphire.
/// Creates a liquid roof; touching it with a block in-game will cause the liquid to
/// be removed and the minimap to be updated.
/// NOMERGE - Write a snapshot test before merging this.
/// </summary>
/// <remarks>
/// Separated into a "plan" and "apply" phases.
/// The plan is created by <see cref="Create"/>, which locks in the elevation
/// to be used for each roof segment.
/// Applying the mutation (returned by <see cref="GetMutation"/>) will only
/// overwrite Points which are empty.
/// This allows you to place more blocks between the plan and apply which may pierce
/// through the roof, which could be desirable for sparse and tall things like trees.
/// </remarks>
public sealed class LiquidRoofPlan
{
	private readonly IReadOnlyList<RoofSegment> segments;
	private readonly LiquidRoofOptions options;

	private LiquidRoofPlan(IReadOnlyList<RoofSegment> segments, LiquidRoofOptions options)
	{
		this.segments = segments;
		this.options = options;
	}

	sealed record RoofSegment
	{
		public required IReadOnlyList<ChunkOffset> Offsets { get; init; }

		/// <summary>
		/// Used to implement the buffer that separates segments.
		/// </summary>
		public required I2DSampler<bool> Area { get; init; }

		public required int Elevation { get; init; }
	}

	public StageMutation GetMutation() => new PutRoofMutation { Plan = this };

	public static LiquidRoofPlan Create(IStage stage) => Create(stage, LiquidRoofOptions.Default);

	public static LiquidRoofPlan Create(IStage stage, LiquidRoofOptions options)
	{
		if (options.SegmentSizeInChunks < 1)
		{
			options = options with { SegmentSizeInChunks = LiquidRoofOptions.Default.SegmentSizeInChunks };
		}

		int segmentDimension = options.SegmentSizeInChunks;

		var segments = new List<RoofSegment>();

		var relevantChunks = stage.ChunksInUse
			.Where(o => options.FilterArea == null || o.Bounds.Intersects(options.FilterArea.Bounds))
			.ToList();

		// Caution: using unscaled XZs here
		var offsetRect = new Rect.BoundsFinder()
			.IncludeAll(relevantChunks.Select(o => o.RawUnscaledOffset))
			.CurrentBounds() ?? Rect.Zero;

		for (int segStartZ = offsetRect.start.Z; segStartZ <= offsetRect.end.Z; segStartZ += segmentDimension)
		{
			for (int segStartX = offsetRect.start.X; segStartX <= offsetRect.end.X; segStartX += segmentDimension)
			{
				var segmentStart = new XZ(segStartX, segStartZ);
				var segmentRect = new Rect(segmentStart, segmentStart.Add(segmentDimension, segmentDimension));
				if (TryCreateSegment(segmentRect, options, stage, out var segment))
				{
					segments.Add(segment);
				}
			}
		}

		return new LiquidRoofPlan(segments, options);
	}

	private static bool TryCreateSegment(Rect offsetRect, LiquidRoofOptions options, IStage stage, out RoofSegment segment)
	{
		int elevation = -1;
		List<ChunkOffset> offsets = new();
		foreach (var xz in offsetRect.Enumerate())
		{
			var offset = new ChunkOffset(xz.X, xz.Z);
			if (stage.TryReadChunk(offset, out var chunk))
			{
				offsets.Add(offset);
				elevation = Math.Max(elevation, GetMaxElevation(chunk, options.FilterArea));
			}
		}

		if (offsets.Count == 0)
		{
			segment = default!;
			return false;
		}

		var segmentArea = new Rect.BoundsFinder()
			.IncludeAll(offsets.Select(o => o.Bounds.start))
			.IncludeAll(offsets.Select(o => o.Bounds.end))
			.CurrentBounds() ?? Rect.Zero;

		// Create a buffer between segments, otherwise segments with the same
		// elevation would not be segmented from each other.
		segmentArea = segmentArea.Expand(-1);

		// Don't exceed max elevation
		elevation = Math.Min(elevation + options.YBoost, DQB2Constants.MaxElevation - 1);

		segment = new RoofSegment
		{
			Area = segmentArea.AsArea().AsSampler(),
			Elevation = elevation,
			Offsets = offsets,
		};
		return true;
	}

	private static int GetMaxElevation(IChunk chunk, I2DSampler<bool>? filterArea)
	{
		int maxElevation = 0;
		foreach (var xz in chunk.Offset.Bounds.Enumerate(filterArea))
		{
			for (int y = DQB2Constants.MaxElevation - 1; y > 0; y--)
			{
				if (!chunk.GetBlock(new Point(xz, y)).IsEmptyBlock())
				{
					maxElevation = Math.Max(maxElevation, y);
					break;
				}
			}
		}
		return maxElevation;
	}

	class PutRoofMutation : StageMutation
	{
		public required LiquidRoofPlan Plan { get; init; }

		internal override void Apply(IMutableStage stage)
		{
			ushort block = Plan.options.RoofBlockId;
			var filterArea = Plan.options.FilterArea;

			foreach (var segment in Plan.segments)
			{
				var segmentArea = segment.Area;

				foreach (var offset in segment.Offsets)
				{
					if (stage.TryGetChunk(offset, out var chunk))
					{
						foreach (var xz in offset.Bounds.Enumerate(filterArea).Where(segmentArea.InArea))
						{
							var point = new Point(xz, segment.Elevation);
							if (chunk.GetBlock(point).IsEmptyBlock() && chunk.HasFoundationAt(xz))
							{
								chunk.SetBlock(point, block);
							}
						}
					}
				}
			}
		}
	}
}
