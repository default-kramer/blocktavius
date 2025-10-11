using Blocktavius.Core;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.ScriptNodes.HillDesigners;

public sealed class HillDesignContext
{
	public required IAreaVM AreaVM { get; init; }

	public required PRNG Prng { get; init; }

	public required int Elevation { get; init; }

	public required ushort FillBlockId { get; init; } // TODO make this an IBlockProvider or something...

	public required XZ ImageCoordTranslation { get; init; } // TODO would be great to make this transparent...
}
