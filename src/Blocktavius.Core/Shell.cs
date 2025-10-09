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
/// The XZ will get three shell items, for example East, SouthEast, and South.
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
	/// This XZ is not inside the area.
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
		var outsidePoints = ComputeOutsidePoints(area);
		var shellPointsMap = FindAllShellItems(area);

		// Step 3: Group shells and determine IsHole.
		var searchBounds = ExpandBounds(area);
		var resultShells = new List<Shell>();
		var visitedShellPoints = new HashSet<XZ>();

		foreach (var startPoint in shellPointsMap.Keys)
		{
			if (visitedShellPoints.Contains(startPoint))
			{
				continue;
			}

			var currentComponentItems = new List<ShellItem>();
			var componentQueue = new Queue<XZ>();

			componentQueue.Enqueue(startPoint);
			visitedShellPoints.Add(startPoint);

			while (componentQueue.Count > 0)
			{
				var currentPoint = componentQueue.Dequeue();
				currentComponentItems.AddRange(shellPointsMap[currentPoint]);

				foreach (var direction in allDirections)
				{
					var neighbor = currentPoint.Add(direction.Step);
					if (shellPointsMap.ContainsKey(neighbor) && visitedShellPoints.Add(neighbor))
					{
						componentQueue.Enqueue(neighbor);
					}
				}
			}

			bool isHole = !outsidePoints.Contains(startPoint);

			resultShells.Add(new Shell
			{
				Area = area,
				ShellItems = currentComponentItems,
				IsHole = isHole
			});
		}

		return resultShells;
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

		for (int z = searchBounds.start.Z + 1; z < searchBounds.end.Z - 1; z++)
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

		foreach (var xz in searchBounds.Enumerate())
		{
			if (area.InArea(xz))
			{
				continue;
			}

			List<ShellItem>? items = null;
			foreach (var direction in allDirections)
			{
				var neighbor = xz.Add(direction.Step);
				if (area.InArea(neighbor))
				{
					if (items == null)
					{
						items = new();
						shellPointsMap[xz] = items;
					}
					items.Add(new ShellItem { XZ = xz, InsideDirection = direction });
				}
			}
		}

		return shellPointsMap;
	}
}
