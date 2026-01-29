using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2.RepairSea;

/// <remarks>
/// This interface exists because making the "No Liquid" (remove sea) case
/// into a full-fledged <see cref="LiquidFamily"/> seems like a bad idea.
/// </remarks>
public interface ILiquid
{
	ushort BlockIdSubsurface { get; }
	ushort BlockIdSurfaceHigh { get; }
	ushort BlockIdSurfaceLow { get; }

	Block ChangePropShell(ref Block.Prop prop, LiquidAmountIndex amount);
}
