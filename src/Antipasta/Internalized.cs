using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

/// <summary>
/// Holds a value that user code is discouraged from attempting to access.
/// </summary>
public sealed class Internalized<T>
{
	internal T Value { get; }

	public Internalized(T value)
	{
		this.Value = value;
	}
}
