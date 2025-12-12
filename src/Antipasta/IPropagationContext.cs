using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

public interface IPropagationContext
{
	/// <summary>
	/// This is internalized because only framework-ish code should access it.
	/// Application code should inherit from some base class that ensures the scheduler is used
	/// correctly (such as the <see cref="AsyncDerivedElement{TComputer, TInput, TOutput}"/>).
	/// </summary>
	Internalized<Scheduling.IAsyncScheduler> AsyncScheduler { get; }

	/// <summary>
	/// Phase 1 is Setup.
	/// This is when all requested changes (<see cref="IChangeset"/>) are being applied.
	/// </summary>
	Progress SetupProgress { get; }

	/// <summary>
	/// Phase 2 is Propagation.
	/// This is when change notifications travel to listeners.
	/// </summary>
	Progress PropagationProgress { get; }

	/// <summary>
	/// True while the <see cref="INodeGroup.NotifyPropertyChanged"/> callback is being executed.
	/// Can be used to detect, for example, the situation where you raise a notification that an ItemsSource
	/// property has changed and WPF attempts to set a SelectedItem property to null.
	/// (This can happen in both the Setup and the Propagation phases.)
	/// </summary>
	bool IsNotifying { get; }
}
