using System.Collections.Immutable;

namespace Blocktavius.Core;

public interface IHaveElevation
{
	int Y { get; }
}

public record struct Elevation(int Y) : IHaveElevation;
public record struct Point(XZ xz, int Y) : IHaveElevation { }

public record struct XZ(int X, int Z)
{
	public static XZ Zero => new XZ(0, 0);

	public XZ Add(int dx, int dz) => new XZ(X + dx, Z + dz);

	public XZ Add(XZ xz) => Add(xz.X, xz.Z);

	public XZ Step(Direction direction) => Add(direction.Step);

	public XZ Step(Direction direction, int steps) => Add(direction.Step.Scale(steps));

	public XZ Subtract(XZ xz) => new XZ(X - xz.X, Z - xz.Z);

	public XZ Scale(int factor) => new XZ(X * factor, Z * factor);

	public XZ Scale(XZ scale) => new XZ(X * scale.X, Z * scale.Z);

	public XZ Unscale(XZ scale) => new XZ(X / scale.X, Z / scale.Z);

	public IEnumerable<XZ> CardinalNeighbors()
	{
		yield return Add(1, 0);
		yield return Add(-1, 0);
		yield return Add(0, 1);
		yield return Add(0, -1);
	}

	public IEnumerable<XZ> Walk(Direction direction, int steps)
	{
		var current = this;
		while (steps > 0)
		{
			yield return current;
			current = current.Step(direction);
			steps--;
		}
	}
}

public record Rect(XZ start, XZ end)
{
	public static readonly Rect Zero = new(new XZ(0, 0), new XZ(0, 0));

	public bool Contains(XZ xz) => GetIndex(xz).HasValue;

	/// <summary>
	/// If the given <paramref name="xz"/> is within this box, returns a unique index
	/// for that xz in the inclusive range 0 .. (Width*Height - 1).
	/// </summary>
	public int? GetIndex(XZ xz)
	{
		if (xz.X >= end.X || xz.Z >= end.Z)
		{
			return null;
		}

		int zIndex = xz.Z - start.Z;
		if (zIndex < 0)
		{
			return null;
		}

		int xIndex = xz.X - start.X;
		if (xIndex < 0)
		{
			return null;
		}

		int width = end.X - start.X;
		return zIndex * width + xIndex;
	}

	public XZ Size => new XZ(end.X - start.X, end.Z - start.Z);

	public bool IsZero => Size.X < 1 || Size.Z < 1;

	public static Rect Union(IEnumerable<Rect> boxes) => DoUnion(boxes);

	public static Rect Union(params IEnumerable<Rect>[] dater) => DoUnion(dater);

	private static Rect DoUnion(params IEnumerable<Rect>[] seqs)
	{
		int minX = int.MaxValue;
		int minZ = int.MaxValue;

		int maxX = int.MinValue;
		int maxZ = int.MinValue;

		bool any = false;

		foreach (var boxes in seqs)
		{
			foreach (var box in boxes)
			{
				any = true;

				var start = box.start;
				var end = box.end;

				minX = Math.Min(minX, start.X);
				minZ = Math.Min(minZ, start.Z);

				maxX = Math.Max(maxX, end.X);
				maxZ = Math.Max(maxZ, end.Z);
			}
		}

		if (!any)
		{
			return Rect.Zero;
		}

		return new Rect(new XZ(minX, minZ), new XZ(maxX, maxZ));
	}

	public Rect Translate(XZ xz) => new Rect(this.start.Add(xz), this.end.Add(xz));

	public IEnumerable<XZ> Enumerate()
	{
		for (int x = start.X; x < end.X; x++)
		{
			for (int z = start.Z; z < end.Z; z++)
			{
				yield return new XZ(x, z);
			}
		}
	}

	public static Rect GetBounds(IEnumerable<XZ> xzs)
	{
		int xMin = int.MaxValue;
		int zMin = int.MaxValue;

		int xMax = int.MinValue;
		int zMax = int.MinValue;

		foreach (var xz in xzs)
		{
			xMin = Math.Min(xMin, xz.X);
			zMin = Math.Min(zMin, xz.Z);

			xMax = Math.Max(xMax, xz.X);
			zMax = Math.Max(zMax, xz.Z);
		}

		return new Rect(new XZ(xMin, zMin), new XZ(xMax + 1, zMax + 1));
	}

	public Rect Intersection(Rect other)
	{
		int x0 = Math.Max(start.X, other.start.X);
		int z0 = Math.Max(start.Z, other.start.Z);
		int x1 = Math.Min(end.X, other.end.X);
		int z1 = Math.Min(end.Z, other.end.Z);

		if (x0 >= x1 || z0 >= z1)
		{
			return Rect.Zero;
		}

		return new Rect(new XZ(x0, z0), new XZ(x1, z1));
	}
}

public interface I2DSampler<out T>
{
	Rect Bounds { get; }
	T Sample(XZ xz);
}

public sealed class MutableArray2D<T> : I2DSampler<T>
{
	private readonly T[] array;
	private readonly T defaultValue;
	public Rect Bounds { get; }

	public MutableArray2D(Rect bounds, T defaultValue)
	{
		this.defaultValue = defaultValue;
		Bounds = bounds;
		array = new T[bounds.Size.X * bounds.Size.Z];
		array.AsSpan().Fill(defaultValue);
	}

	public T this[XZ xz]
	{
		get => array[Bounds.GetIndex(xz) ?? throw new ArgumentOutOfRangeException(nameof(xz))];
		set => Put(xz, value);
	}

	public T Sample(XZ xz)
	{
		var index = Bounds.GetIndex(xz);
		if (index.HasValue)
		{
			return array[index.Value];
		}
		return defaultValue;
	}

	public void Put(XZ xz, T value)
	{
		var index = Bounds.GetIndex(xz);
		if (!index.HasValue)
		{
			throw new ArgumentOutOfRangeException(nameof(xz));
		}
		array[index.Value] = value;
	}
}
