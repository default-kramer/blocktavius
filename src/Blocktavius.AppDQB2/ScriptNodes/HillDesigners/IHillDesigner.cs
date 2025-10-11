using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.ScriptNodes.HillDesigners;

// This interface should be plugin-friendly I think
public interface IHillDesigner
{
	StageMutation? CreateMutation(HillDesignContext context);
}
