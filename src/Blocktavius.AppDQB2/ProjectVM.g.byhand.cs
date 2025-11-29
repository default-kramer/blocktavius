using Blocktavius.DQB2;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ViewmodelDeputy;

namespace Blocktavius.AppDQB2;

/*
 * INCOMPLETE HACKING HERE
 * The idea was to make Gemini or Claude stand in for a non-existent library (codename: ViewmodelDeputy)
 * which would include a source generator and analyzer.
 * If it proves to work well, then maybe actually implement said library.
 * But this yak is hairier than I thought...
 *
 * The goals are:
 * - Analyze property dependency chains.
 *   - Verify against any AssertDependsOn attributes
 *   - Raise notifications for all chained
 *   - Generate metadata in MyProperties class
 * - Support simple (synchronous) computed properties
 *   - Determine the Input (immediate dependencies) the same way as for all properties
 *   - Provide machinery to refresh only when stale
 * - Support async computed properties
 *   - Simplifying assumption: TResult is always nullable and starts with a null value
 *   - For safety: the async computation method should be static
 *   - User code must define the input into the async computation
 *   - The async computation SHOULD BLOCK THE UI THREAD until Unblock() is called!
 * - All of this should combine to give us something like one-way data flow
 *   - If a property has a setter, it is an "origin" property. It cannot depend on anything.
 *   - If a property depends (directly or indirectly) on an origin property, it is a "dependent" property.
 *   - If a property is neither origin nor dependent, it is assumed to be a constant
 *     and does not participate in update propagation.
 *   - Dependency chains are within a single VM only. Communication from one viewmodel to another
 *     should be via methods which set (probably private) origin properties.
 * - Snapshot tests for property dependencies
 * - Snapshot tests for bindable properties
 * - BIG TODO: How to handle properties that depend on an ObservableCollection?
 *   Or depend on any Foo.Bar expression, where Bar is mutable?
 *   (Maybe just warn/error? Do we need this?)
 */

partial class ProjectVM
{
	private static class MyProperties
	{
		private static string NOTE_TO_SELF() => nameof(DeputizedVMAttribute.MyPropertiesClassName); // defines how we should name this class

		private static readonly IReadonlyPropBuilder __propbuilder;
		public static IReadonlyPropBuilder GetPropBuilder() => __propbuilder; // to be used in inheritance situations??

		static MyProperties()
		{
			var propBuilder = new PropBuilder<ProjectVM>();

			ProjectFilePathToDisplay = propBuilder.Register(nameof(ProjectFilePathToDisplay))
				.Finish();

			Layers = propBuilder.Register(nameof(ProjectVM.Layers))
				.Finish();

			Scripts = propBuilder.Register(nameof(ProjectVM.Scripts))
				.Finish();

			CommandExportChunkMask = propBuilder.Register(nameof(ProjectVM.CommandExportChunkMask))
				.Finish();

			CommandExportMinimap = propBuilder.Register(nameof(ProjectVM.CommandExportMinimap))
				.Finish();

			IncludeStgdatInPreview = propBuilder.Register(nameof(ProjectVM.IncludeStgdatInPreview))
				.Finish();

			SelectedLayer = propBuilder.Register(nameof(ProjectVM.SelectedLayer))
				.Finish();

			Notes = propBuilder.Register(nameof(ProjectVM.Notes))
				.Finish();

			ChunkExpansion = propBuilder.Register(nameof(ProjectVM.ChunkExpansion))
				.Finish();

			Profile = propBuilder.Register(nameof(ProjectVM.Profile))
				.Finish();

			SourceSlots = propBuilder.Register(nameof(ProjectVM.SourceSlots))
				.DependsOn(nameof(ProjectVM.Profile))
				.Finish();

			DestSlots = propBuilder.Register(nameof(ProjectVM.DestSlots))
				.DependsOn(nameof(ProjectVM.Profile))
				.Finish();

			SelectedScript = propBuilder.Register(nameof(ProjectVM.SelectedScript))
				.Finish();

			SelectedSourceStage = propBuilder.Register(nameof(ProjectVM.SelectedSourceStage))
				.Finish();

			StgdatFilePath = propBuilder.Register(nameof(ProjectVM.StgdatFilePath))
				.DependsOn(nameof(ProjectVM.SelectedSourceStage))
				.Finish();

			LoadedStage = propBuilder.Register(nameof(ProjectVM.LoadedStage))
				.DependsOn(nameof(ProjectVM.StgdatFilePath))
				.Finish();

			SelectedSourceSlot = propBuilder.Register(nameof(ProjectVM.SelectedSourceSlot))
				.Finish();

			__propbuilder = propBuilder.Freeze();
		}

