using Blocktavius.Core.Generators.BasicHill;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core.Generators;

public sealed class CornerShifterHill
{
	public static I2DSampler<Elevation> BuildNewHill(Rect bounds, PRNG prng, Elevation floor, Elevation peak)
	{
		var sampler = BuildNewHill(bounds.Size, prng, floor, peak);
		return sampler.TranslateTo(bounds.start);
	}

	private static I2DSampler<Elevation> BuildNewHill(XZ size, PRNG prng, Elevation floor, Elevation peak)
	{
		var settings = new CornerShifter.Settings()
		{
			MaxShift = 999,
			MaxMatchingDirections = 99,
			MaxDepth = 5,
			MaxRunLength = 7,
			MinRunLength = 2,
			Width = size.X,
			CanRelaxMaxRunLength = false,
			CanRelaxMinRunLength = false,
		};
		var jauntSettings = new JauntSettings()
		{
			MaxLaneCount = settings.MaxDepth,
			TotalLength = settings.Width,
			RunLengthProvider = RandomValues.BoundedInfiniteDeck(2, 3, 4, 5, 6, 7),
			LaneChangeDirectionProvider = RandomValues.InfiniteDeck(true, true, false, false),
		};
		var fencepostShiftSettings = new FencepostShifter.Settings
		{
			MaxFenceLength = settings.MaxRunLength,
			MinFenceLength = settings.MinRunLength,
			TotalLength = settings.Width,
			MaxNudge = settings.MaxShift,
		};

		var bounds = new Rect(XZ.Zero, size);
		var sampler = new MutableArray2D<Elevation>(bounds, peak);

		var layers = new List<Layer>();
		Elevation elev = new Elevation(floor.Y + 1);
		while (elev.Y <= peak.Y)
		{
			if (layers.Count > 0)
			{
				var prev = layers.Last();
				var next = prev.CreateNextLayer(prng, elev);
				layers.Add(next);
			}
			else
			{
				//layers.Add(Layer2.Create(prng, settings, elev));

				var jaunt = Jaunt.Create(prng, jauntSettings);
				layers.Add(JauntyLayer.Create(jaunt, elev, 0, fencepostShiftSettings));
			}
			elev = new Elevation(elev.Y + 1);
		}

		// Place floor at the Z=0 so that FillDown will work normally for the first layer
		for (int x = 0; x < size.X; x++)
		{
			sampler.Put(new XZ(x, 0), floor);
		}
		int[] prevZ = new int[size.X];
		prevZ.AsSpan().Fill(0);

		foreach (var layer in layers)
		{
			layer.WriteLayer(sampler, prevZ);
		}

		return sampler;
	}

	abstract class Layer
	{
		public abstract void WriteLayer(MutableArray2D<Elevation> sampler, int[] prevZ);

		// TODO composability will be much improved if layers don't have to know how to create the next layer...
		public abstract Layer CreateNextLayer(PRNG prng, Elevation elevation);

		protected static void FillDown(XZ xz, MutableArray2D<Elevation> sampler, int[] prevZ)
		{
			int z = prevZ[xz.X];
			var fillValue = sampler.Sample(new XZ(xz.X, z));
			while (z < xz.Z)
			{
				sampler.Put(new XZ(xz.X, z), fillValue);
				z++;
			}
		}
	}

	abstract class ListLayer : Layer
	{
		public required IReadOnlyList<XZ> coords { get; init; }
		protected ListLayer() { }

		protected abstract Elevation GetElevation(XZ xz);

		public override void WriteLayer(MutableArray2D<Elevation> sampler, int[] prevZ)
		{
			foreach (var xz in coords)
			{
				FillDown(xz, sampler, prevZ);
				sampler.Put(xz, GetElevation(xz));
				prevZ[xz.X] = xz.Z;
			}
		}
	}

	sealed class JauntyLayer : ListLayer
	{
		public required Elevation elevation { get; init; }
		public required Jaunt jaunt { get; init; }
		public required int zOffset { get; init; }
		public required FencepostShifter.Settings shiftSettings { get; init; }
		private JauntyLayer() { }

		public static JauntyLayer Create(Jaunt jaunt, Elevation elevation, int zOffset, FencepostShifter.Settings shiftSettings)
		{
			return new JauntyLayer()
			{
				coords = jaunt.ToCoords(new XZ(0, zOffset)).ToList(),
				elevation = elevation,
				jaunt = jaunt,
				zOffset = zOffset,
				shiftSettings = shiftSettings,
			};
		}

		protected override Elevation GetElevation(XZ xz) => elevation;

		public override Layer CreateNextLayer(PRNG prng, Elevation elevation)
		{
			//var jaunt = this.jaunt.NextLayer(prng);
			var jaunt = this.jaunt.ShiftByFencepost(prng, shiftSettings);
			return Create(jaunt, elevation, this.zOffset + 1, shiftSettings);
		}
	}

	sealed class Layer2 : Layer
	{
		public required CornerShifter.Settings settings { get; init; }
		public required CornerShifter.Contour contour { get; init; }
		public required int zStart { get; init; }
		public required Elevation Elevation { get; init; }

		private Layer2() { }

		private static IEnumerable<XZ> Walk(CornerShifter.Contour contour, int z)
		{
			int x = 0;
			foreach (var corner in contour.Corners)
			{
				while (x <= corner.X)
				{
					yield return new XZ(x, z);
					x++;
				}
				z += corner.Dir.Step.Z;
			}
			while (x < contour.Width)
			{
				yield return new XZ(x, z);
				x++;
			}
		}

		public static Layer2 Create(PRNG prng, CornerShifter.Settings settings, Elevation elevation)
		{
			var contour = CornerShifter.Contour.Generate(prng, settings);
			int zMin = Walk(contour, 0).Select(xz => xz.Z).Min();
			return new Layer2
			{
				settings = settings,
				contour = contour,
				zStart = -zMin,
				Elevation = elevation,
			};
		}

		public override void WriteLayer(MutableArray2D<Elevation> sampler, int[] prevZ)
		{
			var elev = this.Elevation;
			foreach (var xz in Walk(contour, zStart))
			{
				FillDown(xz, sampler, prevZ);
				sampler.Put(xz, elev);
				prevZ[xz.X] = xz.Z;
			}
		}

		public override Layer CreateNextLayer(PRNG prng, Elevation elevation)
		{
			return new Layer2
			{
				settings = this.settings,
				contour = this.contour.Shift(prng, settings),
				Elevation = elevation,
				zStart = this.zStart + 1,
			};
		}
	}
}
