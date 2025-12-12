using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta.Scheduling;

public interface IUnblocker
{
	/// <summary>
	/// When user code calls <see cref="IAsyncContext{TOutput}.UnblockAsync"/> this
	/// method is how the scheduler gets notified.
	/// </summary>
	void Unblock();
}
