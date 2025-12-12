using Antipasta.Scheduling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

/// <summary>
/// This class expects to be used from the UI thread only, and throws if <see cref="ApplyChanges"/>
/// is called before any previous call has finished.
/// It is the responsibility of the scheduler to make sure this is used correctly.
/// </summary>
public sealed class Changeset : IChangeset
{
	readonly record struct Change(INode node, Func<IPropagationContext, PropagationResult> func);

	public required IAsyncScheduler AsyncScheduler { get; init; }
	private readonly Queue<Change> changes = new();
	private Propagator? propagator = null;
	private bool fullyCompleted = false;

	/// <summary>
	/// A value of N allows N+1 total propagations.
	/// The "+1" is because the first propagation is not considered a sequel
	/// and is always allowed to execute.
	/// (Any N less than 0 will be treated as 0.)
	/// </summary>
	public int SequelLimit { get; set; } = 0;

	private Changeset Enqueue(INode node, Func<IPropagationContext, PropagationResult> func)
	{
		if (fullyCompleted)
		{
			throw new InvalidOperationException("This changeset has already completed; no new changes may be requested");
		}

		var change = new Change(node, func);
		if (propagator != null)
		{
			propagator.OnChangeRequestedDuringPropagation(change);
		}
		else
		{
			changes.Enqueue(change);
		}
		return this;
	}

	public IChangeset RequestChangeUntyped(ISettableElementUntyped element, object? value)
	{
		return Enqueue(element, ctx => element.AcceptSetValueRequestUntyped(ctx, value));
	}

	public IChangeset RequestChange<T>(ISettableElement<T> element, T value)
	{
		return Enqueue(element, ctx => element.AcceptSetValueRequest(ctx, value));
	}

	public IChangeset RequestChange(IAsyncProgress asyncProgress)
	{
		return Enqueue(asyncProgress.SourceNode, _ => asyncProgress.LatestProgressReport());
	}

	private void EnqueueViaSequel(Change change)
	{
		if (propagator != null)
		{
			throw new Exception("Assert fail - this sequel's queue is already finalized");
		}
		changes.Enqueue(change);
	}

	public void ApplyChanges()
	{
		if (fullyCompleted)
		{
			throw new InvalidOperationException("Cannot ApplyChanges twice");
		}

		try { ApplyChanges(this); }
		finally { fullyCompleted = true; }
	}

	private Changeset CreateSequel() => new Changeset { AsyncScheduler = this.AsyncScheduler };

	private static readonly SemaphoreSlim oneAtATime = new(1, 1);
	private static void ApplyChanges(Changeset changeset)
	{
		// Only one propagation may be running at a time; this is non-negotiable.
		// User code is allowed to configure the sequel limit.
		// Any sequels will execute within the same acquisition of this semaphore.
		if (oneAtATime.Wait(0))
		{
			try
			{
				DoApplyChanges(changeset);
			}
			finally
			{
				oneAtATime.Release();
			}
		}
		else
		{
			throw new InvalidOperationException("Previous changeset has not completed");
		}
	}

	private static void DoApplyChanges(Changeset changeset)
	{
		int sequelCount = -1; // will be 0 on first changeset
		int sequelLimit = Math.Max(0, changeset.SequelLimit);

		while (true)
		{
			if (changeset.changes.Count == 0)
			{
				return;
			}
			sequelCount++;
			if (sequelCount > sequelLimit)
			{
				throw new InvalidOperationException($"Sequel limit of {sequelLimit} has been reached");
			}

			var sequel = changeset.CreateSequel();
			var propagator = Propagator.Create(changeset.AsyncScheduler, sequel);

			// Set this immediately so that any future calls to Enqueue anything go to the propagator instead
			changeset.propagator = propagator;

			propagator.Setup(changeset.changes);
			propagator.Propagate();

			changeset = sequel;
		}
	}

	sealed class Propagator
	{
		/// <summary>
		/// Order matters here! We must never transition backwards!
		/// For example, Enqueued -> Changed is fine but Changed -> Enqueued is not.
		/// </summary>
		public enum NodeStatus
		{
			None = 0,
			ChangedDuringSetup,
			Enqueued = ChangedDuringSetup, // functionally the same, but for clarity
			Visited,
			Changed,
		};

