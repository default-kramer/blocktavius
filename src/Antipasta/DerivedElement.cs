using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

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
