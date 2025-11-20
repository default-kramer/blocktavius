using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Blocktavius.AppDQB2;

sealed class ExternalImageVM : ViewModelBase
{
	public readonly Guid UniqueId;
	public readonly FileInfo ImageFile;
	public string RelativePath { get; }
	private DateTime lastReloadUtc = DateTime.MinValue;

	public ExternalImageVM(Guid uniqueId, FileInfo imageFile, string relativePath)
	{
		this.UniqueId = uniqueId;
		this.ImageFile = imageFile;
		this.RelativePath = relativePath;
	}

	internal void ReloadIfStale()
	{
		if (!File.Exists(ImageFile.FullName))
		{
			ImageSource = null;
			return;
		}

		try
		{
			var lastWriteUtc = File.GetLastWriteTimeUtc(ImageFile.FullName);
			if (lastWriteUtc <= lastReloadUtc)
			{
				return;
			}
			ImageSource = LoadBitmapAndNormalize(ImageFile.FullName);
			lastReloadUtc = lastWriteUtc;
		}
		catch (Exception)
		{
			ImageSource = null;
		}
	}

	private (BitmapSource bitmap, (XZ imageTranslation, AreaWrapper area)? areaCache)? _imageSource;
	public BitmapSource? ImageSource
	{
		get => _imageSource?.Item1;
		set
		{
			if (value == null)
			{
				_imageSource = null;
			}
			else
			{
				_imageSource = (value, null);
			}
			OnPropertyChanged(nameof(ImageSource));
		}
	}

	internal AreaWrapper? GetArea(XZ imageTranslation)
	{
		if (_imageSource == null)
		{
			return null;
		}
		else if (_imageSource.Value.areaCache?.imageTranslation == imageTranslation)
		{
			return _imageSource.Value.areaCache.Value.area;
		}
		else
		{
			IArea area = new RawImageArea(new BitmapSourceRawImage(_imageSource.Value.bitmap));
			area = area.Translate(imageTranslation);
			var wrapper = new AreaWrapper(area);
			_imageSource = (_imageSource.Value.bitmap, (imageTranslation, wrapper));
			return wrapper;
		}
	}

	/// <summary>
	/// WPF and/or Windows attempts to scale things based on DPI metadata in ways
	/// that can surprise me. So normalize every image we load.
	/// </summary>
	private static BitmapSource LoadBitmapAndNormalize(string fullPath)
	{
		byte[] buffer = File.ReadAllBytes(fullPath);
		var originalBitmap = new BitmapImage();
		using (var stream = new MemoryStream(buffer))
		{
			originalBitmap.BeginInit();
			originalBitmap.CacheOption = BitmapCacheOption.OnLoad;
			originalBitmap.StreamSource = stream;
			originalBitmap.EndInit();
		}
		originalBitmap.Freeze();

		// Gemini says "Even though modern high-DPI ("Retina") displays have much higher pixel densities,
		// the entire Windows scaling system is still based on this fundamental 96 DPI standard."
		BitmapSource finalImage = originalBitmap;
		if (originalBitmap.DpiX != 96 || originalBitmap.DpiY != 96)
		{
			int stride = originalBitmap.PixelWidth * (originalBitmap.Format.BitsPerPixel + 7) / 8;
			byte[] pixelData = new byte[stride * originalBitmap.PixelHeight];
			originalBitmap.CopyPixels(pixelData, stride, 0);

			finalImage = BitmapSource.Create(
				originalBitmap.PixelWidth,
				originalBitmap.PixelHeight,
				96, 96,
				originalBitmap.Format,
				originalBitmap.Palette,
				pixelData,
				stride);
			finalImage.Freeze();
		}

		return finalImage;
	}

	sealed class BitmapSourceRawImage : IRawImage
	{
		private readonly BitmapSource bgra32Source;
		const int bytesPerPixel = 4;

		public BitmapSourceRawImage(BitmapSource bitmapSource)
		{
			if (bitmapSource.Format != System.Windows.Media.PixelFormats.Bgra32)
			{
				var converted = new FormatConvertedBitmap(bitmapSource, System.Windows.Media.PixelFormats.Bgra32, null, 0);
				converted.Freeze();
				bgra32Source = converted;
			}
			else
			{
				bgra32Source = bitmapSource;
			}

			int actualBytesPerPixel = (bgra32Source.Format.BitsPerPixel + 7) / 8;
			if (actualBytesPerPixel != bytesPerPixel)
			{
				throw new InvalidOperationException("This method assumes a 32-bit pixel format.");
			}
		}

		public int Width => bgra32Source.PixelWidth;

		public int Height => bgra32Source.PixelHeight;

		public RawColor GetPixel(int x, int y)
		{
			if (x < 0 || x >= Width || y < 0 || y >= Height)
			{
				return new RawColor { R = 0, G = 0, B = 0, A = 0 };
			}

			var stride = bytesPerPixel * Width;
			var pixelData = new byte[bytesPerPixel];
			bgra32Source.CopyPixels(new System.Windows.Int32Rect(x, y, 1, 1), pixelData, stride, 0);
			return new RawColor { B = pixelData[0], G = pixelData[1], R = pixelData[2], A = pixelData[3] };
		}
	}
}
