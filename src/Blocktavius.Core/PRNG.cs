using System;

namespace Blocktavius.Core;

/// <summary>
/// A deterministic pseudorandom number generator.
/// Starting from the same seed will always produce the same pseudorandom sequence.
/// </summary>
/// <remarks>
/// Based on the Java and C code by Sebastiano Vigna: https://github.com/vigna/MRG32k3a,
/// which is called "the reference implementation" in this file.
///
/// Original paper by Pierre L'Ecuyer: https://pubsonline.informs.org/doi/abs/10.1287/opre.47.1.159
/// </remarks>
public sealed class PRNG
{
	const long m1 = 4294967087L;
	const long m2 = 4294944443L;
	const long a12 = 1403580L;
	const long a13 = 810728L;
	const long a21 = 527612L;
	const long a23 = 1370589L;
	const long corr1 = (m1 * a13);
	const long corr2 = (m2 * a23);
	const double norm = 2.328306549295727688e-10;

	private long s10, s11, s12, s20, s21, s22;

	private void SetState(State state)
	{
		s10 = state.s10;
		s11 = state.s11;
		s12 = state.s12;
		s20 = state.s20;
		s21 = state.s21;
		s22 = state.s22;
	}

	private static void EnsureLessThan(long n, long limit, string name)
	{
		if (n >= limit)
		{
			throw new ArgumentException(string.Format(
				"{0} must be smaller than {1} (given: {2})", name, limit, n));
		}
	}

	public PRNG(State state)
	{
		SetState(state);
	}

	private PRNG(PRNG other)
	{
		this.s10 = other.s10;
		this.s11 = other.s11;
		this.s12 = other.s12;
		this.s20 = other.s20;
		this.s21 = other.s21;
		this.s22 = other.s22;
	}

	public PRNG Clone()
	{
		return new PRNG(this);
	}

	/// <summary>
	/// Mutates the current PRNG and then clones it.
	/// The idea is here is: imagine you have some two-step process and both steps
	/// need a PRNG. Also imagine that the user has locked the PRNG seed to something
	/// they like. If our code looks like this:
	///    doFirstStep(prng)
	///    doSecondStep(prng)
	/// then if the user changes some setting that affects the first step only,
	/// the prng will probably be in a different state when it reaches the second step.
	/// But if we do something like this instead:
	///    doFirstStep(prng.AdvanceAndClone())
	///    doSecondStep(prng.AdvanceAndClone())
	/// if the user changes some setting that affects the first step only, the second
	/// step will receive the same prng state as before, which increases the likelihood
	/// that the user remains satisfied with the seed they locked in earlier.
	///
	/// "So then why advance it at all? Why not just clone it?" The risk is that the
	/// two steps might start by doing something similar. For example, if both steps
	/// start by generating a Jaunt, even if they are different lengths the longer one
	/// will start the same as the shorter one. This offends my sense of taste,
	/// even if such duplication might take a careful eye to notice.
	/// </summary>
	public PRNG AdvanceAndClone()
	{
		NextDouble();
		return Clone();
	}

	public static PRNG Deserialize(string seed) => new PRNG(State.Deserialize(seed));

	/// <summary>
	/// Caller must ensure the thread-safety of the given <paramref name="seeder"/>.
	/// </summary>
	public static PRNG Create(Random seeder)
	{
		return new PRNG(RandomSeed(seeder));
	}

	/// <summary>
	/// Caller must ensure the thread-safety of the given <paramref name="seeder"/>.
	/// </summary>
	public static PRNG.State RandomSeed(Random seeder)
	{
		try
		{
			return new State(GetSeed(seeder, m1), GetSeed(seeder, m1), GetSeed(seeder, m1), GetSeed(seeder, m2), GetSeed(seeder, m2), GetSeed(seeder, m2));
		}
		catch (ArgumentException)
		{
			// This exception should be ****Extremely**** unlikely (getting s10,s11,s12 all zero),
			// but it is still technically possible
			return RandomSeed(seeder);
		}
	}

	private static long GetSeed(Random seeder, long limit)
	{
		return (long)(seeder.NextDouble() * (limit - 1));
	}

