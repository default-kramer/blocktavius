using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

public enum PropagationResult
{
	/// <summary>
	/// Do not propagate to listeners, probably because this node was unchanged.
	/// </summary>
	None,

	/// <summary>
	/// The value of this node changed and propagation should include all listeners.
	/// </summary>
	Changed,

	/// <summary>
	/// An async operation was started, but there is no change that listeners could observe yet.
	/// </summary>
	AsyncNone,
}
