using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core.Generators.Hills;

public static class WinsomeHill
{
	public static I2DSampler<Elevation> BuildWinsomeHills(Region region, PRNG prng, int maxElevation, int steepness)
	{
		var cliffBuilder = new CliffBuilder(1, new Elevation(maxElevation - 30), new Elevation(maxElevation))
		{
			steepness = steepness,
		};
		return AdditiveHillBuilder.BuildHill(region, new Elevation(maxElevation), cliffBuilder);
	}
}
