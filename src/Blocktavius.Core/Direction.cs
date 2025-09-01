using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

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
	private Direction() { }

	public static readonly Direction North = new() { Step = new XZ(0, -1) };
	public static readonly Direction East = new() { Step = new XZ(1, 0) };
	public static readonly Direction South = new() { Step = new XZ(0, 1) };
	public static readonly Direction West = new() { Step = new XZ(-1, 0) };
	public static readonly Direction NorthEast = new() { Step = new XZ(1, -1) };
	public static readonly Direction SouthEast = new() { Step = new XZ(1, 1) };
	public static readonly Direction SouthWest = new() { Step = new XZ(-1, 1) };
	public static readonly Direction NorthWest = new() { Step = new XZ(-1, -1) };

	public required XZ Step { get; init; }

	public bool IsCardinal => Step.X == 0 || Step.Z == 0;
	public bool IsOrdinal => !IsCardinal;

	public static IEnumerable<Direction> CardinalDirections()
	{
		yield return North;
		yield return East;
		yield return South;
		yield return West;
	}

	private static Direction ParseAny(int direction)
	{
		switch (direction)
		{
			case (int)CardinalDirection.North: return North;
			case (int)CardinalDirection.East: return East;
			case (int)CardinalDirection.South: return South;
			case (int)CardinalDirection.West: return West;
			case (int)OrdinalDirection.NorthEast: return NorthEast;
			case (int)OrdinalDirection.SouthEast: return SouthEast;
			case (int)OrdinalDirection.SouthWest: return SouthWest;
			case (int)OrdinalDirection.NorthWest: return NorthWest;
			default: throw new ArgumentException($"invalid direction: {direction}");
		}
	}

	public static Direction Parse(CardinalDirection direction)
	{
		var result = ParseAny((int)direction);
		if (!result.IsCardinal)
		{
			throw new ArgumentException($"invalid cardinal direction: {direction}");
		}
		return result;
	}

	public static Direction Parse(OrdinalDirection direction)
	{
		var result = ParseAny((int)direction);
		if (!result.IsOrdinal)
		{
			throw new ArgumentException($"invalid ordinal direction: {direction}");
		}
		return result;
	}
}
