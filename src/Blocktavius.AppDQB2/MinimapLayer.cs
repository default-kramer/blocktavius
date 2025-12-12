using Antipasta;
using Blocktavius.Core;
using Blocktavius.DQB2;
using System.Collections.Immutable;
using System.IO;
using System.Windows.Media.Imaging;

namespace Blocktavius.AppDQB2;

sealed class MinimapLayer : ViewModelBaseWithCustomTypeDescriptor, ILayerVM
{
	IEnumerable<ExternalImageVM> ILayerVM.ExternalImage => Enumerable.Empty<ExternalImageVM>();
	IAreaVM? ILayerVM.SelfAsAreaVM => null;

	[ElementAsProperty("MinimapImage33")]
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
		public sealed class MinimapImage : AsyncDerivedProp<MinimapImage, MinimapImage.Input, BitmapSource>,
			I.Project.MinimapImage,
			IAsyncComputation<MinimapImage.Input, BitmapSource>
		{
			public sealed record Input
			{
				public required Minimap? Minimap { get; init; }
				public required ICloneableStage? Stage { get; init; }
				public required IReadOnlySet<ChunkOffset> ChunkExpansion { get; init; }
				public required int IslandId { get; init; }

				public static readonly Input Nothing = new()
				{
					Minimap = null,
					Stage = null,
					ChunkExpansion = ImmutableHashSet<ChunkOffset>.Empty,
					IslandId = -1,
				};
			}

			private readonly I.Project.SelectedSourceStage selectedSourceStage;
			private readonly I.Project.LoadedStage loadedStage;
			private readonly I.Project.ChunkExpansion chunkExpansion;

			public MinimapImage(I.Project.SelectedSourceStage selectedSourceStage, I.Project.LoadedStage loadedStage, I.Project.ChunkExpansion chunkExpansion)
			{
				this.selectedSourceStage = ListenTo(selectedSourceStage);
				this.loadedStage = ListenTo(loadedStage);
				this.chunkExpansion = ListenTo(chunkExpansion);
			}

			protected override Input BuildInput()
			{
				if (!MinimapRenderer.IsEnabled)
				{
					return Input.Nothing;
				}
				return new Input()
				{
					ChunkExpansion = chunkExpansion.Value,
					Minimap = loadedStage.Value?.Minimap,
					Stage = loadedStage.Value?.Stage,
					IslandId = selectedSourceStage.Value?.MinimapIslandIds?.FirstOrDefault(-1) ?? -1,
				};
			}

			static async Task IAsyncComputation<Input, BitmapSource>.Compute(IAsyncContext<BitmapSource> context, Input input)
			{
				context.UpdateValue(null);
				if (!MinimapRenderer.IsEnabled)
				{
					return;
				}

				var map = input.Minimap;
				var stage = input.Stage;
				var islandId = input.IslandId;

				if (map == null || stage == null || islandId < 0)
				{
					return;
				}

				await context.UnblockAsync();

				var sampler = map.ReadMapCropped(islandId, stage, input.ChunkExpansion).TranslateTo(XZ.Zero);
				var image = MinimapRenderer.Render(sampler, new MinimapRenderOptions());

				context.UpdateValue(image);
			}
		}
	}
}
