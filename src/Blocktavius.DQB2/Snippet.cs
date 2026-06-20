using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

public interface IBlockdataColumn
{
	int YStart { get; }
	int YEnd { get; }

	ushort GetBlock(int y);
}

public sealed class Snippet : I2DSampler<IBlockdataColumn>
{
	private readonly int sizeX;
	private readonly int sizeZ;
	private readonly int sizeY;
	private readonly IReadOnlyList<ushort> blockdata;
	private readonly int sizePerLayer;

	public XZ SizeXZ => new XZ(sizeX, sizeZ);
	public int SizeY => sizeY;
	public Rect Bounds => new Rect(XZ.Zero, SizeXZ);

	private Snippet(IReadOnlyList<ushort> blockdata, XZ sizeXZ, int sizeY)
	{
		this.sizeX = sizeXZ.X;
		this.sizeZ = sizeXZ.Z;
		this.sizeY = sizeY;
		this.blockdata = blockdata;

		if (sizeX < 0 || sizeZ < 0 || sizeY < 0)
		{
			throw new ArgumentException("negative dimensions are not allowed");
		}
		int totalSize = sizeX * sizeZ * sizeY;
		if (blockdata.Count < totalSize)
		{
			throw new ArgumentException($"blockdata too short, needed {totalSize} but got {blockdata.Count}");
		}

		sizePerLayer = sizeX * sizeZ;
	}

	public sealed class Builder
	{
		private readonly XZ size;
		private readonly int sizeY;
		private readonly ushort[] blockdata;
		private bool built = false;
		public Builder(XZ size, int sizeY)
		{
			this.size = size;
			this.sizeY = sizeY;
			this.blockdata = new ushort[size.X * size.Z * sizeY];
		}

		private int GetIndex(Point point) => 0
			+ point.xz.X
			+ point.xz.Z * size.X
			+ point.Y * size.X * size.Z;

		public ushort this[Point point]
		{
			get => blockdata[GetIndex(point)];
			set
			{
				if (built)
				{
					throw new InvalidOperationException("already built, no further modifications are allowed");
				}
				blockdata[GetIndex(point)] = value;
			}
		}

		public Snippet BuildSnippet()
		{
			return new Snippet(blockdata, size, sizeY);
		}
	}

	public static Snippet Create(IStage stage, Rect bounds, int floorY = 0)
	{
		if (floorY < 0 || floorY >= DQB2Constants.MaxElevation)
		{
			throw new ArgumentOutOfRangeException($"invalid {nameof(floorY)}: {floorY}");
		}

		int maxSize = bounds.Size.X * bounds.Size.Z * (DQB2Constants.MaxElevation - floorY);
		var blockdata = new ushort[maxSize];
		int numBlocks = 0;

		int y = floorY;
		while (y < DQB2Constants.MaxElevation)
		{
			bool empty = true;
			int numBlocksRewind = numBlocks;

			foreach (var xz in bounds.Enumerate())
			{
				var offset = ChunkOffset.FromXZ(xz);
				ushort block = 0;
				if (stage.TryReadChunk(offset, out var chunk))
				{
					block = chunk.GetBlock(new Point(xz, y));
				}
				blockdata[numBlocks++] = block;
				empty = empty && block == 0;
			}

			if (empty)
			{
				numBlocks = numBlocksRewind;
				break;
			}
			y++;
		}

		int actualSizeY = y - floorY;
		Array.Resize(ref blockdata, numBlocks);
		return new Snippet(blockdata, bounds.Size, actualSizeY);
	}

	IBlockdataColumn I2DSampler<IBlockdataColumn>.Sample(XZ xz)
	{
		if (Bounds.Contains(xz))
		{
			int index = xz.Z * SizeXZ.X + xz.X;
			return new Column(this, index);
		}
		throw new ArgumentOutOfRangeException(nameof(xz));
	}

	sealed class Column : IBlockdataColumn
	{
		private readonly Snippet snippet;
		private readonly int indexStart;

		public Column(Snippet snippet, int indexStart)
		{
			this.snippet = snippet;
			this.indexStart = indexStart;
		}

