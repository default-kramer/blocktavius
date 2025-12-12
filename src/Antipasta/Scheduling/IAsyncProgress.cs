using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta.Scheduling;

public interface IAsyncProgress
{
	INode SourceNode { get; }

	/// <summary>
	/// Called synchronously from the UI thread.
	/// The <see cref="PropagationResult"/> returned indicates whether anything has
	/// changed since the previous progress report.
	/// </summary>
	PropagationResult LatestProgressReport();
}
