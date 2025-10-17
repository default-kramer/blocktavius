using Blocktavius.Core;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Blocktavius.AppDQB2;

sealed class ExternalImageLayerVM : ViewModelBase, ILayerVM
{
	public required ExternalImageVM Image { get; init; }

	public string LayerName => Image.RelativePath;

	IEnumerable<ExternalImageVM> ILayerVM.ExternalImage { get { yield return Image; } }

	private bool _isVisible = true;
	public bool IsVisible
	{
		get => _isVisible;
		set => ChangeProperty(ref _isVisible, value);
	}

	public TileTagger<bool> BuildTagger()
	{
		throw new NotImplementedException();
	}

	public bool IsArea(XZ imageTranslation, out AreaWrapper area)
	{
		area = Image?.GetArea(imageTranslation)!;
		return area != null;
	}

	public bool IsRegional(out TileTagger<bool> tagger)
	{
		tagger = null!;
		return false;
	}
}
