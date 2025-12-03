using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Antipasta;

/// <summary>
/// An awaitable struct that signals an <see cref="IUnblocker"/> and then switches execution to a ThreadPool thread.
/// </summary>
public readonly struct ContextUnblocker
{
	private readonly IUnblocker unblocker;

	internal ContextUnblocker(IUnblocker unblocker)
	{
		this.unblocker = unblocker;
	}

	public Awaiter GetAwaiter()
	{
		unblocker.Unblock();
		return new Awaiter();
	}

	public readonly struct Awaiter : ICriticalNotifyCompletion
	{
		public bool IsCompleted => false;

		public void GetResult() { }

		public void OnCompleted(Action continuation)
		{
			ThreadPool.QueueUserWorkItem(_ => continuation(), null);
		}

		public void UnsafeOnCompleted(Action continuation)
		{
			ThreadPool.QueueUserWorkItem(_ => continuation(), null);
		}
	}
}
