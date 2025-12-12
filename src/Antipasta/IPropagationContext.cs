using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

/// <remarks>
/// NOTE - This interface is readonly by design.
/// The plan is that nodes should report their <see cref="PropagationResult"/>
/// and then the machinery will use <see cref="GraphManager.GetListeners"/> to merge
/// all those listeners into the propagation queue.
/// </remarks>
public interface IPropagationContext
{
	Internalized<Scheduling.IAsyncScheduler> AsyncScheduler { get; }

	Progress SetupProgress { get; }

	Progress PropagationProgress { get; }

	/// <summary>
	/// True while the <see cref="INodeGroup.OnChanged(IImmediateNotifyNode)"/> callback is being executed.
	/// Can be used to detect, for example, the situation where you raise a notification that an ItemsSource
	/// property has changed and WPF attempts to set a SelectedItem property to null.
	/// </summary>
	bool IsNotifying { get; }
}
