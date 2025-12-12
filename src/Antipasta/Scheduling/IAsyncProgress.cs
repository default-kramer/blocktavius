using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta.Scheduling;

public interface IAsyncProgress
{
	INode SourceNode { get; }

	PropagationResult Start(); // UI thread
}
