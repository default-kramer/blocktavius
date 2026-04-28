using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core.Generators.Hills;

/// <summary>
/// Given a Jaunt, creates a cliff with a face well-suited for further work by a human builder.
/// </summary>
public static class FacileCliffBuilder
{
	public sealed record Config
	{
		public required PRNG Prng { get; init; }

		/// <summary>
		/// Elevation of the <see cref="Result.BaseCliff"/>
		/// </summary>
		public required int BaseElevation { get; init; }

		/// <summary>
		/// Number of outward steps the overhang will take, relative to the original Jaunt.
		/// </summary>
		public required int OverhangDepth { get; init; }

		/// <summary>
		/// Elevation of the (inverted) <see cref="Result.OverhangSampler"/>.
		/// </summary>
		public required int OverhangHeight { get; init; }

		public Config Validate()
		{
			return this with
			{
				BaseElevation = Math.Max(0, this.BaseElevation),
				OverhangDepth = Math.Clamp(this.OverhangDepth, 0, Math.Max(0, this.OverhangHeight - 1)),
				OverhangHeight = Math.Max(0, this.OverhangHeight),
			};
		}
	}

	public sealed class Result
	{
		public required I2DSampler<int> BaseCliff { get; init; }

		/// <summary>
		/// This sampler should be inverted to produce overhang.
		/// </summary>
		public required I2DSampler<int> OverhangSampler { get; init; }
	}

	public static Result BuildCliff(PositionedJaunt jaunt, Config config)
	{
		config = config.Validate();

		var baseCliff = GenerateBase(jaunt.Jaunt, config);
		var overhang = GenerateOverhang2(jaunt.Jaunt, config);

		return new Result
		{
			BaseCliff = baseCliff,
			OverhangSampler = overhang,
		};
	}

	private static Rect GetJauntBounds(Jaunt jaunt, int extraZ)
	{
		int xStart = jaunt.Runs[0].start;
		int xEnd = jaunt.Runs.Last().end;
		int zStart = jaunt.Runs.Select(r => r.laneOffset).Min();
		int zEnd = jaunt.Runs.Select(r => r.laneOffset).Max() + 1 + extraZ;
		return new Rect(new XZ(xStart, zStart), new XZ(xEnd, zEnd));
	}

	private static void BackfillJaunt(MutableArray2D<int> array, Jaunt jaunt, int value)
	{
		int zStart = array.Bounds.start.Z;
		foreach (var run in jaunt.Runs)
		{
			for (int x = run.start; x < run.end; x++)
			{
				for (int z = run.laneOffset; z >= zStart; z--)
				{
					array.Put(new XZ(x, z), value);
				}
			}
		}
	}

	private static I2DSampler<int> GenerateBase(Jaunt jaunt, Config config)
	{
		var bounds = GetJauntBounds(jaunt, 0);
		var array = new MutableArray2D<int>(bounds, -1);

		BackfillJaunt(array, jaunt, config.BaseElevation);

		// Could probably use Fencepost Shifting here... but let's keep it very simple for now:
		var gaps = FindGaps(jaunt).OrderBy(x => x.LaneOffset).ToList();
		foreach (var gap in gaps)
		{
			var xs = gap.Range.xValues.ToList();
			foreach (var x in xs)
			{
				var backstop = array.Sample(new XZ(x, gap.LaneOffset - 1));
				array.Put(new XZ(x, gap.LaneOffset), backstop - 1);
			}
		}

		return array;
	}

	sealed record Gap
	{
		public required Jaunt.Run? LeftBookend { get; init; }
		public required Jaunt.Run? RightBookend { get; init; }
		public required Range Range { get; init; }
		public required int LaneOffset { get; init; }
	}

