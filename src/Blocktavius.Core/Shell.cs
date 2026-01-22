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
public readonly record struct ShellItem
{
	/// <summary>
	/// This XZ is *not* inside the area.
	/// </summary>
	public required XZ XZ { get; init; }

	/// <summary>
	/// Taking one step in this direction from <see cref="XZ"/> puts you inside the area.
	/// </summary>
	public required Direction InsideDirection { get; init; }

	public required CornerType CornerType { get; init; }
}

public sealed record Shell
{
	/// <summary>
	/// Identifies the points belonging to a single "island" from the <see cref="OriginalArea"/>.
	/// Every island has exactly one shell having <see cref="IsHole"/>==false,
	/// and may have any number shells with IsHole==true.
	/// (Or: every island must be enclosed by exactly one ocean,
	///  and may enclose any number of lakes.)
	/// </summary>
	public required I2DSampler<bool> IslandArea { get; init; }

	public required IReadOnlyList<ShellItem> ShellItems { get; init; }

	public required bool IsHole { get; init; }
}

public interface IArea
{
	Rect Bounds { get; }
	bool InArea(XZ xz);

	bool Intersects(Rect other) => !Bounds.Intersection(other).IsZero;

	// Hmm....
	I2DSampler<bool> AsSampler() => new AreaSampler { Area = this };
}

sealed class AreaSampler : I2DSampler<bool>
{
	public required IArea Area { get; init; }

	public Rect Bounds => Area.Bounds;

	public bool Sample(XZ xz) => Area.InArea(xz);
}

public static class ShellLogic
{
	/// <summary>
	/// The metaphor here is walking around the area, keeping your right hand on the wall.
	/// So insideDir is also the direction your right hand is pointing.
	/// Also, insideDir will always be a cardinal direction because corners do not get
	/// their own dedicated state.
	/// </summary>
	readonly record struct WalkState(XZ shellPosition, Direction insideDir)
	{
		public WalkState Advance(I2DSampler<bool> area, List<ShellItem> itemCollector)
		{
			itemCollector.Add(new ShellItem()
			{
				InsideDirection = insideDir,
				XZ = shellPosition,
				CornerType = CornerType.None,
			});

			var aheadDir = insideDir.TurnLeft90;
			var aheadPos = shellPosition.Add(aheadDir.Step);
			if (area.InArea(aheadPos))
			{
				// inside corner, stay at the same position and turn left
				var diagonalDir = insideDir.TurnLeft45;
				if (area.InArea(shellPosition.Step(diagonalDir)))
				{
					itemCollector.Add(new ShellItem()
					{
						InsideDirection = diagonalDir,
						XZ = shellPosition,
						CornerType = CornerType.Inside,
					});
				}
				return new WalkState(shellPosition, aheadDir);
			}
			else if (!area.InArea(aheadPos.Add(insideDir.Step)))
			{
				// outside corner
				var diagonalDir = insideDir.TurnRight45;
				if (area.InArea(aheadPos.Step(diagonalDir)))
				{
					itemCollector.Add(new ShellItem()
					{
						InsideDirection = diagonalDir,
						XZ = aheadPos, // The XZ for outside corners is the "inner" point
						CornerType = CornerType.Outside,
					});
				}
				return new WalkState(aheadPos.Add(insideDir.Step), insideDir.TurnRight90);
			}
			else
			{
				// no corner
				return new WalkState(aheadPos, insideDir);
			}
		}
	}

	private static WalkState FindFirstWalkState(I2DSampler<bool> area, XZ pointInArea)
	{
		if (!area.InArea(pointInArea))
		{
			throw new ArgumentException($"Not in area: {pointInArea}");
		}

		// Cast 4 rays in each cardinal direction and use the first one that reaches the outside of the area.
		(XZ xz, Direction dir)[] rays = Direction.CardinalDirections().Select(dir => (pointInArea, dir)).ToArray();

		for (int eStop = 0; eStop < 10000; eStop++)
		{
			for (int i = 0; i < rays.Length; i++)
			{
				var ray = rays[i];
				if (!area.InArea(ray.xz))
				{
					return new WalkState(ray.xz, ray.dir.Turn180);
				}
				rays[i] = (ray.xz.Step(ray.dir), ray.dir);
			}
		}
		throw new ArgumentException("Given area is too big (maybe infinite?)");
	}

	public static List<ShellItem> WalkShellFromPoint(I2DSampler<bool> area, XZ pointInArea)
	{
		var startState = FindFirstWalkState(area, pointInArea);
		var items = new List<ShellItem>();
		var current = startState;
		do
		{
			current = current.Advance(area, items);
		}
		while (current != startState);

		return items;
	}