		public static readonly AnalyzedProperty ProjectFilePathToDisplay;
		public static readonly AnalyzedProperty Layers;
		public static readonly AnalyzedProperty Scripts;
		public static readonly AnalyzedProperty CommandExportChunkMask;
		public static readonly AnalyzedProperty CommandExportMinimap;
		public static readonly AnalyzedProperty IncludeStgdatInPreview;
		public static readonly AnalyzedProperty SelectedLayer;
		public static readonly AnalyzedProperty Notes;
		public static readonly AnalyzedProperty ChunkExpansion;
		public static readonly AnalyzedProperty Profile;
		public static readonly AnalyzedProperty SourceSlots;
		public static readonly AnalyzedProperty DestSlots;
		public static readonly AnalyzedProperty SelectedScript;
		public static readonly AnalyzedProperty SelectedSourceStage;
		public static readonly AnalyzedProperty StgdatFilePath;
		public static readonly AnalyzedProperty LoadedStage;
		public static readonly AnalyzedProperty SelectedSourceSlot;
	}

	private static class ComputationTypes
	{
		public sealed class SourceSlotsComputation : IComputation<SourceSlotsComputation.InputKey, IReadOnlyList<SlotVM>>
		{
			public sealed record InputKey
			{
				public required ProfileSettings Profile { get; init; }
			}

			public required ProjectVM Self { get; init; }

			public InputKey Input() => new InputKey { Profile = Self.Profile };

			public IReadOnlyList<SlotVM> Compute() => Self.ComputeSourceSlots();
		}

		public sealed class DestSlotsComputation : IComputation<DestSlotsComputation.InputKey, IReadOnlyList<WritableSlotVM>>
		{
			public sealed record InputKey
			{
				public required ProfileSettings Profile { get; init; }
			}

			public required ProjectVM Self { get; init; }

			public InputKey Input() => new InputKey { Profile = Self.Profile };

			public IReadOnlyList<WritableSlotVM> Compute() => Self.ComputeDestSlots();
		}
	}

	private bool ChangeProperty<T>(ref T field, T value, AnalyzedProperty prop)
	{
		return ChangeProperty(ref field, value, prop.PropertyName);
		// TODO - recompute dependencies, etc...
	}

	private readonly Computer<ComputationTypes.SourceSlotsComputation.InputKey, IReadOnlyList<SlotVM>> _sourceSlots = new();
	public IReadOnlyList<SlotVM> SourceSlots => _sourceSlots.RecomputeIfStale(new ComputationTypes.SourceSlotsComputation { Self = this });

	private readonly Computer<ComputationTypes.DestSlotsComputation.InputKey, IReadOnlyList<WritableSlotVM>> _destSlots = new();
	public IReadOnlyList<WritableSlotVM> DestSlots => _destSlots.RecomputeIfStale(new ComputationTypes.DestSlotsComputation { Self = this });

	private AsyncComputer<LoadedStageInput, StgdatLoader.LoadResult>? _loadedStageComputer;
	private StgdatLoader.LoadResult? LoadedStage
	{
		get
		{
			_loadedStageComputer = _loadedStageComputer ?? new(_GetLoadedStageAsync, () => _LoadedStageInput, _onLoadedStageChanged);
			return _loadedStageComputer.RecomputeIfStale();
		}
	}

	private void _onLoadedStageChanged(StgdatLoader.LoadResult? result)
	{
		// TODO notify
	}





	// === TODO move this to library ===

	/// <summary>
	/// Accept an argument of this type to declare "this method must only be called from the UI thread"
	/// </summary>
	/// <remarks>
	/// Making this a ref struct seems to prevent us from incorrectly passing this from one thread to another
	/// </remarks>
	ref struct UIThreadProof
	{
		public static UIThreadProof AssertOnUIThread() => new UIThreadProof(); // TODO
	}

	class UILock : IDisposable
	{
		private static readonly SemaphoreSlim semaphore = new(1, 1);

		private bool unlocked = false;
		private UILock() { }

		public static UILock? Null => null;

		public static UILock WaitForLock()
		{
			semaphore.Wait();
			return new UILock();
		}

