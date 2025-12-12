using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

/// <summary>
/// TODO - Rename this? It really just means "IViewmodel" assuming WPF.
/// </summary>
public interface INodeGroup
{
	void OnPropagationCompleted(IPropagationContext context);

	/// <summary>
	/// Called when the node's graph manager has a non-null <see cref="GraphManager.NotifyPropertyName"/>.
	/// </summary>
	void NotifyPropertyChanged(INode node);
}
