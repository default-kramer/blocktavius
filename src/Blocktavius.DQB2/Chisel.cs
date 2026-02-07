using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

/// <summary>
/// Diagonal and Concave values are in reference to the direction the builder
/// must be facing to apply that chisel in-game.
/// For example, DiagonalEast means "the builder uses the Diagonal chisel while
/// facing East" and a ball placed on the resulting slope would roll West.
/// </summary>
public enum Chisel : ushort
{
	None = 0,

	DiagonalNorth = 0x1000,
	DiagonalEast = 0x7000,
	DiagonalSouth = 0x5000,
	DiagonalWest = 0x3000,

	DiagonalSouthWest = 0x4000,
	DiagonalSouthEast = 0x6000,
	DiagonalNorthWest = 0x2000,
	DiagonalNorthEast = 0x8000,

	// UNVERIFIED AND PROBABLY WRONG:
	/*
	ConcaveNorthwest = 0x9000,
	ConcaveSouthwest = 0xa000,
	ConcaveSoutheast = 0xb000,
	ConcaveNortheast = 0xc000,
	*/

	TopHalf = 0xd000,
	BottomHalf = 0xe000,
}
