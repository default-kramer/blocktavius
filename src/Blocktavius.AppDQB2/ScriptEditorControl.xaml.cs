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

public partial class ScriptEditorControl : UserControl
{
	public ScriptEditorControl()
	{
		InitializeComponent();
	}

	private void PreviewScriptButton_Click(object sender, RoutedEventArgs e)
	{
		var mainWindow = this.VisualTreeAncestors().OfType<MainWindow>().FirstOrDefault();
		mainWindow?.DoPreview();
	}

	private void RunScriptButton_Click(object sender, RoutedEventArgs e)
	{
		var project = this.DataContextAncestors().OfType<ProjectVM>().FirstOrDefault();
		if (project != null && project.SelectedScript != null)
		{
			var dialog = new PlanScriptDialog();
			dialog.ShowDialog(project);
		}
	}
}
