using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

public sealed class Interpolator2D : I2DSampler<double>
{
	private readonly XZ size;
	private readonly int scale;
	private readonly I2DSampler<double> values;

	private Interpolator2D(XZ size, int scale, I2DSampler<double> values)
	{
		this.size = size;
		this.scale = scale;
		this.values = values;
	}

	public static Interpolator2D Create(XZ size, int scale, PRNG prng, double defaultValue)
	{
		// Adding +2 here might be "wasteful" (only when X and/or Z is divisible
		// by the scale) but it's simple.
		var smallSize = new XZ(size.X / scale + 2, size.Z / scale + 2);
		var smallBounds = new Rect(XZ.Zero, smallSize);
		var values = new MutableArray2D<double>(smallBounds, defaultValue);
		foreach (var xz in smallBounds.Enumerate())
		{
			values.Put(xz, prng.NextDouble());
		}
		return new Interpolator2D(size, scale, values);
	}

	public Rect Bounds => new Rect(XZ.Zero, size);

	public double Sample(XZ xz)
	{
		int smallX = xz.X / scale;
		int smallZ = xz.Z / scale;

		double valNW = values.Sample(new XZ(smallX, smallZ));
		double valNE = values.Sample(new XZ(smallX + 1, smallZ));
		double valSW = values.Sample(new XZ(smallX, smallZ + 1));
		double valSE = values.Sample(new XZ(smallX + 1, smallZ + 1));

		double weightX = (double)(xz.X % scale) / scale;
		double weightZ = (double)(xz.Z % scale) / scale;

		double interpTop = valNW * (1.0 - weightX) + valNE * weightX;
		double interpBottom = valSW * (1.0 - weightX) + valSE * weightX;

		return interpTop * (1.0 - weightZ) + interpBottom * weightZ;
	}
}
