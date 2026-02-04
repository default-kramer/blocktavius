using Blocktavius.Core.Generators.Hills;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

public static class WIP
{
	public static I2DSampler<HillItem> Blah(PRNG prng)
	{
		const int maxElevation = 30;

		var plateauItem = new HillItem { Elevation = maxElevation, Kind = HillItemKind.Plateau };

		var area = BuildPlateau(prng, plateauItem);
		AddChisel(area, maxElevation);

		var settings = new CornerPusherHill.Settings()
		{
			Prng = prng.Clone(),
			MinElevation = 1,
			MaxElevation = maxElevation - 1,
		};
		CornerPusherHill.BuildHill(settings, area, y => new HillItem { Elevation = y, Kind = HillItemKind.Cliff });

		return area.GetSampler(ExpansionId.MaxValue, plateauItem)
			.Project(t => t.Item1 ? t.Item2 : HillItem.Nothing);
	}

	public enum HillItemKind
	{
		None,
		Plateau,
		Chisel,
		Cliff,
	}

	public readonly record struct HillItem
	{
		public int Elevation { get; init; }
		public HillItemKind Kind { get; init; }

		public static readonly HillItem Nothing = new() { Elevation = -1, Kind = HillItemKind.None };
	}

	private static ExpandableArea<HillItem> BuildPlateau(PRNG prng, HillItem fillValue)
	{
		//var initArea = new Rect(XZ.Zero, XZ.Zero.Add(1, 1)).AsArea();
		var initArea = new Rect(XZ.Zero, XZ.Zero.Add(20, 50)).AsArea();
		var initShell = ShellLogic.ComputeShells(initArea).Single();

		var area = new ExpandableArea<HillItem>(initShell);
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
				// NOMERGE - this should be CurrentBounds or something:
				size = area.GetSampler(ExpansionId.MaxValue, fillValue).Bounds.Size;
			}
		}

		return area;
	}

	private static void AddChisel(ExpandableArea<HillItem> area, int elevation)
	{
		var shell = area.CurrentShell();
		var value = new HillItem { Elevation = elevation, Kind = HillItemKind.Chisel };
		area.Expand(shell.Select(item => (item.XZ, value)));
	}
}
