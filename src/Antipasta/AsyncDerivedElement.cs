using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

public interface IAsyncContext<TOutput> where TOutput : class
{
	ContextUnblocker UnblockAsync();
	void UpdateValue(TOutput? output);
	CancellationToken CancellationToken { get; }
}

public interface IAsyncComputation<TInput, TOutput>
	where TInput : IEquatable<TInput>
	where TOutput : class
{
	static abstract Task Compute(IAsyncContext<TOutput> context, TInput input);
}

public abstract class AsyncDerivedElement<TComputer, TInput, TOutput> : BaseNode, IElement<TOutput?>, IAsyncElement
	where TComputer : IAsyncComputation<TInput, TOutput>
	where TInput : IEquatable<TInput>
	where TOutput : class // value must be nullable... or we have to accept an InitialValue parameter
{
	private (TInput input, TOutput? output)? currentValue;
	private (TInput input, ITaskWrapper taskWrapper, AsyncContext context)? mostRecentTask;

	public TOutput? Value => currentValue.GetValueOrDefault().output;
	object? IElementUntyped.UntypedValue => Value;
	Type IElementUntyped.ElementType => typeof(TOutput);
	protected virtual TimeSpan? AutoUnblockTimeout => null;

	protected TElement ListenTo<TElement>(TElement element) where TElement : IElementUntyped
	{
		element.GraphManager.AddListener(this);
		return element;
	}

	protected abstract TInput BuildInput();

	protected virtual bool IsStale(TInput oldInput, TInput freshInput) => !oldInput.Equals(freshInput);

	public sealed override PropagationResult OnPropagation(IPropagationContext context)
	{
		// Propagation should *always* occur on the UI thread

		var input = BuildInput();
		bool isStale;
		if (mostRecentTask.HasValue)
		{
			isStale = IsStale(mostRecentTask.Value.input, input);
			if (isStale)
			{
				mostRecentTask.Value.context.Cancel();
				mostRecentTask.Value.taskWrapper.AttemptCancel();
				mostRecentTask = null;
			}
		}
		else if (currentValue == null)
		{
			isStale = true;
		}
		else
		{
			isStale = IsStale(currentValue.Value.input, input);
		}

		if (!isStale)
		{
			return PropagationResult.None;
		}

		var cts = new CancellationTokenSource();
		var unblocker = context.AsyncScheduler.Value.CreateUnblocker();
		var asyncContext = new AsyncContext(input, context.AsyncScheduler.Value, unblocker, this, cts.Token);

		// Per https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/consuming-the-task-based-asynchronous-pattern
		//    When an asynchronous method is called, it synchronously executes the body of the function
		//    up until the first await expression on an awaitable instance that has not yet completed,
		//    at which point the invocation returns to the caller.
		// So let's run the synchronous part:
		var task = DoRecompute(asyncContext, input);

		// And maybe it completed synchronously:
		if (task.IsCompleted)
		{
			return HandleSyncResult(asyncContext.SynchronousCapture(), input)
				?? PropagationResult.None;
		}

		var taskWrapper = context.AsyncScheduler.Value.RunTask(task, cts);
		unblocker.Spinwait(AutoUnblockTimeout);

		var capture = asyncContext.SynchronousCapture();
		if (!capture.IsTaskComplete)
		{
			mostRecentTask = (input, taskWrapper, asyncContext);
		}

		return HandleSyncResult(capture, input)
			?? PropagationResult.AsyncNone;
	}

	private PropagationResult? HandleSyncResult(SyncCapture capture, TInput freshInput)
	{
		if (capture.HasResult)
		{
			currentValue = (freshInput, capture.Result);
			return PropagationResult.Changed;
		}
		return null;
	}

	private PropagationResult HandleAsyncResult(TInput input, TOutput? output) // UI thread
	{
		if (object.ReferenceEquals(output, Value))
		{
			return PropagationResult.None;
		}

		currentValue = (input, output);
		return PropagationResult.Changed;
	}

	private async Task DoRecompute(AsyncContext context, TInput input)
	{
		try
		{
			await TComputer.Compute(context, input).ConfigureAwait(false);
		}
		finally
		{
			try
			{
				context.MarkComplete();
			}
			finally
			{
				context.UnblockNow();
			}
		}
	}

	internal sealed class SyncCapture
	{
		public required bool HasResult { get; init; }
		public required TOutput? Result { get; init; }
		public required bool IsTaskComplete { get; init; }
	}

	public sealed class AsyncContext : IAsyncContext<TOutput>, IAsyncProgress
	{
		public CancellationToken CancellationToken { get; }
		private readonly TInput input;
		private readonly IAsyncScheduler scheduler;
		private readonly IUnblocker unblocker;
		private readonly AsyncDerivedElement<TComputer, TInput, TOutput> node;
		private readonly SemaphoreSlim sema = new(1, 1);
		private (int updateCounter, TOutput? result) result = (0, default!);
		private volatile int finalResultUpdateCounter = NOTHING;
		private volatile int synchronouslyCapturedUpdateCounter = NOTHING;
		const int NOTHING = -1; // A null updateCounter (valid values start at 0)
		private bool isCanceled = false; // UI thread only

		public AsyncContext(TInput input, IAsyncScheduler scheduler, IUnblocker unblocker,
			AsyncDerivedElement<TComputer, TInput, TOutput> node, CancellationToken cancellationToken)
		{
			this.input = input;
			this.scheduler = scheduler;
			this.unblocker = unblocker;
			this.node = node;
			this.CancellationToken = cancellationToken;
		}

		public void Cancel() => isCanceled = true; // UI thread

		public ContextUnblocker UnblockAsync()
		{
			return new ContextUnblocker(scheduler, unblocker);
		}

		internal void UnblockNow() => unblocker.Unblock();

		public void UpdateValue(TOutput? output) // background thread
		{
			sema.Wait();
			try
			{
				int updateCounter = result.updateCounter + 1;
				result = (updateCounter, output);

				if (synchronouslyCapturedUpdateCounter > NOTHING && updateCounter > synchronouslyCapturedUpdateCounter)
				{
					scheduler.DispatchProgress(this);
				}
			}
			finally { sema.Release(); }
		}

		internal void MarkComplete() // background thread
		{
			if (finalResultUpdateCounter > NOTHING)
			{
				throw new InvalidOperationException("Assert fail - can't call this twice");
			}

			sema.Wait();
			finalResultUpdateCounter = result.updateCounter;
			sema.Release();
		}



		internal SyncCapture SynchronousCapture() // UI thread
		{
			if (synchronouslyCapturedUpdateCounter > NOTHING)
			{
				throw new InvalidOperationException("Assert fail - can't call this twice");
			}

			sema.Wait();
			try
			{
				int updateCounter = result.updateCounter;
				synchronouslyCapturedUpdateCounter = updateCounter;
				return new SyncCapture
				{
					HasResult = updateCounter > 0,
					Result = result.result,
					IsTaskComplete = finalResultUpdateCounter == updateCounter,
				};
			}
			finally { sema.Release(); }
		}

		INode IAsyncProgress.SourceNode => node;
		PropagationResult IAsyncProgress.Start() // UI thread
		{
			if (isCanceled) { return PropagationResult.None; }

			return node.HandleAsyncResult(this.input, result.result);
		}
	}
}