		public int YStart => 0;
		public int YEnd => snippet.sizeY;

		public ushort GetBlock(int y)
		{
			if (y >= YStart && y < snippet.sizeY)
			{
				int index = indexStart + y * snippet.sizePerLayer;
				return snippet.blockdata[index];
			}
			throw new ArgumentOutOfRangeException(nameof(y));
		}
	}
}

public sealed class NEWCLIFF
{
	/// <summary>
	/// A "Slab" is like a wall with some spikes in front of it.
	/// By convention, we think of this wall running from West to East.
	/// First we generate a spike at each X coordinate (from West to East).
	/// Then we choose the height of the wall by adding a randomly chosen
	/// Cap Choice to the max spike that we generated.
	/// So if we generated spikes [3,5,3,4,4,5] and cap 2 the result would look like this:
	///
	/// ------ (this line is just above the top of the slab/wall)
	///        (7)
	///        (6)
	///  x   x (5)
	///  x xxx (4)
	/// xxxxxx (3)
	/// xxxxxx (2)
	/// xxxxxx (1)
	/// ------ (this line is just below the bottom of the slab/wall)
	///
	/// This means that a slab is always flat on the top and bottom;
	/// in other words it has a uniform height along its entire width.
	/// It is only the spikes that are not uniform.
	/// This allows us to easily stack slabs on top of each other.
	///
	/// If this slab <see cref="IsOverhang"/>, we would invert it.
	/// </summary>
	public sealed record SlabShape : IRandomValues<SlabShape>
	{
		public required IRandomValues<int> SpikeChoices { get; init; }
		public required IRandomValues<int> CapChoices { get; init; }
		public required bool IsOverhang { get; init; }

		SlabShape IRandomValues<SlabShape>.NextValue(PRNG prng) => this;
	}

	public sealed record SlabSpec
	{
		public required int? RepeatUntilElevation { get; init; }
		public required IRandomValues<SlabShape> ShapeChoices { get; init; }
	}

	public sealed record CliffSpec
	{
		public required IReadOnlyList<SlabSpec> SlabSpecs { get; init; }
		public required IRandomValues<int> StartFromElevationChoices { get; init; }
	}

	public sealed record Settings2
	{
		public required CliffSpec CliffSpec { get; init; }
		public required PRNG Prng { get; init; }
		public required ushort Block { get; init; }
		public required Jaunt Jaunt { get; init; }
	}

	public static CliffSpec StandardSpec(int startElevation, int targetElevation, int? overhangStartElevation)
	{
		List<SlabSpec> slabs = new();

		slabs.Add(new SlabSpec
		{
			ShapeChoices = new SlabShape
			{
				IsOverhang = false,
				SpikeChoices = RandomValues.FromRange(2, 5),
				CapChoices = RandomValues.FromRange(1, 2),
			},
			RepeatUntilElevation = overhangStartElevation ?? targetElevation,
		});

		if (overhangStartElevation.HasValue)
		{
			slabs.Add(new SlabSpec
			{
				ShapeChoices = new SlabShape
				{
					IsOverhang = true,
					SpikeChoices = RandomValues.FromRange(2, 5),
					CapChoices = RandomValues.FromRange(1, 2),
				},
				RepeatUntilElevation = targetElevation,
			});
		}

		return new CliffSpec
		{
			SlabSpecs = slabs,
			StartFromElevationChoices = RandomValues.Constant(startElevation)
		};
	}

	sealed record Slab
	{
		public required SlabShape SlabShape { get; init; }
		public required int LaneOffset { get; init; }
		public required int SlabMinElevation { get; init; }
		public required int SlabMaxElevation { get; init; }
		public required int SpikeMinElevation { get; init; }
		public required int SpikeMaxElevation { get; init; }
		public required IReadOnlyList<int> SpikeElevations { get; init; }

		private Slab() { }

		public Slab RemapLaneOffset(int delta) => this with { LaneOffset = this.LaneOffset + delta };

