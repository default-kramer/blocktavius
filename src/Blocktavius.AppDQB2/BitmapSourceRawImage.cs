
using Blocktavius.Core;
using System.Windows.Media.Imaging;

namespace Blocktavius.AppDQB2;

public class BitmapSourceRawImage : IRawImage
{
    private readonly BitmapSource _bitmapSource;

    public BitmapSourceRawImage(BitmapSource bitmapSource)
    {
        _bitmapSource = bitmapSource;
    }

    public int Width => _bitmapSource.PixelWidth;

    public int Height => _bitmapSource.PixelHeight;

    public RawColor GetPixel(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return new RawColor { R = 0, G = 0, B = 0, A = 0 };
        }

        var bytesPerPixel = (_bitmapSource.Format.BitsPerPixel + 7) / 8;
        var stride = bytesPerPixel * Width;
        var pixelData = new byte[bytesPerPixel];
        _bitmapSource.CopyPixels(new System.Windows.Int32Rect(x, y, 1, 1), pixelData, stride, 0);

        // Assuming Bgra32 format
        return new RawColor { B = pixelData[0], G = pixelData[1], R = pixelData[2], A = pixelData[3] };
    }
}
