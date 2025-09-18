using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

public static class Util
{
	public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> sequence)
	{
		return sequence.Where(x => x != null)!;
	}

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
		if (result.Bounds.start != XZ.Zero)
		{
			throw new Exception("assert fail! NOMERGE");
		}
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
}