	static bool TryBuildShell(I2DSampler<bool> area, IslandInfo island, out List<ShellItem> items)
	{
		items = new();
		if (island.MustIncludeStates.Count == 0)
		{
			return false;
		}

		var start = island.MustIncludeStates
			.OrderBy(s => s.shellPosition.Z)
			.ThenBy(s => s.shellPosition.X)
			.First();

		var current = start;
		do
		{
			island.MustIncludeStates.Remove(current);
			current = current.Advance(area, items);
		}
		while (current != start);
		return true;
	}

	private static readonly Direction[] allDirections = new[]
	{
		Direction.North, Direction.NorthEast, Direction.East, Direction.SouthEast,
		Direction.South, Direction.SouthWest, Direction.West, Direction.NorthWest
	};

	private static readonly Direction[] cardinalDirections = allDirections.Where(d => d.IsCardinal).ToArray();

	/// <summary>
	/// Most of this logic must use the expanded bounds, since shell items
	/// can be outside of the area's bounds.
	/// </summary>
	private static Rect ExpandBounds<T>(I2DSampler<T> area) => area.Bounds.Expand(1);

	public static IReadOnlyList<Shell> ComputeShells(IArea area) => ComputeShells(area.AsSampler());

	public static IReadOnlyList<Shell> ComputeShells(I2DSampler<bool> area)
	{
		List<Shell> shells = new();

		var islands = FindIslands(area);
		if (islands.Count == 0)
		{
			return shells;
		}

		var outsidePoints = ComputeOutsidePoints(area); // For hole detection

		foreach (var island in islands)
		{
			while (TryBuildShell(area, island, out var items))
			{
				shells.Add(new Shell()
				{
					IslandArea = island.IslandArea,
					ShellItems = items,
					IsHole = !outsidePoints.Contains(items[0].XZ),
				});
			}
		}

		return shells;
	}

	sealed class IslandInfo
	{
		public required int IslandId { get; init; }

		/// <summary>
		/// Also for hole detection. If we build a shell and didn't cover all
		/// of these states, there must be another hole in the island.
		/// </summary>
		public required HashSet<WalkState> MustIncludeStates { get; init; }

		public required I2DSampler<bool> IslandArea { get; init; }
	}

	private static List<IslandInfo> FindIslands(I2DSampler<bool> area)
	{
		List<IslandInfo> infos = new();

		const int Empty = -1;
		var islandFullMap = new MutableArray2D<int>(area.Bounds, Empty);

		foreach (var xz in area.Bounds.Enumerate().Where(area.InArea))
		{
			if (islandFullMap.Sample(xz) == Empty)
			{
				int islandId = infos.Count + 1;
				HashSet<WalkState> mustIncludeStates = new();

				var islandBounds = new Rect.BoundsFinder();
				islandFullMap.Put(xz, islandId);
				islandBounds.Include(xz);

				var queue = new Queue<XZ>();
				queue.Enqueue(xz);
				while (queue.Count > 0)
				{
					var current = queue.Dequeue();
					foreach (var dir in allDirections)
					{
						var neighbor = current.Add(dir.Step);
						if (area.InArea(neighbor))
						{
							if (islandFullMap.Sample(neighbor) == Empty)
							{
								islandFullMap.Put(neighbor, islandId);
								islandBounds.Include(neighbor);
								queue.Enqueue(neighbor);
							}
						}
						else if (dir.IsCardinal)
						{
							// Walk states "skip over" ordinal dirs (corners).
							// Even if that weren't true, we still wouldn't want to include the diagonals
							// that are technically present on straightaways.
							mustIncludeStates.Add(new WalkState(neighbor, dir.Turn180));
						}
					}
				}

				var islandArea = islandFullMap
					.Crop(islandBounds.CurrentBounds() ?? throw new Exception("assert fail"))
					.Project(i => i == islandId);

				var island = new IslandInfo
				{
					IslandId = islandId,
					MustIncludeStates = mustIncludeStates,
					IslandArea = islandArea,
				};
				infos.Add(island);
			}
		}

		return infos;
	}

	/// <summary>
	/// Finds all points that are not inside the area which can "escape" to the border.
	/// Used for hole detection.
	/// </summary>
	private static HashSet<XZ> ComputeOutsidePoints(I2DSampler<bool> area)
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
			foreach (var direction in cardinalDirections)
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
}
