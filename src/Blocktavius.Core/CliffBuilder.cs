using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

sealed class CliffBuilder : AdditiveHillBuilder.ICliffBuilder
{
	interface ILayer
	{
		Jaunt Jaunt { get; }
		int OffsetZ { get; }
		Elevation MinElevation { get; }
		Elevation MaxElevation { get; }

		void Write(MutableArray2D<Elevation> array);
	}

	/// <summary>
	/// Sorted by elevation, lowest at the front of the queue.
	/// </summary>
	private readonly Stack<ILayer> layers = new();
	private readonly int totalLength;
	private readonly Elevation minElevation;
	private readonly Elevation maxElevation;
	private readonly FencepostShifter.Settings shifterSettings;
	private readonly JauntSettings jauntSettings;
	public required int steepness { get; init; } = 1; // must be >= 1

	// TODO these should all be configurable:
	const int minFenceLength = 1;
	const int maxFenceLength = 8;
	const int maxNudge = 4;
	const int maxLaneCount = 5;
	private readonly PRNG prng = PRNG.Create(new Random());

	public CliffBuilder(int totalLength, Elevation min, Elevation max)
	{
		this.totalLength = totalLength;
		this.minElevation = min;
		this.maxElevation = max;

		shifterSettings = new()
		{
			MinFenceLength = minFenceLength,
			MaxFenceLength = maxFenceLength,
			MaxNudge = maxNudge,
			TotalLength = totalLength,
		};
		jauntSettings = new()
		{
			LaneChangeDirectionProvider = RandomValues.InfiniteDeck(true, true, true, false, false, false),
			MaxLaneCount = maxLaneCount,
			TotalLength = totalLength,
			RunLengthProvider = RandomValues.FromRange(shifterSettings.MinFenceLength, shifterSettings.MaxFenceLength),
		};
	}

	private ILayer CreateInitialLayer()
	{
		var jaunt = Jaunt.Create(prng, jauntSettings);
		return new SimpleLayer()
		{
			Jaunt = jaunt,
			MaxElevation = maxElevation,
			MinElevation = new Elevation(maxElevation.Y - steepness + 1),
			OffsetZ = 0,
		};
	}

	private ILayer CreateLayer(ILayer prevLayer)
	{
		var shifted = prevLayer.Jaunt.ShiftByFencepost(prng, shifterSettings);
		return new SimpleLayer()
		{
			Jaunt = shifted,
			MaxElevation = new Elevation(prevLayer.MinElevation.Y - 1),
			MinElevation = new Elevation(prevLayer.MinElevation.Y - steepness),
			OffsetZ = prevLayer.OffsetZ + 1,
		};

		// Other options:

		// create another Jaunt, shift it, do "chip away" decrease elevatino by 1

		// create another Jaunt, shift it, apply 1d sampler, decrease elevatino by N
	}

	private ILayer CreateLayer()
	{
		if (layers.TryPeek(out var layer))
		{
			return CreateLayer(layer);
		}
		return CreateInitialLayer();
	}

	/// <summary>
	/// Creates a cliff that is tall in the north and short in the south.
	/// The returned sampler's bounds will be determined by the given <paramref name="range"/>
	/// for the X dimension, and the Z dimension will start at 0 and go as far south (positive)
	/// as necessary to reach the requested <paramref name="floor"/>.
	/// This allows callers to request a smaller slice of the full width.
	/// </summary>
	public I2DSampler<Elevation> Build(Elevation floor, Range range)
	{
		if (range.xMin < 0 || range.xMax > totalLength - 1)
		{
			throw new ArgumentOutOfRangeException(nameof(range));
		}

		var elevation = maxElevation;
		while (layers.Count == 0 || layers.Peek().MaxElevation.Y > floor.Y)
		{
			layers.Push(CreateLayer());
		}

		// Find the first relevant layer for the requested floor
		var firstLayer = layers.Index().First(l => l.Item.MinElevation.Y > floor.Y);
		int zMax = firstLayer.Item.OffsetZ + firstLayer.Item.Jaunt.NumRuns;

		var bounds = new Rect(new XZ(range.xMin, 0), new XZ(range.xMax + 1, zMax + 1));
		var sampler = new MutableArray2D<Elevation>(bounds, new Elevation(-1));

		// Writing the lower elevations before the higher ones means we don't have
		// to worry about overwriting the work of previous layers.
		foreach (var layer in layers.Skip(firstLayer.Index))
		{
			layer.Write(sampler);
		}

		return sampler;
	}

	int AdditiveHillBuilder.ICliffBuilder.Width => totalLength;

	I2DSampler<Elevation> AdditiveHillBuilder.ICliffBuilder.BuildCliff(Range slice)
	{
		return Build(minElevation, slice);
	}

	AdditiveHillBuilder.ICliffBuilder AdditiveHillBuilder.ICliffBuilder.AnotherOne(int width)
	{
		return new CliffBuilder(width, minElevation, maxElevation)
		{
			steepness = this.steepness,
		};
	}

	class SimpleLayer : ILayer
	{
		public required Jaunt Jaunt { get; init; }
		public required int OffsetZ { get; init; }
		public required Elevation MinElevation { get; init; }
		public required Elevation MaxElevation { get; init; }

		public void Write(MutableArray2D<Elevation> array)
		{
			var bounds = array.Bounds;
			var jauntAsList = Jaunt.ToCoords(new XZ(0, OffsetZ)).ToList();

			for (int x = bounds.start.X; x < bounds.end.X; x++)
			{
				for (int z = jauntAsList[x].Z; z >= bounds.start.Z; z--)
				{
					var xz = new XZ(x, z);
					array.Put(xz, MaxElevation);
				}
			}
		}
	}
}
