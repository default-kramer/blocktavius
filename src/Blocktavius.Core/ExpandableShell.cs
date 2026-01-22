using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

readonly struct ExpansionId
{
	public required int Value { get; init; }

	public ExpansionId Next() => new ExpansionId { Value = this.Value + 1 };

	public static readonly ExpansionId Nothing = new ExpansionId { Value = 0 };
}

sealed class ExpandableShell<T>
{
	readonly record struct Entry
	{
		public required T Value { get; init; }
		public required ExpansionId ExpansionId { get; init; }

		public static readonly Entry Nothing = new Entry { ExpansionId = ExpansionId.Nothing, Value = default! };
	}

	private readonly Shell originalShell;
	private readonly MutableList2D<Entry> expansions;
	private ExpansionId currentExpansionId;
	private IReadOnlyList<ShellItem> currentShell;

	public ExpandableShell(Shell shell)
	{
		this.originalShell = shell;
		this.expansions = new MutableList2D<Entry>(Entry.Nothing, shell.IslandArea.Bounds);
		currentExpansionId = ExpansionId.Nothing;
		currentShell = shell.ShellItems;
	}

	public IReadOnlyList<ShellItem> CurrentShell() => currentShell;

	public ExpansionId Expand(IEnumerable<(XZ xz, T value)> expansion)
	{
		if (!expansion.Any())
		{
			return currentExpansionId;
		}

		currentExpansionId = currentExpansionId.Next();

		// Create the expansion with a temp ID, to be replaced during connectivity check
		var tempId = new ExpansionId { Value = -1 };
		Queue<XZ> notYetConnected = new();
		foreach (var item in expansion)
		{
			if (expansions.Sample(item.xz).ExpansionId.Value != ExpansionId.Nothing.Value)
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

	public I2DSampler<(bool, T)> GetSampler(ExpansionId expansionId, T originalAreaValue)
	{
		return new Sampler
		{
			CutoffExpansionId = expansionId,
			OriginalAreaValue = originalAreaValue,
			ShellEx = this,
		};
	}

	sealed class Sampler : I2DSampler<(bool, T)>
	{
		public required ExpansionId CutoffExpansionId { get; init; }
		public required T OriginalAreaValue { get; init; }
		public required ExpandableShell<T> ShellEx { get; init; }

		// This Bounds may be bigger than strictly necessary for this particular Expansion ID, but that's fine for now
		public Rect Bounds => Rect.Union(ShellEx.originalShell.IslandArea.Bounds, ShellEx.expansions.Bounds);

		public (bool, T) Sample(XZ xz)
		{
			if (ShellEx.originalShell.IslandArea.Sample(xz))
			{
				return (true, OriginalAreaValue);
			}
			var entry = ShellEx.expansions.Sample(xz);
			int id = entry.ExpansionId.Value;
			if (id > ExpansionId.Nothing.Value && id <= CutoffExpansionId.Value)
			{
				return (true, entry.Value);
			}
			return (false, default!);
		}
	}
}
