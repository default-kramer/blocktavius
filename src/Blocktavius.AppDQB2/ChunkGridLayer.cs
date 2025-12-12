using Antipasta;
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
using Rect = Blocktavius.Core.Rect;

namespace Blocktavius.AppDQB2;

sealed class ChunkGridLayer : ViewModelBaseWithCustomTypeDescriptor, ILayerVM
{
	IAreaVM? ILayerVM.SelfAsAreaVM => null;

	[ElementAsProperty("ChunkGridImage86")]
	private readonly MyProperty.ChunkMaskImage xChunkMaskImage;

	public ChunkGridLayer(I.Project.ChunkExpansion chunkExpansion, I.Project.LoadedStage loadedStage)
	{
		xChunkMaskImage = new(chunkExpansion, loadedStage) { Owner = this };
	}

	public string LayerName => "Chunk Grid";

	private bool _isVisible = true;
	public bool IsVisible
	{
		get => _isVisible;
		set => ChangeProperty(ref _isVisible, value);
	}

	public BitmapSource? ChunkGridImage => xChunkMaskImage.Value;

	public IEnumerable<ExternalImageVM> ExternalImage => Enumerable.Empty<ExternalImageVM>();

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

	static class MyProperty
	{
		public sealed class ChunkMaskImage : DerivedProp<ChunkMaskImage, BitmapSource?>, I.Project.ChunkMaskImage
		{
			private readonly I.Project.ChunkExpansion chunkExpansion;
			private readonly I.Project.LoadedStage loadedStage;

			public ChunkMaskImage(I.Project.ChunkExpansion chunkExpansion, I.Project.LoadedStage loadedStage)
			{
				this.chunkExpansion = ListenTo(chunkExpansion);
				this.loadedStage = ListenTo(loadedStage);
			}

			protected override BitmapSource? Recompute()
			{
				var stage = loadedStage.Value?.Stage;
				if (stage == null)
				{
					return null;
				}

				return ChunkGridLayer.BuildImage(stage.ChunksInUse.Concat(chunkExpansion.Value));
			}
		}
	}
}
