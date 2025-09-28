using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

readonly record struct Range(int xMin, int xMax) // inclusive
{
	public bool IsInfeasible => xMax < xMin;

	public int Width => (xMax + 1) - xMin;

	public static Range NoConstraints => new Range(int.MinValue, int.MaxValue);

	public Range Intersect(int xMin, int xMax)
	{
		return new Range(Math.Max(xMin, this.xMin), Math.Min(xMax, this.xMax));
	}

	public Range ConstrainLeft(int left) => new Range(Math.Max(xMin, left), xMax);
	public Range ConstrainRight(int right) => new Range(xMin, Math.Min(xMax, right));

	public int RandomX(PRNG prng) => prng.NextInt32(xMin, xMax + 1);
}
