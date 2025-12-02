using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta.IndexedPropagation;

// This file contains an implementation for discovering a dependency graph
// from a static "marker" class and assigning propagation indexes.

/// <summary>
/// Analyzes a set of types to discover a dependency graph based on constructor arguments,
/// performs a topological sort, and assigns pass and node indexes for propagation.
/// </summary>
public sealed class StaticGraphIndexer
{
	/// <summary>
	/// Holds the calculated index and pass information for a single node type.
	/// </summary>
	public sealed class NodeInfo
	{
		public Type NodeType { get; }
		public Type ImplementationType { get; }
		public NodeIndex NodeIndex { get; }
		public PassIndex PassIndex { get; }

		internal NodeInfo(Type nodeType, Type implementationType, int nodeIndex, PassIndex passIndex)
		{
			NodeType = nodeType;
			ImplementationType = implementationType;
			NodeIndex = new NodeIndex(nodeIndex);
			PassIndex = passIndex;
		}
	}

	private readonly IReadOnlyDictionary<Type, NodeInfo> _nodeInfoByType;

	/// <summary>
	/// Creates an indexer by reflecting on the given marker type and scanning its assembly for implementations.
	/// </summary>
	/// <param name="markerRootType">A static class (like 'I') whose nested interfaces define the nodes of the graph.</param>
	public StaticGraphIndexer(Type markerRootType)
	{
		var implementationAssembly = markerRootType.Assembly;

		// 1. Discover all interfaces within the marker type that represent nodes.
		var nodeInterfaceTypes = DiscoverNodeInterfaces(markerRootType);

		// 2. Find the concrete types that implement these interfaces by scanning the assembly.
		var implementationMap = MapImplementations(nodeInterfaceTypes, implementationAssembly);

		// 3. Build an adjacency list representing the dependency graph (maps a node to its dependents).
		var dependentsGraph = BuildDependencyGraph(implementationMap);

		// 4. Perform a topological sort to assign pass and node indexes.
		_nodeInfoByType = AssignIndexes(dependentsGraph, implementationMap);
	}

	/// <summary>
	/// Gets the calculated indexing information for a given node interface type.
	/// </summary>
	public NodeInfo GetInfo(Type nodeInterfaceType) => _nodeInfoByType[nodeInterfaceType];

	public bool TryGetByImplementationType(Type implementationType, out NodeInfo info)
	{
		info = _nodeInfoByType.Values.FirstOrDefault(x => x.ImplementationType == implementationType)!;
		return info != null;
	}

	public string DUMP()
	{
		var sb = new StringBuilder();
		var data = _nodeInfoByType.OrderBy(x => x.Value.PassIndex.Index).ToList();
		int passIndex = -1;
		foreach (var item in data)
		{
			if (item.Value.PassIndex.Index != passIndex)
			{
				passIndex = item.Value.PassIndex.Index;
				sb.AppendLine().Append($"Pass {passIndex}");
			}
			sb.AppendLine().Append($"  {item.Value.NodeIndex.Index}: {item.Value.NodeType.Name}");
		}
		return sb.ToString();
	}

	private static List<Type> DiscoverNodeInterfaces(Type root)
	{
		// Recursively find all nested interfaces within the root static class (e.g., I.Project.Profile)
		return root.GetNestedTypes(BindingFlags.Public | BindingFlags.Static)
			.SelectMany(DiscoverNodeInterfaces)
			.Concat(root.GetNestedTypes(BindingFlags.Public).Where(t => t.IsInterface))
			.ToList();
	}

	private static Dictionary<Type, Type> MapImplementations(IEnumerable<Type> interfaces, Assembly assemblyToScan)
	{
		var interfaceSet = new HashSet<Type>(interfaces);

		// Scan all types in the provided assembly
		var implementationTypes = assemblyToScan.GetTypes();

		return implementationTypes
			.Select(implType => new
			{
				ImplementationType = implType,
				// Find which of our discovered node interfaces this type implements.
				InterfaceType = implType.GetInterfaces().FirstOrDefault(interfaceSet.Contains)
			})
			.Where(x => x.InterfaceType != null)
			.ToDictionary(x => x.InterfaceType!, x => x.ImplementationType);
	}

