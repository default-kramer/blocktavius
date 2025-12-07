using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Antipasta.IndexedPropagation;

public sealed class GroupPropagator
{
	private readonly NodeQueue nodeQueue; // only for nodes in the current group
	private readonly HashSet<INodeWithStaticPassInfo> listenersInOtherGroups = new();
	private NodeIndex minEnqueuedNode = new NodeIndex(int.MaxValue); // for sorting child groups
	private bool didPropagate = false;

	private GroupPropagator(NodeQueue nodeQueue)
	{
		this.nodeQueue = nodeQueue;
	}

	private static void Begin(INode element, IAsyncScheduler scheduler, Func<NodeQueue, PropagationResult> starterThunk)
	{
		var node = element as INodeWithStaticPassInfo;
		if (node == null)
		{
			throw new Exception("TODO");
		}

		var queue = new NodeQueue()
		{
			NodeGroup = element.NodeGroup,
			ParentContexts = ImmutableStack<IPropagationContext>.Empty,
			AsyncScheduler = scheduler,
		};

		var result = starterThunk(queue);
		if (result == PropagationResult.Changed)
		{
			MaybeNotify(element);
			queue.StartFrom(node, NodeQueue.NodeStatus.Changed);
			var me = new GroupPropagator(queue);
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

	private GroupPropagator CreateChildPropagator(IGrouping<INodeGroup, INodeWithStaticPassInfo> grp)
	{
		var nextQueue = this.nodeQueue.CreateForChildGroup(grp.Key);
		var me = new GroupPropagator(nextQueue);
		foreach (var node in grp)
		{
			me.Enqueue(node);
		}
		return me;
	}

	public void Enqueue(INode node)
	{
		if (node is INodeWithStaticPassInfo n)
		{
			if (n.NodeIndex.Index < minEnqueuedNode.Index)
			{
				minEnqueuedNode = n.NodeIndex;
			}

			if (n.NodeGroup == nodeQueue.NodeGroup)
			{
				nodeQueue.Enqueue(n);
			}
			else
			{
				listenersInOtherGroups.Add(n);
			}
		}
		else
		{
			throw new InvalidOperationException($"Cannot use Indexed Propagation on node: {node}");
		}
	}

	public void Propagate()
	{
		if (didPropagate)
		{
			throw new InvalidOperationException("Cannot call Propagate() twice");
		}
		didPropagate = true;
		using var _ = AntipastaThreadLocal.UsePropagationScope();

		while (nodeQueue.GetNextPass(out var passNodes))
		{
			foreach (var passNode in passNodes)
			{
				nodeQueue.UpdateStatus(passNode, NodeQueue.NodeStatus.Visited);
				var result = passNode.OnPropagation(nodeQueue);
				if (result == PropagationResult.Changed)
				{
					MaybeNotify(passNode);
					nodeQueue.UpdateStatus(passNode, NodeQueue.NodeStatus.Changed);
					foreach (var listener in passNode.GraphManager.GetListeners())
					{
						Enqueue(listener);
					}
				}
			}
		}

		nodeQueue.NodeGroup.OnSelfResolved(nodeQueue);
	}

	public IEnumerable<GroupPropagator> NextGroups()
	{
		if (!didPropagate)
		{
			throw new InvalidOperationException("Must call Propagate() first");
		}

		var nextGroups = listenersInOtherGroups.GroupBy(x => x.NodeGroup).Select(CreateChildPropagator).ToList();
		nextGroups.Sort((a, b) => a.minEnqueuedNode.Index.CompareTo(b.minEnqueuedNode.Index));
		return nextGroups;
	}

	/// <summary>
	/// Given two <see cref="NodeIndex"/>es, the smaller one should not have a dependency on the larger one.
	/// (If it does, it means that either the graph is not a DAG or there is a bug in the index creation
	///  and we will throw an exception.)
	/// </summary>
	sealed class NodeQueue : IPropagationContext
	{
		public enum NodeStatus
		{
			// Order matters here! Status transitions *always* go in this order:
			None = 0,
			Enqueued = 1,
			Visited = 2,
			Changed = 3,
		};

		public required IAsyncScheduler AsyncScheduler { get; init; }
		public required INodeGroup NodeGroup { get; init; }
		public required IImmutableStack<IPropagationContext> ParentContexts { get; init; }

		// The index into this list is always zero-based which might waste a bit of space at
		// the front of the list but there's no need to prematurely optimize here.
		private readonly List<(NodeStatus status, INodeWithStaticPassInfo? node)> nodes;
		private int nextPassStart;

		public NodeQueue()
		{
			this.nodes = new();
			this.nextPassStart = 0;
		}

		private NodeQueue(NodeQueue parent)
		{
			this.nodes = parent.nodes.ToList();
			this.nextPassStart = parent.nextPassStart;
		}

		public NodeQueue CreateForChildGroup(INodeGroup childGroup)
		{
			return new NodeQueue(this)
			{
				NodeGroup = childGroup,
				ParentContexts = this.ParentContexts.Push(this),
				AsyncScheduler = this.AsyncScheduler,
			};
		}

		public void Enqueue(INodeWithStaticPassInfo node)
		{
			int index = node.NodeIndex.Index;
			if (index < nextPassStart)
			{
				if (nodes.ElementAtOrDefault(index).node != null)
				{
					throw new InvalidOperationException($"Cycle detected! {node}");
				}

				// Either there is a cycle or the indexes are not correct:
				throw new InvalidOperationException("Cannot enqueue node; its pass has already been processed.");
			}
			while (index >= nodes.Count)
			{
				nodes.Add((NodeStatus.None, null));
			}

			var entry = nodes[index];
			if (entry.status >= NodeStatus.Visited)
			{
				throw new InvalidOperationException($"Cycle detected! {node}");
			}
			if (entry.node != null && !object.ReferenceEquals(node, entry.node))
			{
				throw new InvalidOperationException($"Distinct nodes cannot have duplicate indexes! For {entry.node} and {node}");
			}
			nodes[index] = (NodeStatus.Enqueued, node);
		}

		/// <summary>
		/// If we're currently on pass N, check N+1, N+2, ... until we find a pass
		/// which has at least one listener who needs to be visited.
		/// </summary>
		public bool GetNextPass(out IReadOnlyList<INodeWithStaticPassInfo> pass)
		{
			var passNodes = new List<INodeWithStaticPassInfo>();

			PassIndex? currentPass = null;
			int i;
			for (i = nextPassStart; i < nodes.Count; i++)
			{
				if (currentPass.HasValue && i > currentPass.Value.MaxNode.Index)
				{
					break;
				}

				var candidate = nodes[i];
				switch (candidate.status)
				{
					case NodeStatus.None:
						break;
					case NodeStatus.Visited:
					case NodeStatus.Changed:
						throw new InvalidOperationException("TODO this means there is a cycle, right?");
					case NodeStatus.Enqueued:
						if (candidate.node == null)
						{
							throw new Exception("Assert fail, enqueued status must have node");
						}
						if (currentPass == null)
						{
							currentPass = candidate.node.PassIndex;
							passNodes.Add(candidate.node);
						}
						else if (currentPass.Value == candidate.node.PassIndex)
						{
							passNodes.Add(candidate.node);
						}
						break;
					default:
						throw new Exception($"Assert fail, unrecognized status {candidate.status}");
				}
			}

			nextPassStart = i;
			pass = passNodes;
			return pass.Count > 0;
		}

		public bool HasChanged(INode node) // TODO this isn't needed, is it?
		{
			if (node is INodeWithStaticPassInfo n)
			{
				int index = n.NodeIndex.Index;
				return index >= 0 && index < nodes.Count && nodes[index].status >= NodeStatus.Changed;
			}
			return false;
		}

		public void UpdateStatus(INodeWithStaticPassInfo node, NodeStatus status)
		{
			int index = node.NodeIndex.Index;
			if (index >= nodes.Count)
			{
				throw new Exception("Assert fail - expected that this node was already Enqueued");
			}
			nodes[index] = (status, node);
		}

		public void StartFrom(INodeWithStaticPassInfo node, NodeStatus status)
		{
			Enqueue(node);
			UpdateStatus(node, status);
			nextPassStart = node.NodeIndex.Index + 1;
			foreach (var listener in node.GraphManager.GetListeners())
			{
				this.Enqueue(listener as INodeWithStaticPassInfo ?? throw new Exception("TODO"));
			}
		}
	}
}
