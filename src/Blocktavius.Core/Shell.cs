using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

/// <summary>
/// Every non-empty <see cref="IArea"/> defines at least one shell.
/// Each ShellItem is an XZ that is both
/// * not inside the area, and
/// * exactly one step 1 step (in a cardinal or an ordinal direction) away from being inside the area
///
/// Outside corners are pretty simple.
/// The XZ of an outside corner will have only one shell item with an ordinal (diagonal) direction.
///
/// Inside corners are more complicated.
/// The XZ will get (at least) three shell items, for example East, SouthEast, and South.
/// Here is an example ASCII drawing of an inside corner.
/// In this drawing, % means "in the area" and lower case letters (a - f) are part of the shell:
///     a%%
///     b%%
///     c%%
///   fed%%
///   %%%%%
///   %%%%%
///
/// The previous example would include `d` three times in the shell items:
///   a East
///   b East
///   c East
///   d East
///   d SouthEast
///   d South
///   e South
///   f South
/// </summary>
readonly record struct ShellItem
{
	/// <summary>
	/// This XZ is *not* inside the area.
	/// </summary>
	public required XZ XZ { get; init; }

	/// <summary>
	/// Taking one step in this direction from <see cref="XZ"/> puts you inside the area.
	/// </summary>
	public required Direction InsideDirection { get; init; }
}

sealed record Shell
{
	public required IArea Area { get; init; }
	public required IReadOnlyList<ShellItem> ShellItems { get; init; }
	public required bool IsHole { get; init; }
}

interface IArea
{
	Rect Bounds { get; }
	bool InArea(XZ xz);
}

static class ShellLogic
{
	private static readonly Direction[] allDirections = new[]
	{
		Direction.North, Direction.NorthEast, Direction.East, Direction.SouthEast,
		Direction.South, Direction.SouthWest, Direction.West, Direction.NorthWest
	};

	/// <summary>
	/// Most of this logic must use the expanded bounds, since shell items
	/// can be outside of the area's bounds.
	/// </summary>
	private static Rect ExpandBounds(IArea area)
	{
		return new Rect(area.Bounds.start.Add(-1, -1), area.Bounds.end.Add(1, 1));
	}

	public static IReadOnlyList<Shell> ComputeShells(IArea area)
	{
		// 1. Find Area Islands
		var pointToIslandId = FindIslands(area);

		// 2. Find all shell items using the two-pass method
		var allShellItemsMap = FindAllShellItems(area);

		// 3. Group shell items by the area island they are adjacent to
		var shellItemsByIsland = new Dictionary<int, List<ShellItem>>();
		foreach (var list in allShellItemsMap.Values)
		{
			foreach (var item in list)
			{
				var adjacentAreaPoint = item.XZ.Add(item.InsideDirection.Step);
				if (pointToIslandId.TryGetValue(adjacentAreaPoint, out var islandId))
				{
					if (!shellItemsByIsland.TryGetValue(islandId, out var items))
					{
						items = new List<ShellItem>();
						shellItemsByIsland[islandId] = items;
					}
					items.Add(item);
				}
			}
		}

		// 4. Process each island's shells
		var finalShells = new List<Shell>();
		var outsidePoints = ComputeOutsidePoints(area); // For hole detection

		foreach (var islandItems in shellItemsByIsland.Values)
		{
			var localShellPointsMap = islandItems.GroupBy(i => i.XZ).ToDictionary(g => g.Key, g => g.ToList());
			var visitedShellPoints = new HashSet<XZ>();

			foreach (var startPoint in localShellPointsMap.Keys)
			{
				if (visitedShellPoints.Add(startPoint))
				{
					var componentQueue = new Queue<XZ>();
					componentQueue.Enqueue(startPoint);
					var currentComponentItems = new List<ShellItem>();

					while (componentQueue.Count > 0)
					{
						var current = componentQueue.Dequeue();
						currentComponentItems.AddRange(localShellPointsMap[current]);
						foreach (var dir in allDirections)
						{
							var neighbor = current.Add(dir.Step);
							if (localShellPointsMap.ContainsKey(neighbor) && visitedShellPoints.Add(neighbor))
							{
								componentQueue.Enqueue(neighbor);
							}
						}
					}

					bool isHole = !outsidePoints.Contains(startPoint);
					finalShells.Add(new Shell { Area = area, ShellItems = currentComponentItems, IsHole = isHole });
				}
			}
		}

		return finalShells;
	}

