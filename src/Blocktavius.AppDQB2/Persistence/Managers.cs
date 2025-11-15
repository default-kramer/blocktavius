using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.Persistence;

public interface IAreaManager
{
	IAreaVM? FindArea(string? persistentId);
}

public interface IBlockManager
{
	IBlockProviderVM? FindBlock(string? persistentId);
}