	/// <returns>A pseudorandom double N such that 0 &lt; N &lt; 1</returns>
	public double NextDouble()
	{
		/* Combination */
		long r = s12 - s22;
		r -= m1 * ((r - 1) >> 63);

		/* Component 1 */
		long p = (a12 * s11 - a13 * s10 + corr1) % m1;
		s10 = s11;
		s11 = s12;
		s12 = p;

		/* Component 2 */
		p = (a21 * s22 - a23 * s20 + corr2) % m2;
		s20 = s21;
		s21 = s22;
		s22 = p;

		// Warning - I am not 100% sure that 0 < N < 1, but
		// 1. The reference implementation says "Returns the next pseudorandom double in (0..1)"
		//    and I am choosing to trust that this is correct.
		// 2. I haven't seen a counterexample yet!
		return r * norm;
	}

	/// <returns>A pseudorandom int N such that 0 &lt;= N &lt; <paramref name="maxValue"/></returns>
	public int NextInt32(int maxValue)
	{
		if (maxValue < 1)
		{
			throw new ArgumentException("maxValue must be greater than zero");
		}

		return (int)(NextDouble() * maxValue);
	}

	public int NextInt32(int start, int end)
	{
		return start + NextInt32(end - start);
	}

	public bool NextBool() => NextInt32(2) == 0;

	public T RandomChoice<T>(params T[] items)
	{
		int index = NextInt32(items.Length);
		return items[index];
	}

	public void Shuffle<T>(List<T> list)
	{
		int n = list.Count;
		for (int i = n - 1; i > 0; i--)
		{
			int j = NextInt32(i + 1);
			(list[j], list[i]) = (list[i], list[j]);
		}
	}

	public string Serialize()
	{
		return DoSerialize(s10, s11, s12, s20, s21, s22);
	}

	private static string DoSerialize(long s10, long s11, long s12, long s20, long s21, long s22)
	{
		return string.Format("{0}-{1}-{2}-{3}-{4}-{5}", s10, s11, s12, s20, s21, s22);
	}

	public readonly struct State
	{
		public readonly long s10;
		public readonly long s11;
		public readonly long s12;
		public readonly long s20;
		public readonly long s21;
		public readonly long s22;

		public State(long s10, long s11, long s12, long s20, long s21, long s22)
		{
			this.s10 = s10;
			this.s11 = s11;
			this.s12 = s12;
			this.s20 = s20;
			this.s21 = s21;
			this.s22 = s22;

			if (s10 == 0 && s11 == 0 && s12 == 0)
			{
				throw new ArgumentException("s10, s11 and s12 cannot be all zero");
			}
			if (s20 == 0 && s21 == 0 && s22 == 0)
			{
				throw new ArgumentException("s20, s21 and s22 cannot be all zero");
			}
			EnsureNonnegative(s20, nameof(s20));
			EnsureNonnegative(s21, nameof(s21));
			EnsureNonnegative(s22, nameof(s22));
			EnsureLessThan(s10, m1, nameof(s10));
			EnsureLessThan(s11, m1, nameof(s11));
			EnsureLessThan(s12, m1, nameof(s12));
			EnsureLessThan(s20, m2, nameof(s20));
			EnsureLessThan(s21, m2, nameof(s21));
			EnsureLessThan(s22, m2, nameof(s22));
		}

		public string Serialize()
		{
			return DoSerialize(s10, s11, s12, s20, s21, s22);
		}

		public static State Deserialize(string item)
		{
			string[] parts = item.Split('-');
			if (parts.Length != 6)
			{
				throw new ArgumentException("invalid State string: " + item);
			}
			long s10 = long.Parse(parts[0]);
			long s11 = long.Parse(parts[1]);
			long s12 = long.Parse(parts[2]);
			long s20 = long.Parse(parts[3]);
			long s21 = long.Parse(parts[4]);
			long s22 = long.Parse(parts[5]);
			return new State(s10, s11, s12, s20, s21, s22);
		}

		private static void EnsureNonnegative(long n, string name)
		{
			if (n < 0)
			{
				throw new ArgumentException(string.Format("{0} must be >= 0 (given: {1})", name, n));
			}
		}
	}
}
