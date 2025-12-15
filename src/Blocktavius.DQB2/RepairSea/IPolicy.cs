using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2.RepairSea;

interface IPolicy
{
	/// <summary>
	/// During sea detection, can this block join the sea (when adjacent)?
	/// </summary>
	bool CanBePartOfSea(ushort blockId);

	/// <summary>
	/// During sea replacement, should this block ID be replaced by the sea's liquid ID?
	/// </summary>
	bool ShouldOverwriteWhenPartOfSea(ushort blockId);
}
