using System.Collections.Immutable;

namespace Antipasta;

// "Antipasta" - I'm almost sure this will be the solution to spaghetti viewmodels.
// Create a dedicated class for every property!
// Accept only those properties on which you depend via the constructor.
// Will want support for:
// * Simple derived properties
// * Async derived properties
// * Settable derived properties
// * Settable source properties

public enum PropagationResult
{
	/// <summary>
	/// Do not propagate to listeners, probably because this node was unchanged.
	/// </summary>
	None,

	/// <summary>
	/// The value of this node changed and propagation should include all listeners.
	/// </summary>
	Changed,

	/// <summary>
	/// An async operation was started, but there is no change that listeners could observe yet.
	/// </summary>
	AsyncNone,
}

/// <summary>
/// A node typically represents a property.
/// For example, if a property FullName depends on another property FirstName,
/// we say that FullName "listens" to FirstName.
/// This allows FullName to recompute its value when FirstName changes.
/// These listener relationships create a directed graph through which property changes can propagate.
/// </summary>
/// <remarks>
/// The graph should be a DAG; if a cycle exists it will cause an error at runtime.
/// It is recommended that user code only create listener relationships in constructors
/// which would make accidental cycle creation impossible.
/// </remarks>
public interface INode
{
	GraphConnectionStatus GraphConnectionStatus { get; }

	/// <summary>
	/// Each node should create its own instance.
	/// </summary>
	GraphManager GraphManager { get; }

	PropagationResult OnPropagation(IPropagationContext context);

	/// <summary>
	/// Gets the group to which this node belongs. This relationship creates a **partition** of the
	/// dependency graph, allowing for dependencies to be managed hierarchically.
	///
	/// By collapsing all nodes within the same group into a single "supernode," we form a
	/// **quotient graph** where each vertex represents a `NodeGroup`. Because the underlying node
	/// graph is a Directed Acyclic Graph (DAG), this quotient graph of groups is also a DAG.
	/// This enables a hierarchical change propagation strategy.
	///
	/// This hierarchy is useful for imposing rules on dependencies. For example, nodes in a "child"
	/// group (like a `CartItem` ViewModel) can listen to nodes in a "parent" group (`Cart` ViewModel),
	/// but aggregations in the parent should not listen directly to numerous children. Instead, the
	/// parent can use hooks like <see cref="INodeGroup.OnChildrenFullyResolved"/> to react to changes.
	///
	/// TODO - I think this comment is not true. Imagine this DAG of nodes:
	///    Cart.DiscountCode -> CartItem.Price -> Cart.TotalPrice
	/// The resulting graph of groups would be
	///    Cart -> CartItem -> Cart
	/// Which we might consider to have a cycle because we return to Cart... but do we have to call this a cycle??
	/// Is it really a problem if we return to a previous owner as long as the set of properties to be
	/// considered is distinct from any previous visits to that same owner?
	/// At the very least I would have to rethink the <see cref="INodeGroup.OnSelfResolved"/> and
	/// <see cref="INodeGroup.OnChildrenFullyResolved"/> callbacks...
	/// </summary>
	INodeGroup NodeGroup { get; }
}

public interface IImmediateNotifyNode
{
	string PropertyName { get; }
}

public interface INodeGroup
{
	void OnSelfResolved(IPropagationContext context);

	void OnChildrenFullyResolved(IPropagationContext context);

	void OnChanged(IImmediateNotifyNode node);
}

/// <remarks>
/// NOTE - This interface is readonly by design.
/// The plan is that nodes should report their <see cref="PropagationResult"/>
/// and then the machinery will use <see cref="GraphManager.GetListeners"/> to merge
/// all those listeners into the propagation queue.
/// </remarks>
public interface IPropagationContext
{
	INodeGroup NodeGroup { get; }

	IImmutableStack<IPropagationContext> ParentContexts { get; }

	IAsyncScheduler AsyncScheduler { get; }
}

public interface IUnblocker
{
	void Unblock();
}

public enum SpinwaitResult
{
	Unknown,
	Unblocked,
	Timeout,
	SpinwaitNotSupported,
}

public interface IWaitableUnblocker : IUnblocker
{
	SpinwaitResult Spinwait(TimeSpan? timeout);
}

public interface IAsyncScheduler
{
	IWaitableUnblocker CreateUnblocker(); // UI thread

	ITaskWrapper RunTask(Task task, CancellationTokenSource cts); // UI thread

	void RunUnblockedContinuation(Action continuation); // any thread

	void DispatchProgress(IAsyncProgress progress); // background thread
}

public interface IAsyncProgress
{
	INode SourceNode { get; }

	// TODO - this is not quite right... what we really need is something that can
	// remembers what propagation strategy should be used, accepts the IAsyncProgress,
	// and creates a new propagation context.
	// (Currently "working" because my half-baked WPF scheduler just assumes Indexed Propagation should be used)
	IAsyncScheduler AsyncScheduler { get; }

	PropagationResult Start(); // UI thread
}

public interface ITaskWrapper
{
	void AttemptCancel();

	Task Task { get; }
}
