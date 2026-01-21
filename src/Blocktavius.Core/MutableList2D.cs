using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

internal sealed class MutableList2D<T> : I2DSampler<T>
{
	private readonly T defaultValue;
	private readonly MidpointList<MidpointList<T>> xLookup;
	private readonly XZ centerXZ;
	private readonly Rect.BoundsFinder BoundsTracker = new();

	public MutableList2D(T defaultValue, Rect boundsHint)
	{
		this.defaultValue = defaultValue;
		if (boundsHint.IsZero)
		{
			throw new ArgumentException("boundsHint must be nonzero");
		}
		else
		{
			centerXZ = boundsHint.ApproximateCenter();
		}
		xLookup = new MidpointList<MidpointList<T>>(centerXZ.X);
	}

	public Rect Bounds => BoundsTracker.CurrentBounds() ?? Rect.Zero;

	public T Sample(XZ xz)
	{
		if (xLookup.TryGetValue(xz.X, out var zLookup))
		{
			if (zLookup.TryGetValue(xz.Z, out var item))
			{
				return item;
			}
		}
		return defaultValue;
	}

	public void Put(XZ xz, T value)
	{
		if (!xLookup.TryGetValue(xz.X, out var zLookup))
		{
			zLookup = new MidpointList<T>(centerXZ.Z);
			xLookup.SetValue(xz.X, zLookup);
		}
		zLookup.SetValue(xz.Z, value);
		BoundsTracker.Include(xz);
	}

	readonly record struct Optional<A>(bool HasValue, A Value)
	{
		public static Optional<A> None => new();
		public static Optional<A> Some(A value) => new(true, value);
	}

	readonly struct MidpointList<A>
	{
		private readonly int midpoint;
		private readonly List<Optional<A>> list = new();

		public MidpointList(int midpoint)
		{
			this.midpoint = midpoint;
		}

		private int GetIndex(int i)
		{
			int delta = i - midpoint;
			if (delta >= 0)
			{
				return 2 * delta; // nonnegative: even index
			}
			else
			{
				return 2 * -delta - 1; // negative: odd index
			}
		}

		public bool TryGetValue(int i, out A value)
		{
			int index = GetIndex(i);
			if (index < list.Count)
			{
				var entry = list[index];
				value = entry.Value;
				return entry.HasValue;
			}
			else
			{
				value = default!;
				return false;
			}
		}

		public void SetValue(int i, A value)
		{
			int index = GetIndex(i);
			while (index >= list.Count)
			{
				list.Add(Optional<A>.None);
			}
			list[index] = Optional<A>.Some(value);
		}
	}
}
