using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace Blocktavius.AppDQB2;

interface ILayerVM : IAreaVM
{
	string LayerName { get; }
	bool IsVisible { get; set; }

	IEnumerable<ExternalImageVM> ExternalImage { get; }
}

class LayerVM : ViewModelBase, ILayerVM
{
	private TileGridPainterVM _painter;
	private int tileSize;

	public bool IsArea(XZ imageTranslation, out AreaWrapper area)
	{
		area = null!;
		return false;
	}

	public bool IsRegional(out TileTagger<bool> tagger)
	{
		tagger = BuildTagger();
		return true;
	}

	IEnumerable<ExternalImageVM> ILayerVM.ExternalImage => Enumerable.Empty<ExternalImageVM>();

	public LayerVM()
	{
		tileSize = 8;
		_painter = RebuildPainter();
	}

	private bool _isVisible = true;
	public bool IsVisible
	{
		get => _isVisible;
		set => ChangeProperty(ref _isVisible, value);
	}

	private string _layerName = "New Layer";
	public string LayerName
	{
		get => _layerName;
		set => ChangeProperty(ref _layerName, value);
	}

	// IoA default size
	const int w = 27 * 32;
	const int h = 19 * 32;

	public static LayerVM BuildChunkMask()
	{
		var vm = new LayerVM();
		vm.tileSize = 32;
		vm._layerName = "Chunk Mask";
		var painter = vm.RebuildPainter();

		foreach (var xz in chunkMaskIoA())
		{
			painter.SetStatus(xz, true);
		}

		return vm;
	}

	private TileGridPainterVM RebuildPainter()
	{
		_painter = new TileGridPainterVM(new XZ(w / tileSize, h / tileSize))
		{
			TileSize = tileSize
		};
		OnPropertyChanged(nameof(TileGridPainterVM));
		return _painter;
	}

	[Browsable(false)]
	public ITileGridPainterVM TileGridPainterVM => _painter;

	[ItemsSource(typeof(TileSizeItemsSource))]
	public int TileSize
	{
		get => tileSize;
		set
		{
			if (tileSize != value)
			{
				tileSize = value;
				OnPropertyChanged();
				RebuildPainter();
			}
		}
	}

	public TileTagger<bool> BuildTagger()
	{
		var unscaledSize = new XZ(_painter.ColumnCount, _painter.RowCount);
		var scale = new XZ(_painter.TileSize, _painter.TileSize);
		var tagger = new TileTagger<bool>(unscaledSize, scale);
		foreach (var xz in new Core.Rect(XZ.Zero, unscaledSize).Enumerate())
		{
			tagger.AddTag(xz, _painter.GetStatus(xz));
		}
		return tagger;
	}

