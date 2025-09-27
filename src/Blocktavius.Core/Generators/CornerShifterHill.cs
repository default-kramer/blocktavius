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
				layers.Add(Layer.Create(prng, settings, elev));
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

	sealed class Layer
	{
		public required CornerShifter.Settings settings { get; init; }
		public required CornerShifter.Contour contour { get; init; }
		public required int zStart { get; init; }
		public required Elevation Elevation { get; init; }

		private Layer() { }

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

		public static Layer Create(PRNG prng, CornerShifter.Settings settings, Elevation elevation)
		{
			var contour = CornerShifter.Contour.Generate(prng, settings);
			int zMin = Walk(contour, 0).Select(xz => xz.Z).Min();
			return new Layer
			{
				settings = settings,
				contour = contour,
				zStart = -zMin,
				Elevation = elevation,
			};
		}

		public void WriteLayer(MutableArray2D<Elevation> sampler, int[] prevZ)
		{
			var elev = this.Elevation;
			foreach (var xz in Walk(contour, zStart))
			{
				FillDown(xz, sampler, prevZ);
				sampler.Put(xz, elev);
				prevZ[xz.X] = xz.Z;
			}
		}

		private static void FillDown(XZ xz, MutableArray2D<Elevation> sampler, int[] prevZ)
		{
			int z = prevZ[xz.X];
			var fillValue = sampler.Sample(new XZ(xz.X, z));
			while (z < xz.Z)
			{
				sampler.Put(new XZ(xz.X, z), fillValue);
				z++;
			}
		}

		public Layer CreateNextLayer(PRNG prng, Elevation elevation)
		{
			return new Layer
			{
				settings = this.settings,
				contour = this.contour.Shift(prng, settings),
				Elevation = elevation,
				zStart = this.zStart + 1,
			};
		}
	}
}