		public static Slab Create(int startElevation, int width, SlabSpec spec, PRNG prng, int laneOffset)
		{
			var shape = spec.ShapeChoices.NextValue(prng);

			var spikes = new List<int>(capacity: width);
			for (int i = 0; i < width; i++)
			{
				spikes.Add(startElevation + shape.SpikeChoices.NextValue(prng));
			}

			int minSpike = spikes.Min();
			int maxSpike = spikes.Max();

			int maxSlab = maxSpike + shape.CapChoices.NextValue(prng);

			return new Slab()
			{
				SlabShape = shape,
				SlabMinElevation = startElevation,
				SlabMaxElevation = maxSlab,
				SpikeMinElevation = minSpike,
				SpikeMaxElevation = maxSpike,
				SpikeElevations = spikes,
				LaneOffset = laneOffset,
			};
		}
	}

	sealed record Run
	{
		public required Jaunt.Run JauntRun { get; init; }
		public required IReadOnlyList<Slab> Slabs { get; init; }
	}

	public static Snippet Build(Settings2 settings)
	{
		var runs = settings.Jaunt.Runs.Select(r => BuildSlabs(settings, r)).ToList();

		const int SPIKE_OFFSET = 1;

		var offsets = runs.SelectMany(r => r.Slabs).Select(s => s.LaneOffset);
		int minZ = offsets.Min();
		int maxZ = offsets.Max() + SPIKE_OFFSET;

		int zAdjust = -minZ; // adjust so that minZ is remapped to 0

		var snippetSize = new XZ(settings.Jaunt.TotalLength, maxZ - minZ);

		int targetY;
		var foo = settings.CliffSpec.SlabSpecs.Last().RepeatUntilElevation;
		if (foo.HasValue)
		{
			targetY = foo.Value;
		}
		else
		{
			targetY = runs.Max(r => r.Slabs.Last().SlabMaxElevation);
		}

		var builder = new Snippet.Builder(snippetSize.Add(1, 1), targetY + 1);
		foreach (var run in runs)
		{
			foreach (var slab in run.Slabs.Reverse())
			{
				int z = slab.LaneOffset + zAdjust;

				int backstopStartY = slab.SlabShape.IsOverhang ? slab.SlabMinElevation : 1;
				int backstopEndY = Math.Min(slab.SlabMaxElevation, targetY) + 1;

				for (int i = 0; i < run.JauntRun.length; i++)
				{
					int x = run.JauntRun.start + i;

					for (int y = backstopStartY; y < backstopEndY; y++)
					{
						builder[new Point(new XZ(x, z), y)] = 9;// settings.Block;
					}

					int spikeStartY;
					int spikeEndY;
					if (slab.SlabShape.IsOverhang)
					{
						int spikeHeight = slab.SpikeElevations[i] - slab.SlabMinElevation;
						spikeStartY = slab.SlabMaxElevation - spikeHeight;
						spikeEndY = Math.Min(slab.SlabMaxElevation, targetY) + 1;
					}
					else
					{
						spikeStartY = 1;
						spikeEndY = Math.Min(slab.SpikeElevations[i], targetY) + 1;
					}

					for (int y = spikeStartY; y < spikeEndY; y++)
					{
						builder[new Point(new XZ(x, z + SPIKE_OFFSET), y)] = settings.Block;
					}
				}
			}
		}

		return builder.BuildSnippet();
	}

