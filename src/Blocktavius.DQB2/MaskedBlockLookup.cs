using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

sealed class MaskedBlockLookup<T> : IReadOnlyDictionary<ushort, T?>
{
	private const int count = Block.CanonicalBlockCount;
	const ushort mask = Block.Mask_CanonicalBlockId;

	private readonly T?[] lookup = new T[count];

	public T? this[ushort key]
	{
		get => lookup[key & mask];
		set => lookup[key & mask] = value;
	}

	public IEnumerable<ushort> Keys => Enumerable.Range(0, count).Select(i => (ushort)i);
	public IEnumerable<T?> Values => lookup;
	public int Count => count;
	public bool ContainsKey(ushort key) => true;

	private IEnumerable<KeyValuePair<ushort, T?>> Enumerate()
	{
		for (ushort i = 0; i < count; i++)
		{
			yield return new KeyValuePair<ushort, T?>(i, lookup[i]);
		}
	}

	public IEnumerator<KeyValuePair<ushort, T?>> GetEnumerator() => Enumerate().GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => Enumerate().GetEnumerator();

	public bool TryGetValue(ushort key, out T? value)
	{
		value = this[key];
		return true;
	}
}
