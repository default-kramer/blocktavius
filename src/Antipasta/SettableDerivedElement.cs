using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

public abstract class SettableDerivedElement<TOutput> : DerivedElement<TOutput>, ISettableElement<TOutput>
{
	/// <remarks>
	/// There seems to be no good compile-time solution for testing whether TOutput accepts null.
	/// And it's too hard to perform *reliable* nullability analysis at runtime.
	/// Perhaps "explicit is better than implicit" here anyway...
	/// </remarks>
	protected abstract bool AcceptsNull(out TOutput nullValue);

	protected abstract bool AcceptSetValueRequest(IPropagationContext context, ref TOutput value);

	PropagationResult ISettableElement<TOutput>.AcceptSetValueRequest(IPropagationContext context, TOutput requestedValue)
	{
		if (AcceptSetValueRequest(context, ref requestedValue))
		{
			return this.PROTECTED_UpdateValue(requestedValue);
		}
		return PropagationResult.None;
	}

	PropagationResult ISettableElementUntyped.AcceptSetValueRequestUntyped(IPropagationContext context, object? newValue)
	{
		ISettableElement<TOutput> me = this;
		if (newValue is TOutput val)
		{
			return me.AcceptSetValueRequest(context, val);
		}
		else if (newValue == null && AcceptsNull(out var nullValue))
		{
			return me.AcceptSetValueRequest(context, nullValue);
		}
		return OnInvalidSetValueRequest(newValue);
	}

	protected virtual PropagationResult OnInvalidSetValueRequest(object? requestedValue)
	{
		string need = typeof(TOutput).FullName ?? typeof(TOutput).Name;
		string got = requestedValue?.GetType()?.FullName ?? requestedValue?.GetType()?.Name ?? "null";
		throw new InvalidOperationException($"Cannot set element's value, {need} is not assignable from {got}");
	}
}
