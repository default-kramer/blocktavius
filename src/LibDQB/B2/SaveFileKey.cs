using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibDQB.B2;

/// <summary>
/// Each STGDAT file has a key that must match the key in its CMNDAT file.
/// If the keys don't match the game will not load it.
/// </summary>
/// <remarks>
/// The game generates a new, apparently-random key every time you save.
/// </remarks>
public readonly record struct SaveFileKey
{
	public uint Value { get; }

	public SaveFileKey(uint value)
	{
		this.Value = value;
	}
}
