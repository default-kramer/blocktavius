using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

/// <summary>
/// An area that can be expanded incrementally.
/// Call <see cref="Expand"/> to add more XZs to the area.
/// The returned <see cref="ExpansionId"/> can be used to access the state of the area
/// immediately after that expansion, ignoring changes from any future expansions.
///
/// The initial shell must not be a hole.
/// During expansion, it is possible that a hole will be created, but this class
/// promises that <see cref="CurrentShell"/> will never return a hole; it will always
/// find the outer shell after expansion.
///
/// One benefit of this class is faster shell computation compared to
/// <see cref="ShellLogic.ComputeShells"/>, which can be very slow if it is run
/// after every layer of a tall and/or large hill construction algorithm.
/// </summary>
sealed class ExpandableArea<T>
{
	readonly record struct Entry
	{
		public required T Value { get; init; }
		public required ExpansionId ExpansionId { get; init; }

		public static readonly Entry Nothing = new Entry { ExpansionId = ExpansionId.Zero, Value = default! };
	}

	private readonly Shell originalShell;
	private readonly MutableList2D<Entry> expansions;
	private ExpansionId currentExpansionId;
	private IReadOnlyList<ShellItem> currentShell;

	public ExpandableArea(Shell shell)
	{
		if (shell.IsHole)
		{
			throw new ArgumentException("Shell must not be a hole");
		}

		this.originalShell = shell;
		this.expansions = new MutableList2D<Entry>(Entry.Nothing, shell.IslandArea.Bounds);
		currentExpansionId = ExpansionId.Zero;
		currentShell = shell.ShellItems;
	}

	public IReadOnlyList<ShellItem> CurrentShell() => currentShell;

	/// <summary>
	/// The given <paramref name="expansion"/> must not be part of the original area or any
	/// previous expansion.
	/// </summary>
	public ExpansionId Expand(IEnumerable<(XZ xz, T value)> expansion)
	{
		if (!expansion.Any())
		{
			return currentExpansionId;
		}

		currentExpansionId = currentExpansionId.Next();
		var comparer = EqualityComparer<T>.Default;

		// Create the expansion with a temp ID, to be replaced during connectivity check
		var tempId = new ExpansionId { Value = -1 };
		Queue<XZ> notYetConnected = new();
		foreach (var item in expansion)
		{
			var exist = expansions.Sample(item.xz);
			if (exist.ExpansionId.Value == tempId.Value)
			{
				if (!comparer.Equals(item.value, exist.Value))
				{
					throw new ArgumentException($"conflicting values given for {item.xz}");
				}
			}
			else if (exist.ExpansionId.Value != ExpansionId.Zero.Value)
			{
				throw new InvalidOperationException("Cannot overwrite previously expanded value");
			}
			expansions.Set(item.xz, new Entry { ExpansionId = tempId, Value = item.value });
			notYetConnected.Enqueue(item.xz);
		}

		// Connectivity check; replace the temp ID with the correct ID when connected
		bool doAnotherPass = notYetConnected.Any();
		while (doAnotherPass)
		{
			doAnotherPass = false;
			var nextPass = new Queue<XZ>();
			while (notYetConnected.TryDequeue(out var xz))
			{
				bool isNowConnected = Direction.CardinalDirections().Any(dir =>
				{
					var neighbor = xz.Step(dir);
					return originalShell.IslandArea.InArea(neighbor)
						|| expansions.Sample(neighbor).ExpansionId.Value > 0;
				});

				if (isNowConnected)
				{
					// replace with correct ID
					var value = expansions.Sample(xz).Value;
					expansions.Set(xz, new Entry { ExpansionId = currentExpansionId, Value = value });
					doAnotherPass = true;
				}
				else
				{
					nextPass.Enqueue(xz);
				}
			}
			notYetConnected = nextPass;
		}

		if (notYetConnected.TryDequeue(out var disconnected))
		{
			throw new ArgumentException($"XZ ({disconnected.X},{disconnected.Z}) is not connected to the expanded area");
		}

		// update shell by walking another lap around the expanded area
		currentShell = ShellLogic.WalkShellFromPoint(GetArea(currentExpansionId), expansion.First().xz);

		return currentExpansionId;
	}

	public I2DSampler<bool> GetArea(ExpansionId expansionId) => GetSampler(expansionId, default!).Project(tuple => tuple.Item1);

	/// <summary>
	/// Returns a sampler which includes all expansions up to and including
	/// the given <paramref name="expansionId"/>, but not beyond.
	/// The <paramref name="originalAreaValue"/> defines that value that should be returned
	/// for XZs which were part of the original area.
	/// Other XZs will return the value that was provided during expansion.
	/// XZs which are not part of the area identified by the given <paramref name="expansionId"/>
	/// will return false for the first item of the tuple.
	/// </summary>
	public I2DSampler<(bool, T)> GetSampler(ExpansionId expansionId, T originalAreaValue)
	{
		if (expansionId.Value < ExpansionId.Zero.Value)
		{
			throw new ArgumentException("invalid expansion ID (negative)");
		}

		return new Sampler
		{
			CutoffExpansionId = expansionId,
			OriginalAreaValue = originalAreaValue,
			Area = this,
		};
	}

	sealed class Sampler : I2DSampler<(bool, T)>
	{
		public required ExpansionId CutoffExpansionId { get; init; }
		public required T OriginalAreaValue { get; init; }
		public required ExpandableArea<T> Area { get; init; }

		// This Bounds may be bigger than strictly necessary for this particular Expansion ID, but that's fine for now
		public Rect Bounds => Rect.Union(Area.originalShell.IslandArea.Bounds, Area.expansions.Bounds);

		public (bool, T) Sample(XZ xz)
		{
			if (Area.originalShell.IslandArea.Sample(xz))
			{
				return (true, OriginalAreaValue);
			}
			var entry = Area.expansions.Sample(xz);
			int id = entry.ExpansionId.Value;
			if (id > ExpansionId.Zero.Value && id <= CutoffExpansionId.Value)
			{
				return (true, entry.Value);
			}
			return (false, default!);
		}
	}
}
