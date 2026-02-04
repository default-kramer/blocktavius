using Blocktavius.Core.Generators.Hills;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

public static class WIP
{
	public static I2DSampler<int> Blah(PRNG prng)
	{
		const int maxElevation = 30;

		var area = InitialShell(prng, maxElevation);
		CornerPusherHill.BuildHill(new CornerPusherHill.Settings()
		{
			Prng = prng.Clone(),
			MinElevation = 1,
			MaxElevation = maxElevation,
		}, area);

		return area.GetSampler(ExpansionId.MaxValue, maxElevation).Project(t => t.Item1 ? t.Item2 : -1);
	}

	private static ExpandableArea<int> InitialShell(PRNG prng, int fillValue)
	{
		//var initArea = new Rect(XZ.Zero, XZ.Zero.Add(1, 1)).AsArea();
		var initArea = new Rect(XZ.Zero, XZ.Zero.Add(20, 50)).AsArea();
		var initShell = ShellLogic.ComputeShells(initArea).Single();

		var area = new ExpandableArea<int>(initShell);
		var shell = area.CurrentShell();
		//while (shell.Count < 600)

		var size = initArea.Bounds.start;
		while (size.X + size.Z + Math.Min(size.X, size.Z) < 200)
		{
			int i = prng.NextInt32(shell.Count);
			var item = shell[i];
			if (item.CornerType != CornerType.Outside) // outside corners would cause "not connected" exceptions
			{
				area.Expand([(shell[i].XZ, fillValue)]);
				shell = area.CurrentShell();
				size = area.GetSampler(ExpansionId.MaxValue, fillValue).Bounds.Size;
			}
		}

		return area;
	}
}
