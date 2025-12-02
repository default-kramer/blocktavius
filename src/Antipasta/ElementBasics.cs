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
}

public interface IElement<TOutput> : IUntypedElement
{
	TOutput Value { get; }
}

/// <summary>
/// TODO - I wonder if there's any value to making this generic?
/// Because this is really only intended for WPF data binding which will be
/// setting everything via reflection anyway.
/// </summary>
public interface ISettableElement<TOutput> : IElement<TOutput>
{
	PropagationResult AcceptSetValueRequest(IPropagationContext context, TOutput newValue);
}

public abstract class BaseNode : INode
{
	private readonly List<WeakReference<INode>> listeners = new();

	public abstract INodeGroup NodeGroup { get; }

	public void AddListener(INode listener)
	{
		listeners.Add(new WeakReference<INode>(listener));
	}

	public IEnumerable<INode> GetListeners()
	{
		foreach (var listener in listeners)
		{
			if (listener.TryGetTarget(out var target))
			{
				yield return target;
			}
			else
			{
				// TODO - should remove from list!
				// Would weak event subscription be better?
				// But we would still need some kind of collection, right?
				// On the other hand, the list of listeners should be small and static,
				// mirroring the relationships between the microtypes for each property...
			}
		}
	}

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
}

public abstract class SettableDerivedElement<TOutput> : DerivedElement<TOutput>, ISettableElement<TOutput>
{
	protected abstract bool AcceptSetValueRequest(ref TOutput newValue);

	PropagationResult ISettableElement<TOutput>.AcceptSetValueRequest(IPropagationContext context, TOutput newValue)
	{
		if (AcceptSetValueRequest(ref newValue))
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
		element.AddListener(this);
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
