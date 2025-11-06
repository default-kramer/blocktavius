using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Blocktavius.Core;
using Blocktavius.DQB2;

namespace Blocktavius.AppDQB2;

public sealed record MinimapRenderOptions
{
	/// <summary>
	/// If true, all tiles are drawn. If false, hidden tiles are obscured.
	/// </summary>
	public bool ShowAllTiles { get; set; } = false;
}

/// <remarks>
/// Based on https://github.com/Sapphire645/DQB2MinimapExporter
/// </remarks>
public static class MinimapRenderer
{
	private const int TileSize = 16;
	private const int HiddenTileIndex = 992;
	private const int OverlayStartIndex = 993;

	// Port of the MountainDic from the Python script
	private static readonly Dictionary<int, int> MountainOverlayMap = new()
	{
		{ 8, 8 }, { 9, 10 }, { 10, 9 }, { 11, 7 }, { 18, 8 }
	};

	private const string sheetRetroPath = @"C:\Users\kramer\Documents\code\DQB2MinimapExporter\Script\Data\SheetRetro.png";
	private static readonly List<BitmapSource> tiles;

	static MinimapRenderer()
	{
		var tilesetImage = new BitmapImage(new Uri(sheetRetroPath, UriKind.RelativeOrAbsolute));
		tiles = ExtractTiles(tilesetImage, TileSize, TileSize);
	}

	public static BitmapSource Render(I2DSampler<MinimapTile> map, MinimapRenderOptions options)
	{
		// Create a drawing visual to render onto
		int imageWidth = map.Bounds.Size.X * TileSize;
		int imageHeight = map.Bounds.Size.Z * TileSize;
		var drawingVisual = new DrawingVisual();

		using (var dc = drawingVisual.RenderOpen())
		{
			// Iterate through every tile in the map and draw it
			for (int z = 0; z < map.Bounds.Size.Z; z++)
			{
				for (int x = 0; x < map.Bounds.Size.X; x++)
				{
					var tile = map.Sample(new XZ(x, z));
					var positionRect = new System.Windows.Rect(x * TileSize, z * TileSize, TileSize, TileSize);

					if (tile.TileValue == -1)
					{
						continue; // Skip empty tiles
					}

					// Handle visibility: If we are not showing all tiles and the tile is not visible, draw the hidden tile.
					if (!options.ShowAllTiles && !tile.IsVisible)
					{
						dc.DrawImage(tiles[HiddenTileIndex], positionRect);
						continue;
					}

					// --- Draw Base Tile ---
					if (tile.TileId >= 0 && tile.TileId < tiles.Count)
					{
						dc.DrawImage(tiles[tile.TileId], positionRect);
					}
					else
					{
						// Draw the error/hidden tile if the ID is out of bounds
						dc.DrawImage(tiles[HiddenTileIndex], positionRect);
					}

					// --- Draw Overlays ---
					// Draw TileType overlay (trees, rooms, etc.)
					if (tile.TileType > 0)
					{
						int overlayIndex = OverlayStartIndex + tile.TileType;
						if (overlayIndex < tiles.Count)
						{
							dc.DrawImage(tiles[overlayIndex], positionRect);
						}
					}

					// Draw Mountain overlay
					if (tile.IsMountain && tile.TileType == 0 && MountainOverlayMap.TryGetValue(tile.TileId, out int mountainOffset))
					{
						int mountainOverlayIndex = OverlayStartIndex + mountainOffset;
						if (mountainOverlayIndex < tiles.Count)
						{
							dc.DrawImage(tiles[mountainOverlayIndex], positionRect);
						}
					}
				}
			}
		}

		// Render the visual to a bitmap
		var finalBitmap = new RenderTargetBitmap(imageWidth, imageHeight, 96, 96, PixelFormats.Pbgra32);
		finalBitmap.Render(drawingVisual);
		finalBitmap.Freeze();

		return finalBitmap;
	}

	/// <summary>
	/// Extracts tiles from a tileset image into a list of Bitmaps.
	/// </summary>
	private static List<BitmapSource> ExtractTiles(BitmapSource tileset, int tileWidth, int tileHeight)
	{
		var extractedTiles = new List<BitmapSource>();
		for (int y = 0; y < tileset.PixelHeight; y += tileHeight)
		{
			for (int x = 0; x < tileset.PixelWidth; x += tileWidth)
			{
				var tileRect = new Int32Rect(x, y, tileWidth, tileHeight);
				var croppedBitmap = new CroppedBitmap(tileset, tileRect);
				croppedBitmap.Freeze();
				extractedTiles.Add(croppedBitmap);
			}
		}
		return extractedTiles;
	}
}