	private static Run BuildSlabs(Settings2 settings, Jaunt.Run run)
	{
		var slabs = new List<Slab>();

		int startElevation = settings.CliffSpec.StartFromElevationChoices.NextValue(settings.Prng);
		int laneOffset = run.laneOffset;

		var slabSpecIterator = settings.CliffSpec.SlabSpecs.GetEnumerator();

		bool keepGoing = slabSpecIterator.MoveNext();
		while (keepGoing)
		{
			var prev = slabs.LastOrDefault();

			var slab = Slab.Create(startElevation, run.length, slabSpecIterator.Current, settings.Prng, laneOffset);
			startElevation = slab.SlabMaxElevation + 1;

			// UGLY!! Patch up lane offset after the fact :(
			if (prev != null && prev.SlabShape.IsOverhang != slab.SlabShape.IsOverhang)
			{
				// Don't change lane offset!
				slab = slab with { LaneOffset = prev.LaneOffset };
				laneOffset = prev.LaneOffset;
			}
			laneOffset += slab.SlabShape.IsOverhang ? 1 : -1;

			slabs.Add(slab);

			var targetElevation = slabSpecIterator.Current.RepeatUntilElevation;
			if (targetElevation.HasValue)
			{
				if (slab.SlabMaxElevation >= targetElevation.Value)
				{
					keepGoing = slabSpecIterator.MoveNext(); // target elevation has now been reached, advance
				}
				else
				{
					keepGoing = true; // repeat current spec until target elevation reached
				}
			}
			else
			{
				keepGoing = slabSpecIterator.MoveNext(); // no repeat condition specified, advance
			}
		}

		// This is weird. We want the *top* (last) slab to line up with the lane offset from the Jaunt,
		// so now that we're done we have to remap it.
		var aha = slabs.LastOrDefault();
		if (aha != null)
		{
			int adjust = aha.LaneOffset - run.laneOffset;
			slabs = slabs.Select(s => s.RemapLaneOffset(-adjust)).ToList();
		}

		return new Run
		{
			JauntRun = run,
			Slabs = slabs,
		};
	}

	public sealed record Settings
	{
		public required PRNG Prng { get; init; }
		public required int FloorY { get; init; }
		public required int TargetY { get; init; }

		public required int C { get; init; }
		public required int R { get; init; }

		public required int LayerC { get; init; }
		public required int LayerR { get; init; }

		public required ushort Block { get; init; }
	}

	public static Snippet Build(Jaunt jaunt, Settings settings)
	{
		List<List<Layer>> segments = new();
		foreach (var run in jaunt.Runs)
		{
			segments.Add(BuildLayers(run, settings));
		}

		int depth = segments.Max(x =>
		{
			var last = x.Last();
			return last.run.laneOffset + last.layerNumber;
		});

		var snippetSize = new XZ(jaunt.TotalLength, depth);
		var builder = new Snippet.Builder(snippetSize.Add(1, 1), settings.TargetY + 1);
		foreach (var segment in segments)
		{
			int z = segment[0].run.laneOffset + segment.Count;

			foreach (var layer in segment)
			{
				for (int i = 0; i < layer.elevations.Count; i++)
				{
					var xz = new XZ(layer.run.start + i, z);
					int elevation = Math.Min(layer.elevations[i], settings.TargetY);
					for (int y = 0; y <= elevation; y++)
					{
						builder[new Point(xz, y)] = settings.Block;
					}
				}

				z--;
			}
		}

		return builder.BuildSnippet();
	}

	readonly record struct Layer(IReadOnlyList<int> elevations, Jaunt.Run run, int layerNumber);

	private static List<Layer> BuildLayers(Jaunt.Run run, Settings settings)
	{
		var layers = new List<Layer>();

		var current = BuildFirstLayer(run, settings);
		layers.Add(current);

		while (current.elevations.Min() < settings.TargetY)
		{
			current = BuildNextLayer(current, settings);
			layers.Add(current);
		}

		return layers;
	}

	private static Layer BuildFirstLayer(Jaunt.Run run, Settings settings)
	{
		var elevations = RandomElevations(run, settings.FloorY + settings.C, settings.R, settings.Prng);
		return new Layer
		{
			elevations = elevations,
			layerNumber = 0,
			run = run,
		};
	}

	private static Layer BuildNextLayer(Layer previous, Settings settings)
	{
		int floorY = previous.elevations.Max() + settings.LayerC + settings.Prng.NextInt32(settings.LayerR);
		var elevations = RandomElevations(previous.run, floorY, settings.R, settings.Prng);
		return new Layer
		{
			elevations = elevations,
			layerNumber = previous.layerNumber + 1,
			run = previous.run,
		};
	}

	private static IReadOnlyList<int> RandomElevations(Jaunt.Run run, int minY, int randY, PRNG prng)
	{
		return Enumerable.Range(0, run.end - run.start).Select(_ => minY + prng.NextInt32(randY)).ToList();
	}
}
