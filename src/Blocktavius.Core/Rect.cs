using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

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

	public Rect Expand(int amount) => new Rect(this.start.Add(-amount, -amount), this.end.Add(amount, amount));

	internal XZ ApproximateCenter()
	{
		if (IsZero)
		{
			throw new InvalidOperationException("not defined for Zero rect");
		}
		int x = start.X + (end.X - start.X) / 2;
		int z = start.Z + (end.Z - start.Z) / 2;
		return new XZ(x, z);
	}

	public static Rect Union(IEnumerable<Rect> boxes) => DoUnion(boxes);

	public static Rect Union(params IEnumerable<Rect>[] dater) => DoUnion(dater);
	public static Rect Union(params Rect[] rects) => DoUnion(rects);

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
		for (int z = start.Z; z < end.Z; z++)
		{
			for (int x = start.X; x < end.X; x++)
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

	public sealed class BoundsFinder
	{
		private int xMin = int.MaxValue;
		private int zMin = int.MaxValue;
		private int xMax = int.MinValue;
		private int zMax = int.MinValue;

		public void Include(XZ xz)
		{
			xMin = Math.Min(xMin, xz.X);
			zMin = Math.Min(zMin, xz.Z);
			xMax = Math.Max(xMax, xz.X);
			zMax = Math.Max(zMax, xz.Z);
		}

		public Rect? CurrentBounds()
		{
			if (xMax >= xMin && zMax >= zMin)
			{
				return new Rect(new XZ(xMin, zMin), new XZ(xMax + 1, zMax + 1));
			}
			return null;
		}
	}

	sealed class RectArea : IArea
	{
		public required Rect Bounds { get; init; }

		public bool InArea(XZ xz) => Bounds.Contains(xz);
	}

	public IArea AsArea() => new RectArea { Bounds = this };
}
