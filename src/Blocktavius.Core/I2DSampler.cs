using System.Collections.Immutable;

namespace Blocktavius.Core;

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