	private static IEnumerable<Gap> FindGaps(Jaunt jaunt)
	{
		int xMin = jaunt.Runs[0].start;
		Stack<Jaunt.Run> stack = new();

		foreach (var run in jaunt.Runs)
		{
			// Any run deeper than the current run (there should be at most 1) is unpaired
			while (stack.TryPeek(out var unpaired) && unpaired.laneOffset < run.laneOffset)
			{
				stack.Pop();
			}

			Gap? gap;
			if (stack.TryPeek(out var start))
			{
				if (start.laneOffset == run.laneOffset)
				{
					// start+end pair
					gap = new Gap
					{
						LeftBookend = start,
						RightBookend = run,
						LaneOffset = run.laneOffset,
						Range = new Range(start.end, run.start - 1),
					};
					stack.Pop();
				}
				else
				{
					gap = null;
				}
			}
			else
			{
				// open to the left
				gap = new Gap
				{
					LeftBookend = null,
					RightBookend = run,
					LaneOffset = run.laneOffset,
					Range = new Range(xMin, run.start - 1),
				};
			}

			// no matter what, this run could still pair to the right also
			stack.Push(run);

			if (gap != null && gap.Range.Width > 0)
			{
				yield return gap;
			}
		}

		// Resolve runs which are open to the right
		int xMax = jaunt.Runs.Last().end - 1;
		foreach (var run in stack)
		{
			var range = new Range(run.end, xMax);
			if (range.Width > 0)
			{
				yield return new Gap
				{
					LeftBookend = run,
					RightBookend = null,
					LaneOffset = run.laneOffset,
					Range = range,
				};
			}
		}
	}

	/// <summary>
	/// It is assumed this sampler will be inverted.
	/// Uses fencepost shifting to generate, column by column, the outward steps
	/// needed to achieve <see cref="Config.OverhangDepth"/> in the vertical space
	/// allowed by <see cref="Config.OverhangHeight"/>.
	/// </summary>
	private static I2DSampler<int> GenerateOverhang2(Jaunt jaunt, Config config)
	{
		var bounds = GetJauntBounds(jaunt, config.OverhangDepth + 1);
		var array = new MutableArray2D<int>(bounds, -1);

		BackfillJaunt(array, jaunt, config.OverhangHeight);

		if (config.OverhangDepth < 1)
		{
			return array;
		}

		int average = config.OverhangHeight / config.OverhangDepth;
		var settings = new FencepostShifter.Settings
		{
			MaxFenceLength = average + 2,
			MinFenceLength = 1,
			MaxNudge = 1,
			TotalLength = config.OverhangHeight,
		};

		var posts = GenerateInitialOverhangFenceposts(config);

		foreach (var run in jaunt.Runs)
		{
			for (int x = run.start; x < run.end; x++)
			{
				var shifter = FencepostShifter.Create(posts, settings);
				posts = shifter.Shift(config.Prng);
				int z = run.laneOffset;
				foreach (var post in posts.Concat([config.OverhangHeight]))
				{
					int y = config.OverhangHeight - post;
					z++;
					array.Put(new XZ(x, z), y);
				}
			}
		}

		return array;
	}

	/// <summary>
	/// For the overhang, we use the <see cref="FencepostShifter"/> idea to decide, for each
	/// column, where each outward step should occur. We need <see cref="Config.OverhangDepth"/>
	/// such steps distributed over <see cref="Config.OverhangHeight"/>.
	/// For example, if depth=3 and height=10 our post list could be [4,6,9] meaning the first
	/// outward step would occur at Y+4, the second at Y+6, and the third at Y+9.
	/// </summary>
	private static List<int> GenerateInitialOverhangFenceposts(Config config)
	{
		// The following technique guarantees that the last post will always equal
		// OverhangHeight, which is not what we want here.
		// So we generate one extra post...
		var distribution = Util.Distribute(config.OverhangHeight, config.OverhangDepth + 1);
		config.Prng.Shuffle(distribution);
		var posts = distribution.Scan(0, (sum, a) => sum + a).ToList();
		posts.RemoveAt(posts.Count - 1); // ... and now remove the extra post.
		return posts;
	}
}
