using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

public interface ISettableElement<TOutput> : IElement<TOutput>, ISettableElementUntyped
{
	PropagationResult AcceptSetValueRequest(IPropagationContext context, TOutput newValue);
}
