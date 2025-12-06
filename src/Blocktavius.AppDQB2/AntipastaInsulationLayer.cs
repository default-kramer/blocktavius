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

public abstract class DerivedProp<TSelf, TOutput> : DerivedElement<TOutput>, INodeWithStaticPassInfo
	where TSelf : DerivedProp<TSelf, TOutput>
{
	public required IViewmodel Owner { get; init; }
	public sealed override INodeGroup NodeGroup => Owner;

	PassIndex INodeWithStaticPassInfo.PassIndex => passIndex;
	NodeIndex INodeWithStaticPassInfo.NodeIndex => nodeIndex;
	private static readonly PassIndex passIndex;
	private static readonly NodeIndex nodeIndex;
	static DerivedProp()
	{
		if (I.indexer.TryGetByImplementationType(typeof(TSelf), out var info))
		{
			passIndex = info.PassIndex;
			nodeIndex = info.NodeIndex;
		}
		else
		{
			throw new Exception("TODO");
		}
	}
}

public abstract class SettableDerivedProp<TSelf, TOutput> : SettableDerivedElement<TOutput>, INodeWithStaticPassInfo
	where TSelf : SettableDerivedProp<TSelf, TOutput>
{
	public required IViewmodel Owner { get; init; }
	public sealed override INodeGroup NodeGroup => Owner;

	PassIndex INodeWithStaticPassInfo.PassIndex => passIndex;
	NodeIndex INodeWithStaticPassInfo.NodeIndex => nodeIndex;
	private static readonly PassIndex passIndex;
	private static readonly NodeIndex nodeIndex;
	static SettableDerivedProp()
	{
		if (I.indexer.TryGetByImplementationType(typeof(TSelf), out var info))
		{
			passIndex = info.PassIndex;
			nodeIndex = info.NodeIndex;
		}
		else
		{
			throw new Exception("TODO");
		}
	}
}

public class OriginProp<TSelf, TOutput> : SettableDerivedProp<TSelf, TOutput>, INodeWithStaticPassInfo
	where TSelf : OriginProp<TSelf, TOutput>
{
	public required TOutput InitialValue { get; init; }

	protected override bool AcceptSetValueRequest(ref TOutput newValue) => true;
	protected override TOutput Recompute() => CachedValue ?? InitialValue;

	PassIndex INodeWithStaticPassInfo.PassIndex => passIndex;
	NodeIndex INodeWithStaticPassInfo.NodeIndex => nodeIndex;
	private static readonly PassIndex passIndex;
	private static readonly NodeIndex nodeIndex;
	static OriginProp()
	{
		if (I.indexer.TryGetByImplementationType(typeof(TSelf), out var info))
		{
			passIndex = info.PassIndex;
			nodeIndex = info.NodeIndex;
		}
		else
		{
			throw new Exception("TODO");
		}
	}
}

public abstract class AsyncDerivedProp<TSelf, TInput, TOutput> : AsyncDerivedElement<TSelf, TInput, TOutput>, INodeWithStaticPassInfo
	where TSelf : AsyncDerivedProp<TSelf, TInput, TOutput>, IAsyncComputation<TInput, TOutput>
	where TInput : IEquatable<TInput>
	where TOutput : class
{
	PassIndex INodeWithStaticPassInfo.PassIndex => passIndex;
	NodeIndex INodeWithStaticPassInfo.NodeIndex => nodeIndex;
	private static readonly PassIndex passIndex;
	private static readonly NodeIndex nodeIndex;
	static AsyncDerivedProp()
	{
		if (I.indexer.TryGetByImplementationType(typeof(TSelf), out var info))
		{
			passIndex = info.PassIndex;
			nodeIndex = info.NodeIndex;
		}
		else
		{
			throw new Exception("TODO");
		}
	}

	public required IViewmodel Owner { get; init; }
	public sealed override INodeGroup NodeGroup => Owner;
}

public interface ICommandNode : INode, ICommand
{
	public bool CanCurrentlyExecute { get; }
}

abstract class CommandNode<TSelf> : ICommandNode, INodeWithStaticPassInfo where TSelf : CommandNode<TSelf>
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


	#region "INodeWithStaticPassInfo"
	PassIndex INodeWithStaticPassInfo.PassIndex => passIndex;
	NodeIndex INodeWithStaticPassInfo.NodeIndex => nodeIndex;
	private static readonly PassIndex passIndex;
	private static readonly NodeIndex nodeIndex;
	static CommandNode()
	{
		if (I.indexer.TryGetByImplementationType(typeof(TSelf), out var info))
		{
			passIndex = info.PassIndex;
			nodeIndex = info.NodeIndex;
		}
		else
		{
			throw new Exception("TODO");
		}
	}
	#endregion
}
