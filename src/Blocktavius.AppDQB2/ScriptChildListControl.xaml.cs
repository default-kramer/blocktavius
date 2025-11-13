using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Blocktavius.AppDQB2;

/// <summary>
/// Data Context should be a <see cref="ScriptNonleafNodeVM"/>.
/// Shows each child with standard wrapper functionality like move up/down and delete.
/// </summary>
public partial class ScriptChildListControl : UserControl
{
	public ScriptChildListControl()
	{
		InitializeComponent();
	}

	private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		var child = (sender as FrameworkElement)?.DataContext as IChildNodeWrapperVM;
		var project = this.DataContextAncestors().OfType<ProjectVM>().FirstOrDefault();
		if (child == null || project == null)
		{
			return;
		}
		project.UpdateSelectedScriptNode(child.Child);
	}
}
