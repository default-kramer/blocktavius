using Antipasta;
using Antipasta.IndexedPropagation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2;

static class Pasta
{
	public static IChangeset NewChangeset()
	{
		return new Changeset { AsyncScheduler = AsyncSchedulerWPF.Instance };
	}

	private static IChangeset? currentChangeset = null;
	private static void WithChangeset(Action<IChangeset> action)
	{
		if (currentChangeset != null)
		{
			action(currentChangeset);
		}
		else
		{
			try
			{
				currentChangeset = NewChangeset();
				action(currentChangeset);
				currentChangeset.ApplyChanges();
			}
			finally
			{
				currentChangeset = null;
			}
		}
	}

	public static void SetElement<T>(ISettableElement<T> element, T value)
	{
		WithChangeset(changset => changset.RequestChange(element, value));
	}

	public static void SetUntypedElement(ISettableElementUntyped element, object? value)
	{
		WithChangeset(changeset => changeset.RequestChangeUntyped(element, value));
	}

	class AsyncSchedulerWPF : IAsyncScheduler
	{
		private AsyncSchedulerWPF() { }
		public static readonly AsyncSchedulerWPF Instance = new();

		public IWaitableUnblocker CreateUnblocker() => new Unblocker();

		public ITaskWrapper RunTask(Task task, CancellationTokenSource cts)
		{
			// Run on the UI thread until Unblock() is called, which will reach our RunUnblockedContinuation method
			return new TaskWrapper
			{
				Task = task,
				CancellationTokenSource = cts,
			};
		}

		public void RunUnblockedContinuation(Action continuation)
		{
			ThreadPool.QueueUserWorkItem(_ => continuation(), null);
		}

		sealed class Unblocker : IWaitableUnblocker
		{
			private volatile bool isUnblocked = false;

			public SpinwaitResult Spinwait(TimeSpan? timeout)
			{
				DateTime expiration = timeout.HasValue ? DateTime.UtcNow.Add(timeout.Value) : DateTime.MaxValue;
				while (!isUnblocked && DateTime.UtcNow < expiration) { }
				return isUnblocked ? SpinwaitResult.Unblocked : SpinwaitResult.Timeout;
			}

			public void Unblock() => isUnblocked = true;
		}

		sealed class TaskWrapper : ITaskWrapper
		{
			public required CancellationTokenSource CancellationTokenSource { get; init; }
			public required Task Task { get; init; }

			public void AttemptCancel()
			{
				try
				{
					CancellationTokenSource.Cancel();
				}
				catch (Exception) { }
			}
		}

		public void DispatchProgress(IAsyncProgress progress)
		{
			System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
			{
				WithChangeset(changset => changset.RequestChange(progress));
			});
		}
	}
}
