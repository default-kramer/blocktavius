using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

/// <summary>
/// Ensures that `this[0]` starts with the minimum XZ in the ring to aid
/// in making algorithms deterministic.
/// Also implements methods that loop around the ring, including the indexer.
/// </summary>
sealed class ShellItemRing : IReadOnlyList<ShellItem>
{
	private readonly IReadOnlyList<ShellItem> items;
	private readonly int startIndex;

	public ShellItemRing(IReadOnlyList<ShellItem> items)
	{
		this.items = items;
		this.startIndex = GetStartIndex(items);
	}

	private static int GetStartIndex(IReadOnlyList<ShellItem> items)
	{
		if (items.Count == 0) { return 0; }

		var minItem = items.Index().MinBy(a => a.Item.XZ);
		// backup as long as the XZ matches (just in case the XZ wraps around the end of the ring)
		int backup = minItem.Index + items.Count;
		while (items[backup % items.Count].XZ == minItem.Item.XZ && backup >= 0)
		{
			backup--;
		}
		return (backup + 1) % items.Count; // undo the last backup
	}

	public ShellItem this[int index] => items[(startIndex + index) % items.Count];

	public int Count => items.Count;

	public IEnumerator<ShellItem> GetEnumerator() => Enumerate(skip: 0).GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

	private IEnumerable<ShellItem> Enumerate(int skip)
	{
		int startIndex = this.startIndex + skip;
		int count = items.Count;
		int end = startIndex + count;
		for (int i = startIndex; i < end; i++)
		{
			yield return items[i % count];
		}
	}

	public IEnumerable<ShellItem> OneLapFrom(int i) => Enumerate(skip: i % items.Count);

	public IEnumerable<ShellItem> InfiniteEnumeration(int skipCount)
	{
		int count = items.Count;
		int i = (startIndex + skipCount) % count;
		while (true)
		{
			yield return items[i];
			i = (i + 1) % count;
		}
	}
}
