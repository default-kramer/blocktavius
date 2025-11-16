using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
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

public partial class StartupControl : UserControl
{
	public StartupControl()
	{
		InitializeComponent();
	}

	private void ButtonCreate_Click(object sender, RoutedEventArgs e)
	{
		var vm = this.DataContext as StartupVM;
		var mainWindowVM = this.DataContextAncestors().OfType<MainWindow.MainWindowVM>().FirstOrDefault();
		if (vm == null || mainWindowVM == null)
		{
			return;
		}

		var dialog = new SaveFileDialog();
		dialog.DefaultExt = ".blocktaviusproject";
		if (dialog.ShowDialog() == true)
		{
			var projectFile = new FileInfo(dialog.FileName);
			var project = vm.CreateAndSaveProject(projectFile);
			mainWindowVM.OpenProject(project);
		}
	}
}
