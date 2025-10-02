using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

// OKAY, can we make a generic hill builder that accepts:
// * a Region (or list of Edges)
// * a function `CliffBuilder CreateCliffBuilder(int width)`
//
// The AdditiveHillBuilder would
// * put cliffs on the outside of each edge
// * fabricate outside corners by extending both cliffs and take min or max (not sure yet)
// * handle inside corners naturally, by taking max
//
// The SubtractiveHillBuilder would be similar maybe... but I feel like that one is
// more complicated in ways I can't precisely describe yet.

sealed class CliffBuilder
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
	private readonly Queue<ILayer> layers = new();

	// TODO these should all be configurable:
	const int totalLength = 500;
	const int minFenceLength = 1;
	const int maxFenceLength = 8;
	const int maxNudge = 4;
	const int maxLaneCount = 5;
	private readonly Elevation startElevation = new Elevation(70);
	private readonly PRNG prng = PRNG.Create(new Random());

	private readonly FencepostShifter.Settings shifterSettings = new()
	{
		MinFenceLength = minFenceLength,
		MaxFenceLength = maxFenceLength,
		MaxNudge = maxNudge,
		TotalLength = totalLength,
	};

	private readonly JauntSettings jauntSettings = new()
	{
		LaneChangeDirectionProvider = RandomValues.InfiniteDeck(true, true, true, false, false, false),
		MaxLaneCount = maxLaneCount,
		TotalLength = totalLength,
		RunLengthProvider = RandomValues.FromRange(minFenceLength, maxFenceLength),
	};

	private ILayer CreateInitialLayer()
	{
		var jaunt = Jaunt.Create(prng, jauntSettings);
		return new SimpleLayer()
		{
			Jaunt = jaunt,
			MaxElevation = startElevation,
			MinElevation = new Elevation(startElevation.Y - 1),
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
			MinElevation = new Elevation(prevLayer.MinElevation.Y - 2),
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

		var elevation = startElevation;
		while (layers.Count == 0 || layers.Peek().MaxElevation.Y > floor.Y)
		{
			layers.Enqueue(CreateLayer());
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
					if (array.Sample(xz).Y > 0)
					{
						// TODO how to we make sure all layers don't have to reimplement this logic?
						// don't overwrite previous layers, go to next x
						z = bounds.start.Z - 1;
						continue;
					}

					array.Put(xz, MaxElevation);
				}
			}
		}
	}
}
