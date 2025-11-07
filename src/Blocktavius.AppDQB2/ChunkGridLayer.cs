using Blocktavius.Core;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rect = Blocktavius.Core.Rect;

namespace Blocktavius.AppDQB2;

sealed class ChunkGridLayer : ViewModelBase, ILayerVM
{
	IAreaVM? ILayerVM.SelfAsAreaVM => null;

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

	private void RebuildImage(string stgdatPath)
	{
		Task.Run(() =>
		{
			if (stgdatLoader.TryLoad(stgdatPath, out var result, out _))
			{
				var image = BuildImage(result.Stage.ChunksInUse);
				Application.Current.Dispatcher.Invoke(() => { ChunkGridImage = image; });
			}
		});
	}

	public void RebuildImage(IEnumerable<ChunkOffset> chunks)
	{
		Task.Run(() =>
		{
			var image = BuildImage(chunks);
			Application.Current.Dispatcher.Invoke(() => { ChunkGridImage = image; });
		});
	}

	private static BitmapSource BuildImage(IEnumerable<ChunkOffset> chunks)
	{
		var grid = BuildSampler(chunks);
		var colorGrid = grid.Project(x => x ? RawColor.Black : RawColor.Transparent);
		var image = ImageBuilder.MakeBitmap(colorGrid, scale: 32);
		image.Freeze();
		return image;
	}

	private static I2DSampler<bool> BuildSampler(IEnumerable<ChunkOffset> chunks)
	{
		var bounds = Rect.GetBounds(chunks.Select(c => new XZ(c.OffsetX, c.OffsetZ)));
		var array = new MutableArray2D<bool>(bounds, false);
		foreach (var chunk in chunks)
		{
			array.Put(new XZ(chunk.OffsetX, chunk.OffsetZ), true);
		}
		return array;
	}
}
