using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

public sealed class ConstantSampler<T> : I2DSampler<T>
{
	public required Rect Bounds { get; init; }
	public required T Value { get; init; }

	public T Sample(XZ xz) => Value;
}
