
namespace Blocktavius.Core;

public interface IRawImage
{
	int Width { get; }
	int Height { get; }
	// Returns the color of the pixel at the given coordinates.
	// The exact color representation can be decided later, but for now
	// let's assume a simple struct or class representing RGBA.
	RawColor GetPixel(int x, int y);
}

public struct RawColor
{
	public byte R, G, B, A;

	public static RawColor Black => new RawColor { R = 0, G = 0, B = 0, A = 0 };
	public static RawColor Transparent => new RawColor { R = 0, G = 0, B = 0, A = 255 };
}
