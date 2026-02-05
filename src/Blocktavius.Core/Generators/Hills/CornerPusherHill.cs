using System;
using System.Collections.Generic;
using System.Linq;

namespace Blocktavius.Core.Generators.Hills;

public static class CornerPusherHill
{
	public sealed record Settings
	{
		public required PRNG Prng { get; init; }
		public required int MinElevation { get; init; }
		public required int MaxElevation { get; init; }

		/// <summary>
		/// Larger values produce steeper hills.
		/// </summary>
		public int MaxConsecutiveMisses { get; init; } = 11;

		/// <summary>
		/// This plus <see cref="MaxInitialMissPercent"/> defines the range for the
		/// randomized initial miss count that will be assigned to each initial shell XZ.
		/// Value is clamped to the range [0, 1].
		/// Percentage is relative to <see cref="MaxConsecutiveMisses"/>.
		/// Without this, the first few layers will be boring until the miss counts increase enough.
		/// </summary>
		/// <remarks>
		/// A negative value preserves some legacy behavior.
		/// </remarks>
		public decimal MinInitialMissPercent { get; init; } = 0.45m;

		/// <summary>
		/// See <see cref="MinInitialMissPercent"/>.
		/// </summary>
		public decimal MaxInitialMissPercent { get; init; } = 1.0m;
	}

	/// <summary>
	/// Builds a hill using integer elevations.
	/// </summary>
	public static I2DSampler<int> BuildHill(Settings settings, Shell shell)
	{
		if (shell.IsHole)
		{
			throw new ArgumentException("Shell must not be a hole");
		}

		var area = new ExpandableArea<int>(shell);
		BuildHill(settings, area, i => i);

		return area.GetSampler(ExpansionId.MaxValue, settings.MaxElevation)
			.Project(tuple => tuple.Item1 ? tuple.Item2 : -1);
	}

	/// <summary>
	/// Generic implementation for building a hill, converting integer elevations to type <typeparamref name="T"/>.
	/// </summary>
	internal static void BuildHill<T>(Settings settings, ExpandableArea<T> area, Func<int, T> converter)
	{
		// This dictionary tracks items on the shell that are candidates for being "pushed" out.
		var pendingExpansion = new Dictionary<XZ, Layer.PendingShellItem>();

		// Initialize the first layer and its miss counts.
		InitializeFirstLayer(settings, area.CurrentShell(), pendingExpansion);

		// Iteratively build each layer from the top down.
		int numLayers = settings.MaxElevation - settings.MinElevation;
		var ring = new ShellItemRing(area.CurrentShell());
		for (int i = 0; i < numLayers; i++)
		{
			int currentElevation = settings.MaxElevation - i;
			var layer = new Layer(ring, pendingExpansion, settings);

			// Calculate and apply the expansion for the current layer.
			var expansion = layer.CalculateExpansion();
			if (expansion.Count > 0)
			{
				area.Expand(expansion.Select(e => (e.XZ, converter(e.Elevation))));
				foreach (var (xz, _) in expansion)
				{
					pendingExpansion.Remove(xz);
				}
			}

			// Update miss counts for the items that will carry over to the next layer.
			ring = new ShellItemRing(area.CurrentShell());
			UpdateMisses(pendingExpansion, ring, currentElevation - 1);
		}

		// Twist ending? The "pending" expansion is actually expansion we already committed to.
		// This was mostly so that the miss count dictionary and the shell items line up nicely,
		// but may also be a consequence of preserving legacy behavior.
		var finalExpansion = pendingExpansion.Select(kvp => (kvp.Key, converter(kvp.Value.Elevation)));
		area.Expand(finalExpansion);
	}

	private static void InitializeFirstLayer(Settings settings, IReadOnlyList<ShellItem> initialShellItems, Dictionary<XZ, Layer.PendingShellItem> pendingExpansion)
	{
		UpdateMisses(pendingExpansion, new ShellItemRing(initialShellItems), settings.MaxElevation);

		// Overwrite legacy initial miss counts if percentage range is valid.
		decimal minPercent = Math.Clamp(settings.MinInitialMissPercent, 0m, 1m);
		decimal maxPercent = Math.Clamp(settings.MaxInitialMissPercent, 0m, 1m);
		int minMisses = Convert.ToInt32(minPercent * settings.MaxConsecutiveMisses);
		int maxMisses = Convert.ToInt32(maxPercent * settings.MaxConsecutiveMisses);

		bool reseed = (minMisses > 0 || maxMisses > 0)
			&& maxMisses >= minMisses
			&& settings.MinInitialMissPercent >= 0
			&& settings.MaxInitialMissPercent >= 0;

		if (reseed)
		{
			var prng = settings.Prng.Clone();
			foreach (var kvp in pendingExpansion)
			{
				kvp.Value.MissCount = prng.NextInt32(minMisses, maxMisses + 1);
			}
		}
	}

