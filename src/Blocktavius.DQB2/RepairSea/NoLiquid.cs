using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2.RepairSea;

/// <summary>
/// Used to remove the sea.
/// </summary>
public sealed class NoLiquid : ILiquid
{
	private NoLiquid() { }
	public static readonly NoLiquid Instance = new();

	public ushort BlockIdSubsurface => 0;
	public ushort BlockIdSurfaceHigh => 0;
	public ushort BlockIdSurfaceLow => 0;

	public Block ChangePropShell(ref Block.Prop prop, LiquidAmountIndex amount)
	{
		return prop.ClearLiquid();
	}
}
