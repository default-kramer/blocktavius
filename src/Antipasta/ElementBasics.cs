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

public interface IAsyncElement : IElementUntyped { }

public interface IElement<TOutput> : IElementUntyped
{
	TOutput Value { get; }
}

public interface ISettableElementUntyped : IElementUntyped
{
	PropagationResult AcceptSetValueRequestUntyped(IPropagationContext context, object? newValue);
}

public interface ISettableElement<TOutput> : IElement<TOutput>, ISettableElementUntyped
{
	PropagationResult AcceptSetValueRequest(IPropagationContext context, TOutput newValue);
}

public abstract class BaseNode : INode
{
	private readonly GraphManager graphManager = new();
	GraphManager INode.GraphManager => graphManager;

	public virtual GraphConnectionStatus GraphConnectionStatus => GraphConnectionStatus.Connected;

	public abstract INodeGroup NodeGroup { get; }

	public abstract PropagationResult OnPropagation(IPropagationContext context);
}

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

public abstract class DerivedElement<TOutput> : BaseNode, IElement<TOutput>
{
	private (int changeCounter, TOutput output)? cache = null;

	protected TElement ListenTo<TElement>(TElement element) where TElement : IElementUntyped
	{
		element.GraphManager.AddListener(this);
		return element;
	}

	protected TOutput? CachedValue => cache.GetValueOrDefault().output;

	public TOutput Value
	{
		get
		{
			if (!cache.HasValue)
			{
				cache = (0, Recompute());
			}
			return cache.Value.output;
		}
	}

	object? IElementUntyped.UntypedValue => Value;
	Type IElementUntyped.ElementType => typeof(TOutput);

	protected abstract TOutput Recompute();

	protected virtual bool RecomputeIfStale(IPropagationContext changes, out TOutput newValue)
	{
		newValue = Recompute();
		return true;
	}

	public sealed override PropagationResult OnPropagation(IPropagationContext context)
	{
		if (RecomputeIfStale(context, out var newValue))
		{
			return this.PROTECTED_UpdateValue(newValue);
		}
		return PropagationResult.None;
	}

	/// <summary>
	/// Should only be called as if it were a protected method.
	/// </summary>
	internal PropagationResult PROTECTED_UpdateValue(TOutput newValue)
	{
		var old = cache.GetValueOrDefault();
		if (ShouldBeConsideredUnchanged(old.output, newValue))
		{
			return PropagationResult.None;
		}
		else
		{
			cache = (old.changeCounter + 1, newValue);
			return PropagationResult.Changed;
		}
	}

	protected virtual bool ShouldBeConsideredUnchanged(TOutput? oldVal, TOutput newVal)
	{
		return object.ReferenceEquals(oldVal, newVal);
	}
}
