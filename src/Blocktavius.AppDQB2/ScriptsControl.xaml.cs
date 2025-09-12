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
using Xceed.Wpf.Toolkit.PropertyGrid;

namespace Blocktavius.AppDQB2
{
	/// <summary>
	/// Interaction logic for ScriptsControl.xaml
	/// </summary>
	public partial class ScriptsControl : UserControl
	{
		public ScriptsControl()
		{
			InitializeComponent();

			lvScripts.SetBinding(ListView.ItemsSourceProperty, nameof(ProjectVM.Scripts));
			lvScripts.DisplayMemberPath = nameof(ScriptVM.Name);

			icScriptContent.SetBinding(DataContextProperty, nameof(ProjectVM.SelectedScript));
			icScriptContent.SetBinding(ItemsControl.ItemsSourceProperty, nameof(ScriptVM.Nodes));

			propGrid.SetBinding(PropertyGrid.SelectedObjectProperty, nameof(ProjectVM.SelectedScriptNode));
		}

		private void Border_MouseDown(object sender, MouseButtonEventArgs e)
		{
			var node = (sender as FrameworkElement)?.DataContext as ScriptNodeVM;
			var project = this.DataContext as ProjectVM;

			if (node != null && project != null)
			{
				project.UpdateSelectedScriptNode(node);
			}
		}

		private void lvScripts_MouseDown(object sender, MouseButtonEventArgs e)
		{
			(this.DataContext as ProjectVM)?.OnScriptListViewClicked();
		}
	}
}
