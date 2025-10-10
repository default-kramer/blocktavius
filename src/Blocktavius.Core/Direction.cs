using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

// These values are chosen such that -1 is a 45 degree turn left and +1 is a 45 degree turn right.
public enum CardinalDirection
{
	North = 1,
	East = 3,
	South = 5,
	West = 7,
};

public enum OrdinalDirection
{
	NorthEast = 2,
	SouthEast = 4,
	SouthWest = 6,
	NorthWest = 8,
};

public sealed class Direction
{
	private readonly int index;
	private readonly string name;

	private Direction(CardinalDirection cardinal)
	{
		this.index = GetIndex(cardinal);
		this.name = Enum.GetName(typeof(CardinalDirection), cardinal) ?? cardinal.ToString();
	}
	private Direction(OrdinalDirection ordinal)
	{
		this.index = GetIndex(ordinal);
		this.name = Enum.GetName(typeof(OrdinalDirection), ordinal) ?? ordinal.ToString();
	}

	public static readonly Direction North = new(CardinalDirection.North) { Step = new(0, -1) };
	public static readonly Direction East = new(CardinalDirection.East) { Step = new(1, 0) };
	public static readonly Direction South = new(CardinalDirection.South) { Step = new(0, 1) };
	public static readonly Direction West = new(CardinalDirection.West) { Step = new(-1, 0) };
	public static readonly Direction NorthEast = new(OrdinalDirection.NorthEast) { Step = new(1, -1) };
	public static readonly Direction SouthEast = new(OrdinalDirection.SouthEast) { Step = new(1, 1) };
	public static readonly Direction SouthWest = new(OrdinalDirection.SouthWest) { Step = new(-1, 1) };
	public static readonly Direction NorthWest = new(OrdinalDirection.NorthWest) { Step = new(-1, -1) };

	const int count = 8;
	private static readonly IReadOnlyList<Direction> lookup;

	private static int GetIndex(CardinalDirection direction) => (int)direction - 1;
	private static int GetIndex(OrdinalDirection direction) => (int)direction - 1;

	static Direction()
	{
		IReadOnlyList<Direction> init(params Direction[] allDirs)
		{
			var array = new Direction[count];
			foreach (var dir in allDirs)
			{
				array[dir.index] = dir;
			}
			return array;
		}

		lookup = init(North, East, South, West, NorthEast, SouthEast, SouthWest, NorthWest);
	}

	public required XZ Step { get; init; }

	public Direction TurnLeft90 => lookup[(index + count - 2) % count];
	public Direction TurnLeft45 => lookup[(index + count - 1) % count];
	public Direction TurnRight45 => lookup[(index + 1) % count];
	public Direction TurnRight90 => lookup[(index + 2) % count];
	public Direction Turn180 => lookup[(index + 4) % count];


	public bool IsCardinal => Step.X == 0 || Step.Z == 0;
	public bool IsOrdinal => !IsCardinal;

	public static IEnumerable<Direction> CardinalDirections()
	{
		yield return North;
		yield return East;
		yield return South;
		yield return West;
	}

	private static Direction ParseAny<T>(int index, T direction)
	{
		if (index < 0 || index >= count)
		{
			throw new ArgumentException($"invalid direction: {direction}");
		}
		return lookup[index];
	}

	public static Direction Parse(CardinalDirection direction)
	{
		var result = ParseAny(GetIndex(direction), direction);
		if (!result.IsCardinal)
		{
			throw new ArgumentException($"invalid cardinal direction: {direction}");
		}
		return result;
	}

	public static Direction Parse(OrdinalDirection direction)
	{
		var result = ParseAny(GetIndex(direction), direction);
		if (!result.IsOrdinal)
		{
			throw new ArgumentException($"invalid ordinal direction: {direction}");
		}
		return result;
	}

	public override string ToString() => name;
}
