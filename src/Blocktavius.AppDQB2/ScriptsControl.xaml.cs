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

		private void PreviewScriptButton_Click(object sender, RoutedEventArgs e)
		{
			var mainWindow = this.VisualTreeAncestors().OfType<MainWindow>().FirstOrDefault();
			mainWindow?.DoPreview();
		}

		private void RunScriptButton_Click(object sender, RoutedEventArgs e)
		{
			var project = this.DataContext as ProjectVM;
			if (project != null && project.SelectedScript != null)
			{
				var dialog = new PlanScriptDialog();
				dialog.ShowDialog(project);
			}
		}
	}
}
