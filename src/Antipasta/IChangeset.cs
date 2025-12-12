using Antipasta.Scheduling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

/// <summary>
/// Requested changes are not applied until <see cref="ApplyChanges"/> is called.
/// </summary>
public interface IChangeset
{
	IChangeset RequestChangeUntyped(ISettableElementUntyped element, object? value);

	IChangeset RequestChange<T>(ISettableElement<T> element, T value);

	void ApplyChanges();
}
