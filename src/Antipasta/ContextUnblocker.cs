using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Antipasta;

/// <summary>
/// An awaitable struct that signals an <see cref="IUnblocker"/> and then switches execution to a ThreadPool thread.
/// </summary>
public readonly struct ContextUnblocker
{
	private readonly IAsyncScheduler scheduler;
	private readonly IUnblocker unblocker;

	internal ContextUnblocker(IAsyncScheduler scheduler, IUnblocker unblocker)
	{
		this.scheduler = scheduler;
		this.unblocker = unblocker;
	}

	public Awaiter GetAwaiter()
	{
		unblocker.Unblock();
		return new Awaiter(scheduler);
	}

	public readonly struct Awaiter : ICriticalNotifyCompletion
	{
		private readonly IAsyncScheduler scheduler;
		internal Awaiter(IAsyncScheduler scheduler)
		{
			this.scheduler = scheduler;
		}

		public bool IsCompleted => false;

		public void GetResult() { }

		public void OnCompleted(Action continuation)
		{
			scheduler.RunUnblockedContinuation(continuation);
		}

		public void UnsafeOnCompleted(Action continuation)
		{
			scheduler.RunUnblockedContinuation(continuation);
		}
	}
}
