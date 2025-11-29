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
		private set => ChangeProperty(ref _minimapImage, value);
	}

	public async void RebuildImage(ProjectVM project)
	{
		if (project.SelectedSourceStage == null
			|| project.SelectedSourceStage.MinimapIslandIds.Count < 1
			|| !MinimapRenderer.IsEnabled)
		{
			MinimapImage = null;
			return;
		}

		int islandId = project.SelectedSourceStage.MinimapIslandIds.First();
		var StgdatPath = project.SelectedSourceStage.StgdatFile.FullName;
		var stage = await project.TryLoadMutableStage(expandChunks: true);
		if (stage == null)
		{
			return;
		}

		// TODO should cache CMNDAT loading similar to STGDAT loading.
		// ALSO - there's probably concurrency bugs in this code,
		// because the ProjectVM could be mutated by the UI thread...
		var image = await GetMinimapImage(StgdatPath, islandId, stage);
		this.MinimapImage = image;
	}

	private static Task<BitmapSource?> GetMinimapImage(string StgdatPath, int islandId, IStage stage)
	{
		return Task.Run(() =>
		{
			var cmndatPath = Path.Combine(new FileInfo(StgdatPath).Directory?.FullName ?? "<<FAIL>>", "CMNDAT.BIN");
			var cmndatFile = new FileInfo(cmndatPath);
			if (cmndatFile.Exists)
			{
				var map = Minimap.FromCmndatFile(cmndatFile);
				var sampler = map.ReadMapCropped(islandId, stage).TranslateTo(XZ.Zero);
				var image = MinimapRenderer.Render(sampler, new MinimapRenderOptions());
				return image;
			}
			return null;
		});
	}
}
