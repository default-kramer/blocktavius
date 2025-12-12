using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

public abstract class BaseNode : INode
{
	private readonly GraphManager graphManager = new();
	GraphManager INode.GraphManager => graphManager;

	public virtual GraphConnectionStatus GraphConnectionStatus => GraphConnectionStatus.Connected;

	public abstract INodeGroup NodeGroup { get; }

	public abstract PropagationResult OnPropagation(IPropagationContext context);
}