		public sealed class IndexedNode
		{
			private NodeStatus status;
			public IndexedNode(NodeStatus status)
			{
				this.status = status;
			}

			public required int Layer { get; init; }
			public required int IndexWithinLayer { get; init; }
			public required INode Node { get; init; }
			public NodeStatus Status => status;

			public void AdvanceStatus(NodeStatus newStatus)
			{
				if (newStatus < status)
				{
					throw new Exception($"Assert fail - cannot go from {status} to {newStatus}");
				}
				status = newStatus;
			}
		}

		sealed class LayeredNodeQueue
		{
			public required PropagationId PropagationId { get; init; }
			private readonly List<List<IndexedNode>?> lookup = new();
			public int CurrentLayer { get; private set; } = -1; // start at -1 so that TryGetNextLayer will attempt layer 0

			public void SkipToLayer(int layer)
			{
				CurrentLayer = layer;
			}

			public IndexedNode AddOrAdvance(INode node, NodeStatus value)
			{
				var entry = FindOrAdd(node, value);
				entry.AdvanceStatus(value);
				return entry;
			}

			public IndexedNode FindOrAdd(INode node, NodeStatus addValue)
			{
				var gm = node.GraphManager;
				int layer = gm.GetLayer();
				var layerTable = GetOrCreateLayerTable(layer);

				var existingIndex = gm.PropagationTempIndex;
				if (existingIndex.propagationId.Counter == this.PropagationId.Counter)
				{
					var entry = layerTable[existingIndex.index];
					if (!ReferenceEquals(entry.Node, node))
					{
						throw new Exception("Assert fail - node not found at previously assigned index");
					}
					return entry;
				}
				else if (existingIndex.propagationId.Counter > this.PropagationId.Counter)
				{
					// Should never happen because propagation should
					// 1) always happen on the same thread (UI thread) and
					// 2) always complete synchronously
					// Note: Async progress updates create a new propagation.
					throw new InvalidOperationException($"Propagation {this.PropagationId.Counter} cannot continue when newer propagation {existingIndex.propagationId.Counter} exists");
				}
				else
				{
					int index = layerTable.Count;
					gm.PropagationTempIndex = (PropagationId, index);
					var entry = new IndexedNode(addValue)
					{
						IndexWithinLayer = index,
						Layer = layer,
						Node = node,
					};
					layerTable.Add(entry);
					return entry;
				}
			}

			private List<IndexedNode> GetOrCreateLayerTable(int layer)
			{
				while (lookup.Count <= layer)
				{
					lookup.Add(null);
				}

				var retval = lookup[layer];
				if (retval == null)
				{
					retval = new List<IndexedNode>();
					lookup[layer] = retval;
				}
				return retval;
			}

			public bool TryGetNextLayer(out IReadOnlyList<IndexedNode> layerTable)
			{
				for (int i = CurrentLayer + 1; i < lookup.Count; i++)
				{
					var table = lookup[i];
					if (table != null)
					{
						layerTable = table;
						CurrentLayer = i;
						return true;
					}
				}

				layerTable = null!;
				return false;
			}
		}

		sealed class Context : IPropagationContext
		{
			public required Internalized<IAsyncScheduler> AsyncScheduler { get; init; }

			public Progress SetupProgress { get; set; } = Progress.NotStarted;
			public Progress PropagationProgress { get; set; } = Progress.NotStarted;
			public bool IsNotifying { get; set; } = false;
		}

		private readonly Context context;
		private readonly LayeredNodeQueue queue;
		private readonly PropagationId propagationId;
		private readonly HashSet<INodeGroup> groupsWithChanges;
		private readonly List<INodeGroup> groupsWithChangesInOrder;
		private readonly Changeset sequelChangeset;

		private Propagator(Context context, Changeset sequelChangeset)
		{
			this.context = context;
			propagationId = PropagationId.Create();
			queue = new LayeredNodeQueue() { PropagationId = propagationId };
			groupsWithChanges = new(ReferenceEqualityComparer.Instance);
			groupsWithChangesInOrder = new();
			this.sequelChangeset = sequelChangeset;
		}

