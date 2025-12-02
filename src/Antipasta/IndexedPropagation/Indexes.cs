using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta.IndexedPropagation;

// The idea behind Indexed Propagation relies on our ability to create a
// topological ordering of the dependency graph ahead of time.
// For example, with the separate-class-per-node approach you could write
// a static constructor that uses reflection to discover and validate
// the DAG implied by the the constructor arguments of each node type.
//
// The concept of the NodeGroup is very relevant here.
// Whenever it is time to propagate into a new NodeGroup, we do so using a new subcontext that
// 1) inherits all information from the parent context
// 2) but is totally independent of any sibling NodeGroups.
// This sibling independence means that you can statically assign, for example:
// * ShoppingCart nodes use indexes 0-15
// * ShoppingCartItem nodes use indexes 16-18
// And even though we have a fixed number of indexes for potentially many ShoppingCartItems,
// it will work because each ShoppingCartItem will be visited using a subcontext that is
// independent from any of its sibling items.
//
// TBD if we will need "NonIndexed Propagation" to deal with situations that do not
// conform to these "static and sibling-independent" requirements...

public readonly record struct PassIndex
{
	public readonly int Index;
	public readonly NodeIndex MinNode;
	public readonly NodeIndex MaxNode;

	public PassIndex(int index, NodeIndex minNode, NodeIndex maxNode)
	{
		this.Index = index;
		this.MinNode = minNode;
		this.MaxNode = maxNode;
	}
}

public readonly record struct NodeIndex
{
	public readonly int Index;

	public NodeIndex(int index) { this.Index = index; }
}

public interface INodeWithStaticPassInfo : INode
{
	public PassIndex PassIndex { get; }
	public NodeIndex NodeIndex { get; }
}