	private static Dictionary<Type, List<Type>> BuildDependencyGraph(IReadOnlyDictionary<Type, Type> implementationMap)
	{
		var dependentsGraph = implementationMap.Keys.ToDictionary(k => k, _ => new List<Type>());

		foreach (var (interfaceType, implType) in implementationMap)
		{
			// Assuming one public constructor that defines dependencies.
			// This might need refinement if multiple constructors are present or non-public constructors are used.
			var constructor = implType.GetConstructors().FirstOrDefault();
			if (constructor == null) continue;

			foreach (var param in constructor.GetParameters())
			{
				// If a constructor parameter is one of our node interfaces, it's a dependency.
				if (implementationMap.ContainsKey(param.ParameterType))
				{
					// The parameter's type is a dependency of the current interfaceType.
					// So, param.ParameterType (dependency) -> interfaceType (dependent)
					dependentsGraph[param.ParameterType].Add(interfaceType);
				}
			}
		}
		return dependentsGraph;
	}

	private static Dictionary<Type, NodeInfo> AssignIndexes(
		Dictionary<Type, List<Type>> dependentsGraph,
		IReadOnlyDictionary<Type, Type> implementationMap)
	{
		var allNodes = dependentsGraph.Keys.ToList();
		var inDegrees = allNodes.ToDictionary(n => n, _ => 0);
		foreach (var node in allNodes)
		{
			foreach (var dependent in dependentsGraph[node])
			{
				inDegrees[dependent]++;
			}
		}

		// Standard Kahn's algorithm for topological sort.
		var queue = new Queue<Type>(allNodes.Where(n => inDegrees[n] == 0));
		var sortedNodes = new List<Type>();

		while (queue.Count > 0)
		{
			var node = queue.Dequeue();
			sortedNodes.Add(node);

			foreach (var dependent in dependentsGraph[node])
			{
				inDegrees[dependent]--;
				if (inDegrees[dependent] == 0)
				{
					queue.Enqueue(dependent);
				}
			}
		}

		if (sortedNodes.Count != allNodes.Count)
		{
			var cycleNodes = string.Join(", ", allNodes.Except(sortedNodes).Select(t => t.Name));
			throw new InvalidOperationException($"A dependency cycle was detected in the graph. Nodes involved: {cycleNodes}");
		}

		// The result of the sort gives the global node index.
		var nodeIndexMap = sortedNodes
			.Select((type, index) => new { Type = type, Index = index })
			.ToDictionary(x => x.Type, x => x.Index);

		// Assign pass numbers based on dependencies.
		// A node's pass is 1 + max(pass of its direct dependencies).
		var passAssignmentMap = new Dictionary<Type, int>();
		foreach (var nodeType in sortedNodes)
		{
			int maxDepPass = -1;
			// Find nodes that 'nodeType' depends on.
			// This is the reverse lookup from dependentsGraph.
			foreach (var dependencyCandidate in dependentsGraph.Keys)
			{
				if (dependentsGraph[dependencyCandidate].Contains(nodeType))
				{
					maxDepPass = Math.Max(maxDepPass, passAssignmentMap[dependencyCandidate]);
				}
			}
			passAssignmentMap[nodeType] = maxDepPass + 1;
		}

		var finalInfos = new Dictionary<Type, NodeInfo>();
		var passData = new Dictionary<int, (int min, int max)>();

		// Calculate the min/max node index for each pass.
		// Node indexes are assigned sequentially during the topological sort.
		// Pass indexes are assigned after pass numbers are known.
		foreach (var nodeType in sortedNodes)
		{
			int index = nodeIndexMap[nodeType];
			int pass = passAssignmentMap[nodeType];

			if (passData.TryGetValue(pass, out var d))
			{
				passData[pass] = (d.min, index); // min is set once, max is updated
			}
			else
			{
				passData[pass] = (index, index);
			}
		}

		var passIndexMap = passData.ToDictionary(
			kvp => kvp.Key,
			kvp => new PassIndex(kvp.Key, new NodeIndex(kvp.Value.min), new NodeIndex(kvp.Value.max))
		);

		// Construct the final NodeInfo object for each node.
		foreach (var nodeType in sortedNodes)
		{
			var implType = implementationMap[nodeType];
			int index = nodeIndexMap[nodeType];
			int pass = passAssignmentMap[nodeType];
			finalInfos[nodeType] = new NodeInfo(nodeType, implType, index, passIndexMap[pass]);
		}

		return finalInfos;
	}
}
