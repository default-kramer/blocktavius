using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

public interface ISettableElementUntyped : IElementUntyped
{
	PropagationResult AcceptSetValueRequestUntyped(IPropagationContext context, object? newValue);
}
