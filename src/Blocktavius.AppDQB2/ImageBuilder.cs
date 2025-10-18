using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Blocktavius.AppDQB2;

static class ImageBuilder
{
	public static BitmapSource MakeBitmap(I2DSampler<RawColor> sampler, int scale)
	{
		if (scale < 1)
		{
			throw new ArgumentException("scale must be at least 1");
		}

		int width = sampler.Bounds.Size.X;
		int height = sampler.Bounds.Size.Z;
		int scaledWidth = width * scale;
		int scaledHeight = height * scale;

		if (scaledWidth < 1 || scaledHeight < 1)
		{
			throw new ArgumentException("image must have positive width and height");
		}

		var bitmap = new WriteableBitmap(scaledWidth, scaledHeight, 96, 96, PixelFormats.Bgra32, null);
		byte[] pixelData = new byte[scaledWidth * scaledHeight * 4];

		foreach (var origXZ in sampler.Bounds.Enumerate())
		{
			RawColor color = sampler.Sample(origXZ);
			var xz = origXZ.Add(sampler.Bounds.start.Scale(-1));

			(byte a, byte r, byte g, byte b) = (color.A, color.R, color.G, color.B);

			for (int i = 0; i < scale; i++)
			{
				for (int j = 0; j < scale; j++)
				{
					int pixelX = xz.X * scale + i;
					int pixelZ = xz.Z * scale + j;

					int index = (pixelZ * scaledWidth + pixelX) * 4;
					pixelData[index] = b;
					pixelData[index + 1] = g;
					pixelData[index + 2] = r;
					pixelData[index + 3] = a;
				}
			}
		}

		bitmap.WritePixels(new Int32Rect(0, 0, scaledWidth, scaledHeight), pixelData, scaledWidth * 4, 0);
		return bitmap;
	}
}