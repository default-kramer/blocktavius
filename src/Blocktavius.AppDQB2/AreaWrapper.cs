using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2;

public sealed class AreaWrapper
{
	public readonly IArea Area;
	private readonly Lazy<IReadOnlyList<Shell>> shells;
	public IReadOnlyList<Shell> Shells => shells.Value;

	public AreaWrapper(IArea area)
	{
		this.Area = area;
		this.shells = new Lazy<IReadOnlyList<Shell>>(() => ShellLogic.ComputeShells(area));
	}

	public bool TryConvertToRegions(int minTileSize, out IReadOnlyList<Region> regions)
	{
		var tagger = Pixelate(minTileSize, Area);
		regions = tagger.GetRegions(true, XZ.Zero);
		return true;
	}

	private static TileTagger<bool> Pixelate(int tileSize, IArea area)
	{
		// We could be less wasteful by calculating the unscaledOffset and then applying
		// that translation when we call tagger.GetRegions... but not now.
		// Just always start the tagger at XZ.Zero for simplicity.

		int tileCountX = (area.Bounds.end.X + tileSize - 1) / tileSize;
		int tileCountZ = (area.Bounds.end.Z + tileSize - 1) / tileSize;
		var unscaledSize = new XZ(tileCountX, tileCountZ);
		var tagger = new TileTagger<bool>(unscaledSize, new XZ(tileSize, tileSize));

		foreach (var unscaledXZ in new Rect(XZ.Zero, unscaledSize).Enumerate())
		{
			var start = unscaledXZ.Scale(tileSize);
			var tile = new Rect(start, start.Add(tileSize, tileSize));

			bool hasData;
			if (tile.end.X < area.Bounds.start.X || tile.end.Z < area.Bounds.start.Z)
			{
				hasData = false;
			}
			else
			{
				hasData = tile.Enumerate().Any(area.InArea);
			}

			tagger.AddTag(unscaledXZ, hasData);
		}

		return tagger;
	}
}
