using Antipasta;
using Antipasta.IndexedPropagation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Blocktavius.AppDQB2;

public interface IProperty<TOutput> : IElement<TOutput> { }

public interface IViewmodel : INodeGroup { }

// TODO remove all unnecessary TSelf here?

public abstract class DerivedProp<TSelf, TOutput> : DerivedElement<TOutput>
	where TSelf : DerivedProp<TSelf, TOutput>
{
	public required IViewmodel Owner { get; init; }
	public sealed override INodeGroup NodeGroup => Owner;
}

public abstract class SettableDerivedProp<TSelf, TOutput> : SettableDerivedElement<TOutput>
	where TSelf : SettableDerivedProp<TSelf, TOutput>
{
	public required IViewmodel Owner { get; init; }
	public sealed override INodeGroup NodeGroup => Owner;
}

public class OriginProp<TSelf, TOutput> : SettableDerivedProp<TSelf, TOutput>
	where TSelf : OriginProp<TSelf, TOutput>
{
	public required TOutput InitialValue { get; init; }

	protected override bool AcceptSetValueRequest(ref TOutput newValue) => true;
	protected override TOutput Recompute() => CachedValue ?? InitialValue;
}

public abstract class AsyncDerivedProp<TSelf, TInput, TOutput> : AsyncDerivedElement<TSelf, TInput, TOutput>
	where TSelf : AsyncDerivedProp<TSelf, TInput, TOutput>, IAsyncComputation<TInput, TOutput>
	where TInput : IEquatable<TInput>
	where TOutput : class
{
	public required IViewmodel Owner { get; init; }
	public sealed override INodeGroup NodeGroup => Owner;
}

public interface ICommandNode : INode, ICommand
{
	public bool CanCurrentlyExecute { get; }
}

abstract class CommandNode : ICommandNode
{
	private readonly GraphManager graphManager = new();
	GraphConnectionStatus INode.GraphConnectionStatus => GraphConnectionStatus.Connected;
	GraphManager INode.GraphManager => graphManager;
	INodeGroup INode.NodeGroup => Owner;

	public required IViewmodel Owner { get; init; }

	public event EventHandler? CanExecuteChanged;

	public bool CanCurrentlyExecute => RefreshCanExecute().canExecute;

	private (bool canExecute, Action executionThunk)? cache = null;
	private (bool canExecute, bool changed, Action executionThunk) RefreshCanExecute()
	{
		Action? freshThunk = CanExecute();
		bool freshCanExecute = freshThunk != null;
		bool changed = (cache?.canExecute).GetValueOrDefault(!freshCanExecute) != freshCanExecute;

		Action thunk = freshThunk ?? OnInvalidExecutionAttempt;
		cache = (freshCanExecute, thunk);
		if (changed)
		{
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
		return (freshCanExecute, changed, thunk);
	}

	PropagationResult INode.OnPropagation(IPropagationContext context)
	{
		if (RefreshCanExecute().changed)
		{
			return PropagationResult.Changed;
		}
		return PropagationResult.None;
	}

	bool ICommand.CanExecute(object? parameter) => RefreshCanExecute().canExecute;

	void ICommand.Execute(object? parameter) => RefreshCanExecute().executionThunk();

	/// <summary>
	/// This method is assumed to run quickly.
	/// A null return value indicates "cannot execute" status.
	/// A non-null return value indicates "can execute" status and the
	/// thunk will be invoked when ICommand.Execute(...) is called.
	/// </summary>
	protected abstract Action? CanExecute();

	/// <summary>
	/// Called when someone calls ICommand.Execute(...) when CanExecute(...) returned false.
	/// </summary>
	protected virtual void OnInvalidExecutionAttempt()
	{
		throw new InvalidOperationException($"CanExecute is false, should not call Execute.");
	}

	protected TNode ListenTo<TNode>(TNode node) where TNode : INode
	{
		node.GraphManager.AddListener(this);
		return node;
	}
}
