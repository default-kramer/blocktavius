using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

public record struct Point(XZ xz, int Y);

public record struct XZ(int X, int Z)
{
	public static XZ Zero => new XZ(0, 0);

	public XZ Add(int dx, int dz) => new XZ(X + dx, Z + dz);

	public XZ Add(XZ xz) => Add(xz.X, xz.Z);

	public XZ Step(Direction direction) => Add(direction.Step);

	public XZ Step(Direction direction, int steps) => Add(direction.Step.Scale(steps));

	public XZ Subtract(XZ xz) => new XZ(X - xz.X, Z - xz.Z);

	public XZ Scale(int factor) => new XZ(X * factor, Z * factor);

	public XZ Scale(XZ scale) => new XZ(X * scale.X, Z * scale.Z);

	public XZ Unscale(XZ scale) => new XZ(X / scale.X, Z / scale.Z);

	public IEnumerable<XZ> CardinalNeighbors()
	{
		yield return Add(1, 0);
		yield return Add(-1, 0);
		yield return Add(0, 1);
		yield return Add(0, -1);
	}

	public IEnumerable<XZ> OrdinalNeighbors()
	{
		yield return Add(-1, -1);
		yield return Add(1, -1);
		yield return Add(-1, 1);
		yield return Add(1, 1);
	}

	public IEnumerable<XZ> AllNeighbors() => CardinalNeighbors().Concat(OrdinalNeighbors());

	public IEnumerable<XZ> Walk(Direction direction, int steps)
	{
		var current = this;
		while (steps > 0)
		{
			yield return current;
			current = current.Step(direction);
			steps--;
		}
	}
}
