using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

/// <summary>
/// Every <see cref="INode"/> should have its own GraphManager.
/// Handles listener subscription and assists in propagation.
/// </summary>
public sealed class GraphManager
{
	private readonly List<INode> listeners = new();
	private INode[]? outBuffer = null;
	private int pendingLayer = 0;
	private int? finalizedLayer;

	/// <summary>
	/// Supports topological sorting. A node in Layer 0 has no dependencies,
	/// and a node in Layer N may depend on nodes in Layers 0..N-1 only.
	/// </summary>
	/// <remarks>
	/// The first time this is called it "finalizes" this node's layer.
	/// This will happen the first time another node listens to this node
	/// or the first time propagation visits this node.
	/// </remarks>
	internal int GetLayer()
	{
		if (!finalizedLayer.HasValue)
		{
			finalizedLayer = pendingLayer;
		}
		return finalizedLayer.Value;
	}

	public void AddListener(INode listener)
	{
		// Finalize this.layer before passing it to the listener
		this.GetLayer();
		listener.GraphManager.ListenTo(this);
		listeners.Add(listener);
	}

	private void ListenTo(GraphManager source)
	{
		if (!source.finalizedLayer.HasValue)
		{
			throw new Exception("Assert fail - expected source.layer to be finalized here");
		}
		int myMinLayer = source.finalizedLayer.Value + 1;

		// If this.layer is finalized, don't allow it to increase.
		// (Maybe we could allow it and push down the increase to the entire subtree, but I don't
		//  want that to be the default behavior. Something like `bool AllowImproperListening` maybe...)
		if (this.finalizedLayer.HasValue && myMinLayer > this.finalizedLayer.Value)
		{
			throw new InvalidOperationException("Cannot increase layer after other nodes have subscribed to this node.");
		}
		this.pendingLayer = Math.Max(this.pendingLayer, myMinLayer);
	}

	/// <summary>
	/// NOT THREAD SAFE - Should be called from the UI thread only.
	/// </summary>
	internal ReadOnlySpan<INode> GetListeners()
	{
		if (outBuffer == null || outBuffer.Length < listeners.Count)
		{
			outBuffer = new INode[listeners.Count];
		}

		Stack<int> removeIndexes = new();

		int bufferIndex = 0;
		for (int i = 0; i < listeners.Count; i++)
		{
			var listener = listeners[i];
			if (listener.GraphConnectionStatus == GraphConnectionStatus.Connected)
			{
				outBuffer[bufferIndex++] = listener;
			}
			else if (listener.GraphConnectionStatus == GraphConnectionStatus.PermanentlyDisconnected)
			{
				removeIndexes.Push(i);
			}
		}

		while (removeIndexes.TryPeek(out int removeIdx))
		{
			listeners.RemoveAt(removeIdx);
		}

		return outBuffer.AsSpan().Slice(0, bufferIndex);
	}

	internal (PropagationId propagationId, int index) PropagationTempIndex { get; set; } = (PropagationId.None, -1);

	public string? NotifyPropertyName { get; set; }
}
