using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core.Generators.Hills;

public static class BUBBLER
{
	public sealed record Settings
	{
		public required int MinElevation { get; init; }
		public required int MaxElevation { get; init; }
		public required PRNG Prng { get; init; }
		public required XZ Where { get; init; }

		public int BubbleFactor { get; set; } = 3;
		public decimal MinBubbleChance { get; set; } = 0.2m;
		public int Smoothness { get; set; } = 2;
		public int Scale { get; set; } = 4;

		public bool Validate(out Settings validSettings)
		{
			validSettings = this;
			bool valid = true;

			if (MaxElevation < 1)
			{
				valid = false;
				validSettings = validSettings with { MaxElevation = 1 };
			}
			if (MinElevation > validSettings.MaxElevation)
			{
				valid = false;
				validSettings = validSettings with { MinElevation = validSettings.MaxElevation };
			}
			if (BubbleFactor < 1)
			{
				valid = false;
				validSettings = validSettings with { BubbleFactor = 1 };
			}
			if (MinBubbleChance < 0)
			{
				valid = false;
				validSettings = validSettings with { MinBubbleChance = 0 };
			}
			if (Scale < 1)
			{
				valid = false;
				validSettings = validSettings with { Scale = 1 };
			}
			return valid;
		}
	}

	public static I2DSampler<Elevation> Build(Settings settings)
	{
		if (!settings.Validate(out _))
		{
			throw new ArgumentException(nameof(settings));
		}

		var items = Bubble(settings.Prng, settings.MinElevation, settings.MaxElevation, settings.BubbleFactor, settings.MinBubbleChance);
		var array = Construct(items, settings.Scale);

		int smoothCounter = settings.Smoothness;
		while (smoothCounter-- > 0)
		{
			array = Smooth(array, settings.MinElevation);
		}

		var centralize = array.Bounds.Size.Unscale(new XZ(2, 2)).Scale(-1);
		return array.TranslateTo(settings.Where.Add(centralize));
	}

	/// <summary>
	/// Target hit count is maxElevation * factor.
	/// Start with just XZ(0,0) in the pool.
	/// Repeat until any XZ reaches target hit count:
	///   Choose a random XZ from the pool and remove it.
	///   Decide whether this XZ gets hit or not.
	///   If it does, increment its hit counter and add all 4 neighbors to the pool.
	///
	/// When done, the pool is no longer needed.
	/// The idea is that items near the center of mass will be duplicated in the pool
	/// more often than items near the outskirts.
	///
	/// The returned elevation is hitCount / factor for each XZ.
	/// </summary>
	private static List<(XZ xz, int elev)> Bubble(PRNG prng, int minElevation, int maxElevation, int factor, decimal minChanceDecimal)
	{
		List<XZ> pool = [XZ.Zero];
		Dictionary<XZ, int> hitCounter = new();

		int stopWhen = maxElevation * factor;
		int minChance = Math.Max(1, Convert.ToInt32(stopWhen * minChanceDecimal));

		bool done = false;
		while (!done)
		{
			var index = prng.NextInt32(pool.Count);
			var xz = pool[index];
			pool[index] = pool[^1];
			pool.RemoveAt(pool.Count - 1);

			int hits = hitCounter.GetValueOrDefault(xz, 0);
			int chance = Math.Max(minChance, stopWhen - hits);
			if (prng.NextInt32(stopWhen) <= chance)
			{
				hits++;
				hitCounter[xz] = hits;
				if (hits >= stopWhen)
				{
					done = true;
				}

				pool.Add(xz.Add(-1, 0));
				pool.Add(xz.Add(1, 0));
				pool.Add(xz.Add(0, -1));
				pool.Add(xz.Add(0, 1));
			}

			if (pool.Count == 0)
			{
				// This is extremely unlikely, maybe impossible, but better safe than sorry:
				pool.AddRange(hitCounter.Keys);
			}
		}

		return hitCounter.Select(kvp => (xz: kvp.Key, elev: Math.Min(maxElevation, kvp.Value / factor)))
			.Where(item => item.elev >= minElevation)
			.ToList();
	}

	private static MutableArray2D<Elevation> Construct(IReadOnlyList<(XZ xz, int elev)> include, int scale)
	{
		var unscaledBounds = Rect.GetBounds(include.Select(item => item.xz));
		var size = unscaledBounds.Size.Scale(scale);
		var bounds = new Rect(unscaledBounds.start, unscaledBounds.start.Add(size));
		var array = new MutableArray2D<Elevation>(bounds, new Elevation(-1));
		foreach (var item in include)
		{
			var point = item.xz.Subtract(unscaledBounds.start).Scale(scale).Add(unscaledBounds.start);
			var elev = new Elevation(item.elev);
			for (int z = 0; z < scale; z++)
			{
				for (int x = 0; x < scale; x++)
				{
					array.Put(point.Add(x, z), elev);
				}
			}
		}
		return array;
	}

	private static MutableArray2D<Elevation> Smooth(MutableArray2D<Elevation> array, int minElevation)
	{
		var smoothed = new MutableArray2D<Elevation>(array.Bounds, new Elevation(-1));
		foreach (var xz in array.Bounds.Enumerate())
		{
			(int count, int sum) = (0, 0);
			void look(XZ xz, int weight)
			{
				int sample = Math.Max(minElevation, array.Sample(xz).Y);
				count += weight;
				sum += sample * weight;
			}

			look(xz, 3);
			look(xz.Add(1, 0), 3);
			look(xz.Add(-1, 0), 3);
			look(xz.Add(0, 1), 3);
			look(xz.Add(0, -1), 3);
			look(xz.Add(-1, -1), 2);
			look(xz.Add(-1, 1), 2);
			look(xz.Add(1, -1), 2);
			look(xz.Add(1, 1), 2);
			if (count > 0)
			{
				smoothed.Put(xz, new Elevation(sum / count));
			}
		}
		return smoothed;
	}
}
