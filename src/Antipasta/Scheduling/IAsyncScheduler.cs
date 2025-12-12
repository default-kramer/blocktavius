using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta.Scheduling;

public interface IAsyncScheduler
{
	IWaitableUnblocker CreateUnblocker(); // UI thread

	ITaskWrapper RunTask(Task task, CancellationTokenSource cts); // UI thread

	void RunUnblockedContinuation(Action continuation); // any thread

	void DispatchProgress(IAsyncProgress progress); // background thread
}
