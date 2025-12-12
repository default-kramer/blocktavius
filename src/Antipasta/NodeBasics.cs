using System.Collections.Immutable;

namespace Antipasta;

public interface IUnblocker
{
	void Unblock();
}

public enum SpinwaitResult
{
	Unknown,
	Unblocked,
	Timeout,
	SpinwaitNotSupported,
}

public interface IWaitableUnblocker : IUnblocker
{
	SpinwaitResult Spinwait(TimeSpan? timeout);
}

public interface IAsyncScheduler
{
	IWaitableUnblocker CreateUnblocker(); // UI thread

	ITaskWrapper RunTask(Task task, CancellationTokenSource cts); // UI thread

	void RunUnblockedContinuation(Action continuation); // any thread

	void DispatchProgress(IAsyncProgress progress); // background thread
}

public interface IAsyncProgress
{
	INode SourceNode { get; }

	PropagationResult Start(); // UI thread
}

public interface ITaskWrapper
{
	void AttemptCancel();

	Task Task { get; }
}
