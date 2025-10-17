
using Blocktavius.Core;

namespace Blocktavius.Core;

public class RawImageArea : IArea
{
    private readonly IRawImage _rawImage;

    public RawImageArea(IRawImage rawImage)
    {
        _rawImage = rawImage;
        Bounds = CalculateBounds();
    }

    public Rect Bounds { get; }

    public bool InArea(XZ xz)
    {
        if (!Bounds.Contains(xz))
        {
            return false;
        }

        return _rawImage.GetPixel(xz.X, xz.Z).A > 0;
    }

    private Rect CalculateBounds()
    {
        int minX = _rawImage.Width;
        int minY = _rawImage.Height;
        int maxX = 0;
        int maxY = 0;

        for (int y = 0; y < _rawImage.Height; y++)
        {
            for (int x = 0; x < _rawImage.Width; x++)
            {
                if (_rawImage.GetPixel(x, y).A > 0)
                {
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (minX > maxX || minY > maxY)
        {
            return Rect.Zero;
        }

        return new Rect(new XZ(minX, minY), new XZ(maxX + 1, maxY + 1));
    }
}
