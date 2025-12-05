using Antipasta;
using Blocktavius.Core;
using Blocktavius.DQB2;
using System.IO;
using System.Windows.Media.Imaging;

namespace Blocktavius.AppDQB2;

sealed class MinimapLayer : ViewModelBase, ILayerVM
{
	IEnumerable<ExternalImageVM> ILayerVM.ExternalImage => Enumerable.Empty<ExternalImageVM>();
	IAreaVM? ILayerVM.SelfAsAreaVM => null;

	private readonly MyProperty.MinimapImage minimapImage;
	public MinimapLayer(I.Project.SelectedSourceStage selectedSourceStage, I.Project.LoadedStage loadedStage, I.Project.ChunkExpansion chunkExpansion)
	{
		minimapImage = new(selectedSourceStage, loadedStage, chunkExpansion) { Owner = this };
	}

	public string LayerName => "Minimap";

	private bool _isVisible = true;
	public bool IsVisible
	{
		get => _isVisible;
		set => ChangeProperty(ref _isVisible, value);
	}

	public BitmapSource? MinimapImage => minimapImage.Value;

	static class MyProperty
	{
		public sealed class MinimapImage : DerivedProp<MinimapImage, BitmapSource?>, I.Project.MinimapImage, IImmediateNotifyNode
		{
			string IImmediateNotifyNode.PropertyName => nameof(MinimapLayer.MinimapImage);

			private readonly I.Project.SelectedSourceStage selectedSourceStage;
			private readonly I.Project.LoadedStage loadedStage;
			private readonly I.Project.ChunkExpansion chunkExpansion;

			public MinimapImage(I.Project.SelectedSourceStage selectedSourceStage, I.Project.LoadedStage loadedStage, I.Project.ChunkExpansion chunkExpansion)
			{
				this.selectedSourceStage = ListenTo(selectedSourceStage);
				this.loadedStage = ListenTo(loadedStage);
				this.chunkExpansion = ListenTo(chunkExpansion);
			}

			protected override BitmapSource? Recompute()
			{
				if (!MinimapRenderer.IsEnabled)
				{
					return null;
				}

				var map = loadedStage.Value?.Minimap;
				var stage = loadedStage.Value?.Stage;
				var islandId = selectedSourceStage.Value?.MinimapIslandIds?.FirstOrDefault();

				if (map == null || stage == null || !islandId.HasValue)
				{
					return null;
				}

				// TODO: This is not efficient, we shouldn't need to copy the whole stage when all we
				// really need is the expanded chunks!
				var expandedStage = stage.Clone();
				expandedStage.ExpandChunks(chunkExpansion.Value);

				var sampler = map.ReadMapCropped(islandId.Value, expandedStage).TranslateTo(XZ.Zero);
				var image = MinimapRenderer.Render(sampler, new MinimapRenderOptions());
				return image;
			}
		}
	}
}