	// TEMP - exported from Racket, should read from STGDAT instead
	// Also should make this layer read-only by default
	private static IEnumerable<XZ> chunkMaskIoA()
	{
		yield return new XZ(7, 0);
		yield return new XZ(8, 0);
		yield return new XZ(9, 0);
		yield return new XZ(10, 0);
		yield return new XZ(11, 0);
		yield return new XZ(12, 0);
		yield return new XZ(13, 0);
		yield return new XZ(14, 0);
		yield return new XZ(18, 0);
		yield return new XZ(19, 0);
		yield return new XZ(20, 0);
		yield return new XZ(6, 1);
		yield return new XZ(7, 1);
		yield return new XZ(8, 1);
		yield return new XZ(9, 1);
		yield return new XZ(10, 1);
		yield return new XZ(11, 1);
		yield return new XZ(12, 1);
		yield return new XZ(13, 1);
		yield return new XZ(14, 1);
		yield return new XZ(15, 1);
		yield return new XZ(16, 1);
		yield return new XZ(17, 1);
		yield return new XZ(18, 1);
		yield return new XZ(19, 1);
		yield return new XZ(20, 1);
		yield return new XZ(21, 1);
		yield return new XZ(22, 1);
		yield return new XZ(4, 2);
		yield return new XZ(5, 2);
		yield return new XZ(6, 2);
		yield return new XZ(7, 2);
		yield return new XZ(8, 2);
		yield return new XZ(9, 2);
		yield return new XZ(10, 2);
		yield return new XZ(11, 2);
		yield return new XZ(12, 2);
		yield return new XZ(13, 2);
		yield return new XZ(14, 2);
		yield return new XZ(15, 2);
		yield return new XZ(16, 2);
		yield return new XZ(17, 2);
		yield return new XZ(18, 2);
		yield return new XZ(19, 2);
		yield return new XZ(20, 2);
		yield return new XZ(21, 2);
		yield return new XZ(22, 2);
		yield return new XZ(3, 3);
		yield return new XZ(4, 3);
		yield return new XZ(5, 3);
		yield return new XZ(6, 3);
		yield return new XZ(7, 3);
		yield return new XZ(8, 3);
		yield return new XZ(9, 3);
		yield return new XZ(10, 3);
		yield return new XZ(11, 3);
		yield return new XZ(12, 3);
		yield return new XZ(13, 3);
		yield return new XZ(14, 3);
		yield return new XZ(15, 3);
		yield return new XZ(16, 3);
		yield return new XZ(17, 3);
		yield return new XZ(18, 3);
		yield return new XZ(19, 3);
		yield return new XZ(20, 3);
		yield return new XZ(21, 3);
		yield return new XZ(22, 3);
		yield return new XZ(2, 4);
		yield return new XZ(3, 4);
		yield return new XZ(4, 4);
		yield return new XZ(5, 4);
		yield return new XZ(6, 4);
		yield return new XZ(7, 4);
		yield return new XZ(8, 4);
		yield return new XZ(9, 4);
		yield return new XZ(10, 4);
		yield return new XZ(11, 4);
		yield return new XZ(12, 4);
		yield return new XZ(13, 4);
		yield return new XZ(14, 4);
		yield return new XZ(15, 4);
		yield return new XZ(16, 4);
		yield return new XZ(17, 4);
		yield return new XZ(18, 4);
		yield return new XZ(19, 4);
		yield return new XZ(20, 4);
		yield return new XZ(21, 4);
		yield return new XZ(22, 4);
		yield return new XZ(23, 4);
		yield return new XZ(24, 4);
		yield return new XZ(25, 4);
		yield return new XZ(1, 5);
		yield return new XZ(2, 5);
		yield return new XZ(3, 5);
		yield return new XZ(4, 5);
		yield return new XZ(5, 5);
		yield return new XZ(6, 5);
		yield return new XZ(7, 5);
		yield return new XZ(8, 5);
		yield return new XZ(9, 5);
		yield return new XZ(10, 5);
		yield return new XZ(11, 5);
		yield return new XZ(12, 5);
		yield return new XZ(13, 5);
		yield return new XZ(14, 5);
		yield return new XZ(15, 5);
		yield return new XZ(16, 5);
		yield return new XZ(17, 5);
		yield return new XZ(18, 5);
		yield return new XZ(19, 5);
		yield return new XZ(20, 5);
		yield return new XZ(21, 5);
		yield return new XZ(22, 5);
		yield return new XZ(23, 5);
		yield return new XZ(24, 5);
		yield return new XZ(25, 5);
		yield return new XZ(26, 5);
		yield return new XZ(1, 6);
		yield return new XZ(2, 6);
		yield return new XZ(3, 6);
		yield return new XZ(4, 6);
		yield return new XZ(5, 6);
		yield return new XZ(6, 6);
		yield return new XZ(7, 6);
		yield return new XZ(8, 6);
		yield return new XZ(9, 6);
		yield return new XZ(10, 6);
		yield return new XZ(11, 6);
		yield return new XZ(12, 6);
		yield return new XZ(13, 6);
		yield return new XZ(14, 6);
		yield return new XZ(15, 6);
		yield return new XZ(16, 6);
		yield return new XZ(17, 6);
		yield return new XZ(18, 6);
		yield return new XZ(19, 6);
		yield return new XZ(20, 6);
		yield return new XZ(21, 6);
		yield return new XZ(22, 6);
		yield return new XZ(23, 6);
		yield return new XZ(24, 6);
		yield return new XZ(25, 6);
		yield return new XZ(26, 6);
		yield return new XZ(0, 7);
		yield return new XZ(1, 7);
		yield return new XZ(2, 7);
		yield return new XZ(3, 7);
		yield return new XZ(4, 7);
		yield return new XZ(5, 7);
		yield return new XZ(6, 7);
		yield return new XZ(7, 7);
		yield return new XZ(8, 7);
		yield return new XZ(9, 7);
		yield return new XZ(10, 7);
		yield return new XZ(11, 7);
		yield return new XZ(12, 7);
		yield return new XZ(13, 7);
		yield return new XZ(14, 7);
		yield return new XZ(15, 7);
		yield return new XZ(16, 7);
		yield return new XZ(17, 7);
		yield return new XZ(18, 7);
		yield return new XZ(19, 7);
		yield return new XZ(20, 7);
		yield return new XZ(21, 7);
		yield return new XZ(22, 7);
		yield return new XZ(23, 7);
		yield return new XZ(24, 7);
		yield return new XZ(25, 7);
		yield return new XZ(26, 7);
		yield return new XZ(0, 8);
		yield return new XZ(1, 8);
		yield return new XZ(2, 8);
		yield return new XZ(3, 8);
		yield return new XZ(4, 8);
		yield return new XZ(5, 8);
		yield return new XZ(6, 8);
		yield return new XZ(7, 8);
		yield return new XZ(8, 8);
		yield return new XZ(9, 8);
		yield return new XZ(10, 8);
		yield return new XZ(11, 8);
		yield return new XZ(12, 8);
		yield return new XZ(13, 8);
		yield return new XZ(14, 8);
		yield return new XZ(15, 8);
		yield return new XZ(16, 8);
		yield return new XZ(17, 8);
		yield return new XZ(18, 8);
		yield return new XZ(19, 8);
		yield return new XZ(20, 8);
		yield return new XZ(21, 8);
		yield return new XZ(22, 8);
		yield return new XZ(23, 8);
		yield return new XZ(24, 8);
		yield return new XZ(25, 8);
		yield return new XZ(26, 8);
		yield return new XZ(0, 9);
		yield return new XZ(1, 9);
		yield return new XZ(2, 9);
		yield return new XZ(3, 9);
		yield return new XZ(4, 9);
		yield return new XZ(5, 9);
		yield return new XZ(6, 9);
		yield return new XZ(7, 9);
		yield return new XZ(8, 9);
		yield return new XZ(9, 9);
		yield return new XZ(10, 9);
		yield return new XZ(11, 9);
		yield return new XZ(12, 9);
		yield return new XZ(13, 9);
		yield return new XZ(14, 9);
		yield return new XZ(15, 9);
		yield return new XZ(16, 9);
		yield return new XZ(17, 9);
		yield return new XZ(18, 9);
		yield return new XZ(19, 9);
		yield return new XZ(20, 9);
		yield return new XZ(21, 9);
		yield return new XZ(22, 9);
		yield return new XZ(23, 9);
		yield return new XZ(24, 9);
		yield return new XZ(25, 9);
		yield return new XZ(26, 9);
		yield return new XZ(0, 10);
		yield return new XZ(1, 10);
		yield return new XZ(2, 10);
		yield return new XZ(3, 10);
		yield return new XZ(4, 10);
		yield return new XZ(5, 10);
		yield return new XZ(6, 10);
		yield return new XZ(7, 10);
		yield return new XZ(8, 10);
		yield return new XZ(9, 10);
		yield return new XZ(10, 10);
		yield return new XZ(11, 10);
		yield return new XZ(12, 10);
		yield return new XZ(13, 10);
		yield return new XZ(14, 10);
		yield return new XZ(15, 10);
		yield return new XZ(16, 10);
		yield return new XZ(17, 10);
		yield return new XZ(18, 10);
		yield return new XZ(19, 10);
		yield return new XZ(20, 10);
		yield return new XZ(21, 10);
		yield return new XZ(22, 10);
		yield return new XZ(23, 10);
		yield return new XZ(24, 10);
		yield return new XZ(25, 10);
		yield return new XZ(26, 10);
		yield return new XZ(0, 11);
		yield return new XZ(1, 11);
		yield return new XZ(2, 11);
		yield return new XZ(3, 11);
		yield return new XZ(4, 11);
		yield return new XZ(5, 11);
		yield return new XZ(6, 11);
		yield return new XZ(7, 11);
		yield return new XZ(8, 11);
		yield return new XZ(9, 11);
		yield return new XZ(10, 11);
		yield return new XZ(11, 11);
		yield return new XZ(12, 11);
		yield return new XZ(13, 11);
		yield return new XZ(14, 11);
		yield return new XZ(15, 11);
		yield return new XZ(16, 11);
		yield return new XZ(17, 11);
		yield return new XZ(18, 11);
		yield return new XZ(19, 11);
		yield return new XZ(20, 11);
		yield return new XZ(21, 11);
		yield return new XZ(22, 11);
		yield return new XZ(23, 11);
		yield return new XZ(24, 11);
		yield return new XZ(25, 11);
		yield return new XZ(1, 12);
		yield return new XZ(2, 12);
		yield return new XZ(3, 12);
		yield return new XZ(4, 12);
		yield return new XZ(5, 12);
		yield return new XZ(6, 12);
		yield return new XZ(7, 12);
		yield return new XZ(8, 12);
		yield return new XZ(9, 12);
		yield return new XZ(10, 12);
		yield return new XZ(11, 12);
		yield return new XZ(12, 12);
		yield return new XZ(13, 12);
		yield return new XZ(14, 12);
		yield return new XZ(15, 12);
		yield return new XZ(16, 12);
		yield return new XZ(17, 12);
		yield return new XZ(18, 12);
		yield return new XZ(19, 12);
		yield return new XZ(20, 12);
		yield return new XZ(21, 12);
		yield return new XZ(22, 12);
		yield return new XZ(23, 12);
		yield return new XZ(2, 13);
		yield return new XZ(3, 13);
		yield return new XZ(4, 13);
		yield return new XZ(5, 13);
		yield return new XZ(6, 13);
		yield return new XZ(7, 13);
		yield return new XZ(8, 13);
		yield return new XZ(9, 13);
		yield return new XZ(10, 13);
		yield return new XZ(11, 13);
		yield return new XZ(12, 13);
		yield return new XZ(13, 13);
		yield return new XZ(14, 13);
		yield return new XZ(15, 13);
		yield return new XZ(16, 13);
		yield return new XZ(17, 13);
		yield return new XZ(18, 13);
		yield return new XZ(19, 13);
		yield return new XZ(20, 13);
		yield return new XZ(21, 13);
		yield return new XZ(22, 13);
		yield return new XZ(2, 14);
		yield return new XZ(3, 14);
		yield return new XZ(4, 14);
		yield return new XZ(5, 14);
		yield return new XZ(6, 14);
		yield return new XZ(7, 14);
		yield return new XZ(8, 14);
		yield return new XZ(9, 14);
		yield return new XZ(10, 14);
		yield return new XZ(11, 14);
		yield return new XZ(12, 14);
		yield return new XZ(13, 14);
		yield return new XZ(14, 14);
		yield return new XZ(15, 14);
		yield return new XZ(16, 14);
		yield return new XZ(17, 14);
		yield return new XZ(18, 14);
		yield return new XZ(19, 14);
		yield return new XZ(20, 14);
		yield return new XZ(4, 15);
		yield return new XZ(5, 15);
		yield return new XZ(6, 15);
		yield return new XZ(7, 15);
		yield return new XZ(8, 15);
		yield return new XZ(9, 15);
		yield return new XZ(10, 15);
		yield return new XZ(11, 15);
		yield return new XZ(12, 15);
		yield return new XZ(13, 15);
		yield return new XZ(14, 15);
		yield return new XZ(15, 15);
		yield return new XZ(16, 15);
		yield return new XZ(17, 15);
		yield return new XZ(18, 15);
		yield return new XZ(9, 16);
		yield return new XZ(10, 16);
		yield return new XZ(11, 16);
		yield return new XZ(12, 16);
		yield return new XZ(13, 16);
		yield return new XZ(14, 16);
		yield return new XZ(9, 17);
		yield return new XZ(10, 17);
		yield return new XZ(11, 17);
		yield return new XZ(12, 17);
		yield return new XZ(13, 17);
		yield return new XZ(10, 18);
		yield return new XZ(11, 18);
		yield return new XZ(12, 18);
	}
}
