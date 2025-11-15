using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.Persistence;

public sealed class ScriptDeserializationContext
{
	public required IAreaManager AreaManager { get; init; }
	public required IBlockManager BlockManager { get; init; }
}
