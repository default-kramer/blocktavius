using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.ScriptNodes.CliffDesigners;

public sealed record CliffDesignContext
{
	public required PositionedJaunt PositionedJaunt { get; init; }

	public required PRNG Prng { get; init; }
}
