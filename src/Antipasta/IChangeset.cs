using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

/// <summary>
/// Caution: Requested changes may not be applied immediately!
/// </summary>
public interface IChangeset
{
	IChangeset RequestChangeUntyped(ISettableElementUntyped element, object? value);

	IChangeset RequestChange<T>(ISettableElement<T> element, T value);
}
