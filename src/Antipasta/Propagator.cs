using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

sealed class Propagator
{
	public enum NodeStatus
	{
		// Order matters here! We must never transition backwards!
		// For example, Enqueued -> Changed is fine but Changed -> Enqueued is not.
		// UPDATE: There is one exception, we do allow going from Enqueued -> ***DuringSetup,
		// but the order does still matter for other validation checks.
		None = 0,
		VisitedDuringSetup,
		ChangedDuringSetup,
		Enqueued,
		Visited,
		Changed,
	};

	public sealed class IndexedNode<T>
	{
		public required int Layer { get; init; }
		public required int IndexWithinLayer { get; init; }
		public required INode Node { get; init; }
		public required T Payload { get; set; }
	}

	sealed class LayeredNodeQueue<T>
	{
		public required PropagationId PropagationId { get; init; }
		private readonly List<List<IndexedNode<T>>?> lookup = new();
		public int CurrentLayer { get; private set; } = -1; // start at -1 so that TryGetNextLayer will attempt layer 0

		public void SkipToLayer(int layer)
		{
			CurrentLayer = layer;
		}

		public IndexedNode<T> AddOrUpdate(INode node, T value)
		{
			var entry = FindOrAdd(node, value);
			entry.Payload = value;
			return entry;
		}

		public IndexedNode<T> FindOrAdd(INode node, T addValue)
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
				var entry = new IndexedNode<T>()
				{
					IndexWithinLayer = index,
					Layer = layer,
					Node = node,
					Payload = addValue,
				};
				layerTable.Add(entry);
				return entry;
			}
		}

		private List<IndexedNode<T>> GetOrCreateLayerTable(int layer)
		{
			while (lookup.Count <= layer)
			{
				lookup.Add(null);
			}

			var retval = lookup[layer];
			if (retval == null)
			{
				retval = new List<IndexedNode<T>>();
				lookup[layer] = retval;
			}
			return retval;
		}

		public bool TryGetNextLayer(out IReadOnlyList<IndexedNode<T>> layerTable)
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
	private readonly LayeredNodeQueue<NodeStatus> queue;
	private readonly PropagationId propagationId;
	private readonly HashSet<INodeGroup> groupsWithChanges; // TODO NOMERGE need reference equality here
	private readonly List<INodeGroup> groupsWithChangesInOrder;
	private readonly Changeset sequelChangeset;

	private Propagator(Context context, Changeset sequelChangeset)
	{
		this.context = context;
		propagationId = PropagationId.Create();
		queue = new LayeredNodeQueue<NodeStatus>() { PropagationId = propagationId };
		groupsWithChanges = new();
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
		if (node is IImmediateNotifyNode n)
		{
			var orig = context.IsNotifying;
			try
			{
				context.IsNotifying = true;
				node.NodeGroup.OnChanged(n);
			}
			finally
			{
				context.IsNotifying = orig;
			}
		}
	}

	private IndexedNode<NodeStatus> AfterChanged(IndexedNode<NodeStatus> item)
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
			if (found.Payload > NodeStatus.Enqueued)
			{
				throw new InvalidOperationException("Circular dependency detected!");
			}
			found.Payload = NodeStatus.Enqueued;
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
			var entry = queue.FindOrAdd(change.node, NodeStatus.VisitedDuringSetup);
			var result = change.func(context);
			if (result == PropagationResult.Changed)
			{
				entry.Payload = NodeStatus.ChangedDuringSetup;
				AfterChanged(entry);
				minLayer = Math.Min(minLayer.GetValueOrDefault(entry.Layer), entry.Layer);
			}
			else
			{
				entry.Payload = NodeStatus.None;
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
		if (entry.Payload >= NodeStatus.Visited)
		{
			// We've come too far. This change must be deferred to the sequel.
			sequelChangeset.EnqueueViaSequel(change);
		}
		else
		{
			entry.Payload = NodeStatus.VisitedDuringSetup;
			var result = change.func(context);
			if (result == PropagationResult.Changed)
			{
				entry.Payload = NodeStatus.ChangedDuringSetup;
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
				if (item.Payload == NodeStatus.None) // not enqueued, ignore it
				{
					continue;
				}

				if (item.Payload >= NodeStatus.Visited)
				{
					throw new Exception($"Assert fail - node in layer {item.Layer} is already visited? {item.Node}");
				}

				var entry = queue.AddOrUpdate(item.Node, NodeStatus.Visited);
				var result = item.Node.OnPropagation(context);
				if (result == PropagationResult.Changed)
				{
					entry.Payload = NodeStatus.Changed;
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