	/// <summary>
	/// Constructs a dictionary such that every key is inside the area.
	/// Keys that share the same value belong to the same island.
	/// </summary>
	private static Dictionary<XZ, int> FindIslands(IArea area)
	{
		var pointToIslandId = new Dictionary<XZ, int>();
		var visitedAreaPoints = new HashSet<XZ>();
		int islandCount = 0;
		foreach (var xz in area.Bounds.Enumerate().Where(area.InArea))
		{
			if (visitedAreaPoints.Add(xz))
			{
				var queue = new Queue<XZ>();
				queue.Enqueue(xz);
				while (queue.Count > 0)
				{
					var current = queue.Dequeue();
					pointToIslandId[current] = islandCount;
					foreach (var dir in allDirections)
					{
						var neighbor = current.Add(dir.Step);
						if (area.InArea(neighbor) && visitedAreaPoints.Add(neighbor))
						{
							queue.Enqueue(neighbor);
						}
					}
				}
				islandCount++;
			}
		}
		return pointToIslandId;
	}

	/// <summary>
	/// Finds all points that are not inside the area which can "escape" to the border.
	/// Used for hole detection.
	/// </summary>
	private static HashSet<XZ> ComputeOutsidePoints(IArea area)
	{
		var searchBounds = ExpandBounds(area);
		var outsidePoints = new HashSet<XZ>();
		var floodQueue = new Queue<XZ>();

		for (int x = searchBounds.start.X; x < searchBounds.end.X; x++)
		{
			var top = new XZ(x, searchBounds.start.Z);
			if (!area.InArea(top) && outsidePoints.Add(top))
			{
				floodQueue.Enqueue(top);
			}

			var bottom = new XZ(x, searchBounds.end.Z - 1);
			if (!area.InArea(bottom) && outsidePoints.Add(bottom))
			{
				floodQueue.Enqueue(bottom);
			}
		}

		for (int z = searchBounds.start.Z; z < searchBounds.end.Z; z++)
		{
			var left = new XZ(searchBounds.start.X, z);
			if (!area.InArea(left) && outsidePoints.Add(left))
			{
				floodQueue.Enqueue(left);
			}

			var right = new XZ(searchBounds.end.X - 1, z);
			if (!area.InArea(right) && outsidePoints.Add(right))
			{
				floodQueue.Enqueue(right);
			}
		}

		while (floodQueue.Count > 0)
		{
			var current = floodQueue.Dequeue();
			foreach (var direction in allDirections)
			{
				var neighbor = current.Add(direction.Step);
				if (searchBounds.Contains(neighbor) && !area.InArea(neighbor) && outsidePoints.Add(neighbor))
				{
					floodQueue.Enqueue(neighbor);
				}
			}
		}

		return outsidePoints;
	}

	/// <summary>
	/// Finds all shell items, in no particular order.
	/// </summary>
	private static Dictionary<XZ, List<ShellItem>> FindAllShellItems(IArea area)
	{
		var searchBounds = ExpandBounds(area);
		var shellPointsMap = new Dictionary<XZ, List<ShellItem>>();

		// Pass 1: Find primary shell items using "cardinal preference", meaning that
		// if an XZ has a cardinal item all its potential ordinal items will be skipped/dropped.
		// The result is that all ordinal items will be outside corners after this pass.
		foreach (var p in searchBounds.Enumerate())
		{
			if (area.InArea(p)) continue;

			var adjacencies = new List<Direction>();
			foreach (var dir in allDirections)
			{
				if (area.InArea(p.Add(dir.Step)))
				{
					adjacencies.Add(dir);
				}
			}

			if (adjacencies.Count == 0) continue;

			var items = new List<ShellItem>();
			var hasCardinal = adjacencies.Any(dir => dir.IsCardinal);

			foreach (var direction in adjacencies)
			{
				if (hasCardinal && direction.IsOrdinal) continue;
				items.Add(new ShellItem { XZ = p, InsideDirection = direction });
			}

			if (items.Count > 0)
			{
				shellPointsMap[p] = items;
			}
		}

		// Pass 2: Manufacture inside corners wherever 2 (or more) cardinals occupy the same XZ
		foreach (var p in shellPointsMap.Keys)
		{
			var items = shellPointsMap[p];
			var cardinals = items.Where(i => i.InsideDirection.IsCardinal).Select(i => i.InsideDirection).ToHashSet();

			if (cardinals.Count >= 2)
			{
				if (cardinals.Contains(Direction.North) && cardinals.Contains(Direction.East))
					items.Add(new ShellItem { XZ = p, InsideDirection = Direction.NorthEast });
				if (cardinals.Contains(Direction.East) && cardinals.Contains(Direction.South))
					items.Add(new ShellItem { XZ = p, InsideDirection = Direction.SouthEast });
				if (cardinals.Contains(Direction.South) && cardinals.Contains(Direction.West))
					items.Add(new ShellItem { XZ = p, InsideDirection = Direction.SouthWest });
				if (cardinals.Contains(Direction.West) && cardinals.Contains(Direction.North))
					items.Add(new ShellItem { XZ = p, InsideDirection = Direction.NorthWest });
			}
		}

		return shellPointsMap;
	}
}
