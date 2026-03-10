using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibDQB.B2;

public readonly record struct IslandId
{
	public byte Value { get; }

	public IslandId(byte value)
	{
		this.Value = value;
	}

	// TODO - add constants for known values
}