	private static void UpdateMisses(Dictionary<XZ, Layer.PendingShellItem> pendingItems, IEnumerable<ShellItem> shellItems, int elevation)
	{
		// reduce inside corners weight from 3:1 down to 2:1
		foreach (var item in shellItems.Where(i => i.CornerType != CornerType.Inside))
		{
			if (pendingItems.TryGetValue(item.XZ, out var pendingItem))
			{
				pendingItem.MissCount++;
			}
			else
			{
				pendingItems[item.XZ] = new Layer.PendingShellItem { MissCount = 1, Elevation = elevation };
			}
		}
	}

	/// <summary>
	/// Represents the logic for calculating the expansion of a single elevation layer.
	/// </summary>
	private sealed class Layer
	{
		public sealed class PendingShellItem
		{
			public int MissCount { get; set; } // mutable

			/// <summary>
			/// We retain the elevation this item was *introduced* at to maintain
			/// compatability with a previous version of this algorithm.
			/// (Also because I can't figure out why it gets more jagged when we break compatability.)
			/// </summary>
			public int Elevation { get; init; }
		}

		// Constants for run length calculations
		// Should convert these to Settings and experiment...
		private const int MinPushRunLength = 2;
		private const int MaxPushRunLength = 7;
		private const int MinSkipRunLength = 2;
		private const int MaxSkipRunLengthDivisor = 2;
		private const int MaxSkipRunLengthCap = 50;

		private readonly ShellItemRing shellRing;
		private readonly Dictionary<XZ, PendingShellItem> pendingExpansion;
		private readonly Settings settings;
		private readonly PRNG prng;

		public Layer(ShellItemRing shell, Dictionary<XZ, PendingShellItem> pendingExpansion, Settings settings)
		{
			this.shellRing = shell;
			this.pendingExpansion = pendingExpansion;
			this.settings = settings;
			this.prng = settings.Prng;
		}

		/// <summary>
		/// Calculates the list of shell items to be pushed out for this layer.
		/// </summary>
		public List<(XZ XZ, int Elevation)> CalculateExpansion()
		{
			var expansion = new List<(XZ, int)>();
			if (shellRing.Count == 0)
			{
				return expansion;
			}

			// Starting from a random point on the shell, do one lap, alternating between
			// pushing and skipping runs of shell items.
			int startIndex = prng.NextInt32(shellRing.Count);
			int endIndex = startIndex + shellRing.Count;
			int currentIndex = startIndex;
			bool push = false;

			do
			{
				push = !push;
				int runLength;

				if (push)
				{
					runLength = CalculatePushRunLength(currentIndex);
					for (int i = 0; i < runLength; i++)
					{
						var item = shellRing[currentIndex];
						currentIndex++;
						int elev = pendingExpansion[item.XZ].Elevation;
						expansion.Add((item.XZ, elev));
					}
				}
				else
				{
					runLength = CalculateSkipRunLength(currentIndex);
					currentIndex += runLength;
				}
			}
			while (currentIndex < endIndex);

			return expansion;
		}

		private int CalculatePushRunLength(int currentIndex)
		{
			int maxConsecutiveMisses = settings.MaxConsecutiveMisses;

			// Determine how many items *must* be pushed because they have exceeded their miss count.
			int mustTakeNow = shellRing.OneLapFrom(currentIndex)
				.TakeWhile(i => pendingExpansion[i.XZ].MissCount >= maxConsecutiveMisses)
				.Take(MaxPushRunLength)
				.Count();

			int minRun = Math.Max(mustTakeNow, MinPushRunLength);
			int maxRun = Math.Max(mustTakeNow, MaxPushRunLength);

			return prng.NextInt32(minRun, 1 + maxRun);
		}

		private int CalculateSkipRunLength(int currentIndex)
		{
			int maxConsecutiveMisses = settings.MaxConsecutiveMisses;
			int maxRunLength = Math.Min(shellRing.Count / MaxSkipRunLengthDivisor, MaxSkipRunLengthCap);

			// Determine how many items we can skip before hitting one that *must* be pushed.
			int mustStopAt = shellRing.OneLapFrom(currentIndex)
				.TakeWhile(i => pendingExpansion[i.XZ].MissCount < maxConsecutiveMisses)
				.Take(maxRunLength)
				.Count();

			if (mustStopAt < MinSkipRunLength)
			{
				return mustStopAt;
			}

			return prng.NextInt32(MinSkipRunLength, 1 + Math.Min(mustStopAt, maxRunLength));
		}
	}
}
