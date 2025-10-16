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
}
