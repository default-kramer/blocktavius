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
public interface IUntypedElement : INode
{
	object? UntypedValue { get; }

	Type ElementType { get; }
}

public interface IAsyncElement : IUntypedElement { }

public interface IElement<TOutput> : IUntypedElement
{
	TOutput Value { get; }
}

public interface IUntypedSettableElement : IUntypedElement
{

}

/// <summary>
/// TODO - I wonder if there's any value to making this generic?
/// Because this is really only intended for WPF data binding which will be
/// setting everything via reflection anyway.
/// </summary>
public interface ISettableElement<TOutput> : IElement<TOutput>, IUntypedSettableElement
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

abstract class SourceElement<TOutput> : BaseNode, IElement<TOutput>
{
	private TOutput value;
	protected SourceElement(TOutput initialValue)
	{
		this.value = initialValue;
	}

	public TOutput Value => value;

	object? IUntypedElement.UntypedValue => Value;
	Type IUntypedElement.ElementType => typeof(TOutput);
}

public abstract class SettableDerivedElement<TOutput> : DerivedElement<TOutput>, ISettableElement<TOutput>
{
	protected abstract bool AcceptSetValueRequest(IPropagationContext context, ref TOutput newValue);

	PropagationResult ISettableElement<TOutput>.AcceptSetValueRequest(IPropagationContext context, TOutput newValue)
	{
		if (AcceptSetValueRequest(context, ref newValue))
		{
			return this.PROTECTED_UpdateValue(newValue);
		}
		return PropagationResult.None;
	}
}

public abstract class DerivedElement<TOutput> : BaseNode, IElement<TOutput>
{
	private (int changeCounter, TOutput output)? cache = null;

	protected TElement ListenTo<TElement>(TElement element) where TElement : IUntypedElement
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

	object? IUntypedElement.UntypedValue => Value;
	Type IUntypedElement.ElementType => typeof(TOutput);

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
