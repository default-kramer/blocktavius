using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

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

	INodeGroup NodeGroup { get; }
}
