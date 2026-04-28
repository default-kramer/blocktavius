using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.ScriptNodes.CliffDesigners;

public interface ICliffDesigner
{
	StageMutation? CreateMutation(CliffDesignContext context);

	Persistence.IPersistentCliffDesigner ToPersistModel();
}
