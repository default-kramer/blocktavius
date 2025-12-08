using Antipasta.IndexedPropagation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Antipasta;

public sealed class Propagator
{
	public enum NodeStatus
	{
		// Order matters here! Status transitions *always* go in this order:
		None = 0,
		Enqueued = 1,
		Visited = 2,
		Changed = 3,
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

		public void SkipToLayer(IndexedNode<T> node)
		{
			CurrentLayer = node.Layer;
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
		public required IAsyncScheduler AsyncScheduler { get; init; }
	}

	private readonly Context context;
	private readonly LayeredNodeQueue<NodeStatus> queue;
	private readonly PropagationId propagationId;
	private readonly HashSet<INodeGroup> groupsWithChanges;
	private readonly List<INodeGroup> groupsWithChangesInOrder;

	private Propagator(Context context)
	{
		this.context = context;
		propagationId = PropagationId.Create();
		queue = new LayeredNodeQueue<NodeStatus>() { PropagationId = propagationId };
		groupsWithChanges = new();
		groupsWithChangesInOrder = new();
	}

	private static void Begin(INode node, IAsyncScheduler scheduler, Func<IPropagationContext, PropagationResult> starterFunc)
	{
		var context = new Context { AsyncScheduler = scheduler };
		var result = starterFunc(context);
		if (result == PropagationResult.Changed)
		{
			var me = new Propagator(context);
			var item = me.AfterChanged(node);
			me.queue.SkipToLayer(item);
			me.Propagate();
		}
	}

	public static void SetElement<T>(ISettableElement<T> element, T value, IAsyncScheduler scheduler)
	{
		Begin(element, scheduler, context => element.AcceptSetValueRequest(context, value));
	}

	public static void HandleAsyncProgress(IAsyncProgress progress) // UI thread
	{
		Begin(progress.SourceNode, progress.AsyncScheduler, _ => progress.Start());
	}

	private static void MaybeNotify(INode node)
	{
		if (node is IImmediateNotifyNode n)
		{
			node.NodeGroup.OnChanged(n);
		}
	}

	private IndexedNode<NodeStatus> AfterChanged(INode node)
	{
		var item = queue.FindOrAdd(node, NodeStatus.Changed);
		if (groupsWithChanges.Add(node.NodeGroup))
		{
			groupsWithChangesInOrder.Add(node.NodeGroup);
		}

		MaybeNotify(node);

		foreach (var listener in node.GraphManager.GetListeners())
		{
			var found = queue.FindOrAdd(listener, NodeStatus.Enqueued);
			if (found.Payload > NodeStatus.Enqueued)
			{
				throw new InvalidOperationException("Circular dependency detected!");
			}
		}

		return item;
	}

	private bool didPropagate = false;
	private void Propagate()
	{
		if (didPropagate)
		{
			throw new InvalidOperationException("Cannot call this method twice");
		}
		didPropagate = true;
		using var _ = AntipastaThreadLocal.UsePropagationScope();

		while (queue.TryGetNextLayer(out var layerTable))
		{
			foreach (var item in layerTable)
			{
				if (item.Payload >= NodeStatus.Visited)
				{
					throw new Exception($"Assert fail - node in layer {item.Layer} is already visited? {item.Node}");
				}
				var result = item.Node.OnPropagation(context);
				if (result == PropagationResult.Changed)
				{
					AfterChanged(item.Node);
				}
				else
				{
					queue.AddOrUpdate(item.Node, NodeStatus.Visited);
				}
			}
		}

		// TODO not sure if we should close (dispose) the PropagationScope before this callback or not:
		foreach (var group in groupsWithChangesInOrder)
		{
			group.OnPropagationCompleted(context);
		}
	}
}
