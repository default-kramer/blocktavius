using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

/// <summary>
/// An "Element" is an <see cref="INode"/> that holds a value.
/// Some machinery in Antipasta.WPF will be able to surface elements as properties
/// for WPF data binding (probably via a custom type descriptor).
/// </summary>
public interface IElementUntyped : INode
{
	object? UntypedValue { get; }

	Type ElementType { get; }
}