		internal static Propagator Create(IAsyncScheduler scheduler, Changeset sequelChangeset)
		{
			var context = new Context { AsyncScheduler = scheduler.Internalize() };
			return new Propagator(context, sequelChangeset);
		}

		private static void MaybeNotify(INode node, Context context)
		{
			if (node.GraphManager.NotifyPropertyName != null)
			{
				var orig = context.IsNotifying;
				try
				{
					context.IsNotifying = true;
					node.NodeGroup.NotifyPropertyChanged(node);
				}
				finally
				{
					context.IsNotifying = orig;
				}
			}
		}

		private IndexedNode AfterChanged(IndexedNode item)
		{
			var node = item.Node;
			if (groupsWithChanges.Add(node.NodeGroup))
			{
				groupsWithChangesInOrder.Add(node.NodeGroup);
			}

			MaybeNotify(node, context);

			foreach (var listener in node.GraphManager.GetListeners())
			{
				var found = queue.FindOrAdd(listener, NodeStatus.Enqueued);
				if (found.Status > NodeStatus.Enqueued)
				{
					throw new InvalidOperationException("Circular dependency detected!");
				}
				found.AdvanceStatus(NodeStatus.Enqueued);
			}

			return item;
		}

		internal void Setup(IEnumerable<Change> changes)
		{
			if (context.SetupProgress != Progress.NotStarted)
			{
				throw new InvalidOperationException($"Cannot call {nameof(Setup)} twice");
			}

			context.SetupProgress = Progress.InProgress;

			int? minLayer = null;
			foreach (var change in changes)
			{
				var result = change.func(context);
				if (result == PropagationResult.Changed)
				{
					var entry = queue.AddOrAdvance(change.node, NodeStatus.ChangedDuringSetup);
					AfterChanged(entry);
					minLayer = Math.Min(minLayer.GetValueOrDefault(entry.Layer), entry.Layer);
				}
			}

			if (minLayer.HasValue)
			{
				queue.SkipToLayer(minLayer.Value);
			}

			context.SetupProgress = Progress.Completed;
		}

		internal void OnChangeRequestedDuringPropagation(Change change)
		{
			var entry = queue.FindOrAdd(change.node, NodeStatus.None);
			if (entry.Status >= NodeStatus.Visited)
			{
				// We've come too far. This change must be deferred to the sequel.
				sequelChangeset.EnqueueViaSequel(change);
			}
			else
			{
				var result = change.func(context);
				if (result == PropagationResult.Changed)
				{
					entry.AdvanceStatus(NodeStatus.ChangedDuringSetup);
					AfterChanged(entry);
				}
			}
		}

		internal void Propagate()
		{
			if (context.SetupProgress != Progress.Completed)
			{
				throw new InvalidOperationException($"Must call {nameof(Setup)} first");
			}
			if (context.PropagationProgress != Progress.NotStarted)
			{
				throw new InvalidOperationException($"Cannot call {nameof(Propagate)} twice");
			}

			context.PropagationProgress = Progress.InProgress;

			while (queue.TryGetNextLayer(out var layerTable))
			{
				foreach (var item in layerTable)
				{
					if (item.Status == NodeStatus.None) // not enqueued, ignore it
					{
						continue;
					}

					if (item.Status >= NodeStatus.Visited)
					{
						throw new Exception($"Assert fail - node in layer {item.Layer} is already visited? {item.Node}");
					}

					var entry = queue.AddOrAdvance(item.Node, NodeStatus.Visited);
					var result = item.Node.OnPropagation(context);
					if (result == PropagationResult.Changed)
					{
						entry.AdvanceStatus(NodeStatus.Changed);
						AfterChanged(entry);
					}
				}
			}

			context.PropagationProgress = Progress.Completed;

			// TODO not sure if we should close (dispose) the PropagationScope before this callback or not:
			foreach (var group in groupsWithChangesInOrder)
			{
				group.OnPropagationCompleted(context);
			}
		}
	}
}
