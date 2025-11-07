using Blocktavius.Core;
using Blocktavius.DQB2;
using System.IO;
using System.Windows.Media.Imaging;

namespace Blocktavius.AppDQB2;

sealed class MinimapLayer : ViewModelBase, ILayerVM
{
	IEnumerable<ExternalImageVM> ILayerVM.ExternalImage => Enumerable.Empty<ExternalImageVM>();
	IAreaVM? ILayerVM.SelfAsAreaVM => null;

	public string LayerName => "Minimap";

	private bool _isVisible = true;
	public bool IsVisible
	{
		get => _isVisible;
		set => ChangeProperty(ref _isVisible, value);
	}

	private BitmapSource? _minimapImage = null;
	public BitmapSource? MinimapImage
	{
		get => _minimapImage;
		set => ChangeProperty(ref _minimapImage, value);
	}

	public void RebuildImage(ProjectVM project)
	{
		const int islandId = 0; // TODO

		var StgdatPath = project.StgdatFilePath ?? "";

		var cmndatPath = Path.Combine(new FileInfo(StgdatPath).Directory?.FullName ?? "<<FAIL>>", "CMNDAT.BIN");
		var cmndatFile = new FileInfo(cmndatPath);
		if (cmndatFile.Exists)
		{
			var map = Minimap.FromCmndatFile(cmndatFile);
			if (project.TryLoadMutableStage(out var stage, expandChunks: true))
			{
				var sampler = map.ReadMapCropped(islandId, stage).TranslateTo(XZ.Zero);
				var image = MinimapRenderer.Render(sampler, new MinimapRenderOptions());
				this.MinimapImage = image;
			}
		}
	}
}