		void IDisposable.Dispose()
		{
			if (!unlocked)
			{
				unlocked = true;
				try { semaphore.Release(); }
				catch (SemaphoreFullException) { }
			}
		}
	}

	interface IAsyncGetterContext<TInput, TResult>
		where TInput : class, IEquatable<TInput>
		where TResult : class
	{
		CancellationToken CancellationToken { get; }

		TInput Input { get; }

		void SetValue(TResult? result);

		void Unblock();

		// This could be nice maybe? Automatically unblock if the timeout
		// is reached before the async computation finishes.
		void UnblockAfter(TimeSpan timeout) => Unblock(); // TODO!
	}

	sealed class AsyncComputer<TInput, TResult>
		where TInput : class, IEquatable<TInput>
		where TResult : class
	{
		private readonly Func<IAsyncGetterContext<TInput, TResult>, Task> asyncComputeMethod;
		private readonly Func<TInput> _getFreshInput;
		private TInput GetFreshInput(UIThreadProof uiThreadProof) => _getFreshInput();
		private readonly Action<TResult?> onValueChanged;

		public AsyncComputer(Func<IAsyncGetterContext<TInput, TResult>, Task> asyncComputeMethod, Func<TInput> getFreshInput, Action<TResult?> onValueChanged)
		{
			this.asyncComputeMethod = asyncComputeMethod;
			this._getFreshInput = getFreshInput;
			this.onValueChanged = onValueChanged;
		}

		sealed class GetterContext : IAsyncGetterContext<TInput, TResult>
		{
			private readonly CancellationTokenSource canceler = new();

			public required TInput Input { get; init; }
			public required Action<GetterContext, TResult?> SetValueHandler { get; init; }
			public CancellationToken CancellationToken => canceler.Token;
			public bool IsCanceled { get; private set; }
			public bool WasUnblocked { get; private set; } = false;

			void IAsyncGetterContext<TInput, TResult>.SetValue(TResult? result)
			{
				SetValueHandler(this, result);
			}

			void IAsyncGetterContext<TInput, TResult>.Unblock()
			{
				WasUnblocked = true;
			}

			public void CancelIfStale(TInput freshInput, UIThreadProof ensureUIThread)
			{
				if (IsCanceled)
				{
					return;
				}

				bool isFresh = freshInput.Equals(this.Input);
				if (!isFresh)
				{
					IsCanceled = true;
					try { canceler.Cancel(); }
					catch (Exception) { }
				}
			}
		}

		private (TInput input, TResult? result)? current = null;
		private GetterContext? runningTask = null;

		public TResult? RecomputeIfStale()
		{
			RecomputeIfStale(UIThreadProof.AssertOnUIThread(), null);
			return current?.result;
		}

		private void RecomputeIfStale(UIThreadProof uiThreadProof, UILock? uiLock)
		{
			var input = GetFreshInput(uiThreadProof);

			if (runningTask != null)
			{
				runningTask.CancelIfStale(input, uiThreadProof);
				if (!runningTask.IsCanceled)
				{
					return; // still running and not stale
				}
				runningTask = null;
			}

			if (current.HasValue && input.Equals(current.Value.input))
			{
				return; // not stale
			}

			if (uiLock == null)
			{
				using var gotUiLock = UILock.WaitForLock();
				RecomputeIfStale(uiThreadProof, gotUiLock);
			}
			else
			{
				Recompute(input, uiThreadProof, uiLock);
			}
		}

		private void Recompute(TInput input, UIThreadProof uIThreadProof, UILock uiLockProof)
		{
			TResult? latestResult = null;

			var context = new GetterContext
			{
				Input = input,
				SetValueHandler = (me, result) =>
				{
					latestResult = result;
					_ = Application.Current?.Dispatcher?.InvokeAsync(() =>
					{
						if (result == latestResult)
						{
							ChangeAndNotify(input, result, UIThreadProof.AssertOnUIThread());
						}
					});
				},
			};

			try
			{
				var task = Task.Run(async () =>
				{
					await asyncComputeMethod(context);
				}, context.CancellationToken);

				// spinwait until the task finishes or calls Unblock()
				while (!(task.IsCompleted || context.WasUnblocked)) { }
			}
			catch (TaskCanceledException) { }
		}

		private void ChangeAndNotify(TInput input, TResult? result, UIThreadProof uiThreadProof)
		{
			current = (input, result);
			onValueChanged(result);
		}
	}
}
