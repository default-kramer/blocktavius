using Blocktavius.Core;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Blocktavius.AppDQB2;

sealed class ChunkGridLayer : ViewModelBase, ILayerVM
{
	private readonly StgdatLoader stgdatLoader = new();

	public string LayerName => "Chunk Grid";

	private bool _isVisible = true;
	public bool IsVisible
	{
		get => _isVisible;
		set => ChangeProperty(ref _isVisible, value);
	}

	private string _stgdatPath = "";
	public string StgdatPath
	{
		get => _stgdatPath;
		set
		{
			if (_stgdatPath != value)
			{
				ChangeProperty(ref _stgdatPath, value);
				ChunkGridImage = null;
				RebuildImage(value);
			}
		}
	}

	private BitmapSource? _chunkGridImage = null;
	public BitmapSource? ChunkGridImage
	{
		get => _chunkGridImage;
		private set => ChangeProperty(ref _chunkGridImage, value);
	}

	public IEnumerable<ExternalImageVM> ExternalImage => Enumerable.Empty<ExternalImageVM>();

	public bool IsArea(XZ imageTranslation, out AreaWrapper area)
	{
		area = null!;
		return false;
	}

	public bool IsRegional(out TileTagger<bool> tagger)
	{
		tagger = null!;
		return false;
	}

	private void RebuildImage(string stgdatPath)
	{
		Task.Run(() =>
		{
			if (stgdatLoader.TryLoad(stgdatPath, out var result, out _))
			{
				var grid = result.Stage.ChunkGrid();
				var colorGrid = grid.Project(x => x ? RawColor.Black : RawColor.Transparent);
				var image = ImageBuilder.MakeBitmap(colorGrid, scale: 32);
				image.Freeze();
				Application.Current.Dispatcher.Invoke(() => { ChunkGridImage = image; });
			}
		});
	}
}
