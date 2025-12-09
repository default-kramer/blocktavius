using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

public interface IChangeset
{
	IChangeset RequestChange<T>(ISettableElement<T> element, T value);

	IChangeset RequestChange(IAsyncProgress progress);

	void ApplyChanges();
}

readonly record struct Change(INode node, Func<IPropagationContext, PropagationResult> func);

public sealed class Changeset : IChangeset
{
	public required IAsyncScheduler AsyncScheduler { get; init; }
	private readonly Queue<Change> changes = new();
	private Propagator? propagator = null;

	/// <summary>
	/// A value of N allows N+1 total propagations.
	/// The "+1" is because the first propagation is not considered a sequel
	/// and is always allowed to execute.
	/// (Any N less than 0 will be treated as 0.)
	/// </summary>
	public int SequelLimit { get; set; } = 0;

	private Changeset Enqueue(INode node, Func<IPropagationContext, PropagationResult> func)
	{
		var change = new Change(node, func);
		if (propagator != null)
		{
			propagator.OnChangeRequestedDuringPropagation(change);
		}
		else
		{
			changes.Enqueue(change);
		}
		return this;
	}

	public IChangeset RequestChange<T>(ISettableElement<T> element, T value)
	{
		return Enqueue(element, ctx => element.AcceptSetValueRequest(ctx, value));
	}

	public IChangeset RequestChange(IAsyncProgress asyncProgress)
	{
		return Enqueue(asyncProgress.SourceNode, _ => asyncProgress.Start());
	}

	internal void EnqueueViaSequel(Change change)
	{
		if (propagator != null)
		{
			throw new Exception("Assert fail - this sequel's queue is already finalized");
		}
		changes.Enqueue(change);
	}

	public void ApplyChanges() => ApplyChanges(this);

	private Changeset CreateSequel() => new Changeset { AsyncScheduler = this.AsyncScheduler };

	private static readonly SemaphoreSlim oneAtATime = new(1, 1);
	private static void ApplyChanges(Changeset changeset)
	{
		// Only one propagation may be running at a time; this is non-negotiable.
		// User code is allowed to configure the sequel limit.
		// Any sequels will execute within the same acquisition of this semaphore.
		if (oneAtATime.Wait(0))
		{
			try
			{
				DoApplyChanges(changeset);
			}
			finally
			{
				oneAtATime.Release();
			}
		}
		else
		{
			throw new InvalidOperationException("Previous changeset has not completed");
		}
	}

	private static void DoApplyChanges(Changeset changeset)
	{
		int sequelCount = -1; // will be 0 on first changeset
		int sequelLimit = Math.Max(0, changeset.SequelLimit);

		while (true)
		{
			if (changeset.changes.Count == 0)
			{
				return;
			}
			sequelCount++;
			if (sequelCount > sequelLimit)
			{
				throw new InvalidOperationException($"Sequel limit of {sequelLimit} has been reached");
			}

			var sequel = changeset.CreateSequel();
			var propagator = Propagator.Create(changeset.AsyncScheduler, sequel);

			// Set this immediately so that any future calls to Enqueue anything go to the propagator instead
			changeset.propagator = propagator;

			propagator.Setup(changeset.changes);
			propagator.Propagate();

			changeset = sequel;
		}
	}
}
