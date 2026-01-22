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

	public ExpansionId Expand(IReadOnlyList<(XZ xz, T value)> expansion)
	{
		if (expansion.Count == 0)
		{
			return currentExpansionId;
		}

		currentExpansionId = currentExpansionId.Next();

		foreach (var item in expansion)
		{
			if (expansions.Sample(item.xz).ExpansionId.Value != ExpansionId.Nothing.Value)
			{
				throw new InvalidOperationException("Cannot overwrite previously expanded value");
			}
			expansions.Set(item.xz, new Entry { ExpansionId = currentExpansionId, Value = item.value });
		}

		// TODO connectivity check...

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
