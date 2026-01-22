using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

public static class Util
{
	public static T SafeCast<T>(this T item) => item;

	public static T? AsNullable<T>(this T item) where T : struct
	{
		return item;
	}

	public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> sequence)
	{
		return sequence.Where(x => x != null)!;
	}

	public static bool InArea(this I2DSampler<bool> area, XZ xz) => area.Sample(xz);

	public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T>? sequence) => sequence ?? Enumerable.Empty<T>();

	sealed class Translator<T> : I2DSampler<T>
	{
		private readonly I2DSampler<T> sampler;
		private readonly XZ translation;
		public Rect Bounds { get; }

		public Translator(I2DSampler<T> sampler, XZ translation)
		{
			this.sampler = sampler;
			this.Bounds = sampler.Bounds.Translate(translation);
			this.translation = translation;
		}

		public T Sample(XZ xz) => sampler.Sample(xz.Subtract(translation));
	}

	public static I2DSampler<T> Translate<T>(this I2DSampler<T> sampler, XZ xz)
	{
		return new Translator<T>(sampler, xz);
	}

	public static I2DSampler<T> TranslateTo<T>(this I2DSampler<T> sampler, XZ newTopLeft)
	{
		var relative = newTopLeft.Subtract(sampler.Bounds.start);
		var result = sampler.Translate(relative);
		return result;
	}

	abstract class Rotator<T> : I2DSampler<T>
	{
		protected readonly I2DSampler<T> sampler;
		public Rect Bounds { get; }

		protected Rotator(I2DSampler<T> sampler)
		{
			this.sampler = sampler;
			if (RotateBounds)
			{
				var start = sampler.Bounds.start;
				Bounds = new Rect(start, start.Add(sampler.Bounds.Size.Z, sampler.Bounds.Size.X));
			}
			else
			{
				Bounds = sampler.Bounds;
			}
		}

		protected abstract bool RotateBounds { get; }

		public T Sample(XZ xz)
		{
			xz = xz.Subtract(sampler.Bounds.start);
			xz = Blah(xz);
			xz = sampler.Bounds.start.Add(xz);
			return sampler.Sample(xz);
		}

		protected abstract XZ Blah(XZ blah);
	}

	class Rotator90<T> : Rotator<T>
	{
		public Rotator90(I2DSampler<T> sampler) : base(sampler) { }
		protected override bool RotateBounds => true;
		protected override XZ Blah(XZ blah)
		{
			return new XZ(blah.Z, Bounds.Size.X - 1 - blah.X);
		}
	}

	class Rotator180<T> : Rotator<T>
	{
		public Rotator180(I2DSampler<T> sampler) : base(sampler) { }
		protected override bool RotateBounds => false;
		protected override XZ Blah(XZ blah)
		{
			return new XZ(Bounds.Size.X - 1 - blah.X, Bounds.Size.Z - 1 - blah.Z);
		}
	}

	class Rotator270<T> : Rotator<T>
	{
		public Rotator270(I2DSampler<T> sampler) : base(sampler) { }
		protected override bool RotateBounds => true;
		protected override XZ Blah(XZ blah)
		{
			return new XZ(Bounds.Size.Z - 1 - blah.Z, blah.X);
		}
	}

	public static I2DSampler<T> Rotate<T>(this I2DSampler<T> sampler, int degrees)
	{
		degrees = (degrees % 360 + 360) % 360;

		if (degrees == 0)
		{
			return sampler;
		}
		else if (degrees == 90)
		{
			return new Rotator90<T>(sampler);
		}
		else if (degrees == 180)
		{
			return new Rotator180<T>(sampler);
		}
		else if (degrees == 270)
		{
			return new Rotator270<T>(sampler);
		}

		throw new ArgumentException($"{nameof(degrees)} must be a multiple of 90, but got {degrees}");
	}

	sealed class SwapEW<T> : I2DSampler<T>
	{
		private readonly I2DSampler<T> sampler;
		public SwapEW(I2DSampler<T> sampler)
		{
			this.sampler = sampler;
		}

		public Rect Bounds => sampler.Bounds;

		public T Sample(XZ xz)
		{
			int xOffset = xz.X - sampler.Bounds.start.X;
			int x = sampler.Bounds.end.X - (1 + xOffset);
			return sampler.Sample(new XZ(x, xz.Z));
		}
	}

	public static I2DSampler<T> SwapEastWest<T>(this I2DSampler<T> sampler)
	{
		return new SwapEW<T>(sampler);
	}

	sealed record class Cropper<T>(I2DSampler<T> sampler, Rect Bounds) : I2DSampler<T>
	{
		public T Sample(XZ xz)
		{
			if (Bounds.Contains(xz))
			{
				return sampler.Sample(xz);
			}
			return sampler.Sample(sampler.Bounds.end); // end is out of bounds by definition
		}
	}

	public static I2DSampler<T> Crop<T>(this I2DSampler<T> sampler, Rect newBounds) => new Cropper<T>(sampler, newBounds);

	sealed class ElevationAdjuster : I2DSampler<int>
	{
		public required I2DSampler<int> decorated { get; init; }
		public required int Adjustment { get; init; }

		public Rect Bounds => decorated.Bounds;

		public int Sample(XZ xz)
		{
			var elevation = decorated.Sample(xz);
			if (elevation >= 0)
			{
				return elevation + Adjustment;
			}
			return elevation;
		}
	}

	public static I2DSampler<int> AdjustElevation(this I2DSampler<int> sampler, int deltaY)
	{
		return new ElevationAdjuster() { decorated = sampler, Adjustment = deltaY };
	}

	sealed class Projection2D<TFrom, TTo> : I2DSampler<TTo>
	{
		public required I2DSampler<TFrom> From { get; init; }
		public required Func<TFrom, XZ, TTo> Project { get; init; }

		public Rect Bounds => From.Bounds;

		public TTo Sample(XZ xz) => Project(From.Sample(xz), xz);
	}

	public static I2DSampler<TTo> Project<TFrom, TTo>(this I2DSampler<TFrom> from, Func<TFrom, TTo> func)
	{
		return new Projection2D<TFrom, TTo>
		{
			From = from,
			Project = (sample, xz) => func(sample),
		};
	}

	public static I2DSampler<TTo> Project<TFrom, TTo>(this I2DSampler<TFrom> from, Func<TFrom, XZ, TTo> func)
	{
		return new Projection2D<TFrom, TTo>
		{
			From = from,
			Project = func,
		};
	}

	sealed class TranslatedArea : IArea
	{
		public required IArea Orig { get; init; }
		public required Rect Bounds { get; init; }
		public required XZ ReverseTranslation { get; init; }

		public bool InArea(XZ xz)
		{
			return Orig.InArea(xz.Add(ReverseTranslation));
		}
	}

	public static IArea Translate(this IArea area, XZ translation)
	{
		var bounds = new Rect(area.Bounds.start.Add(translation), area.Bounds.end.Add(translation));
		return new TranslatedArea
		{
			Orig = area,
			Bounds = bounds,
			ReverseTranslation = translation.Scale(-1),
		};
	}

	public static IArea AsArea(this I2DSampler<bool> sampler) => new SamplerArea { Sampler = sampler };

	class SamplerArea : IArea
	{
		public required I2DSampler<bool> Sampler { get; init; }
		public Rect Bounds => Sampler.Bounds;
		public bool InArea(XZ xz) => Sampler.Sample(xz);
	}
}
