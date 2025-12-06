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

namespace Blocktavius.AppDQB2
{
	/// <summary>
	/// Interaction logic for ProjectParamsControl.xaml
	/// </summary>
	public partial class ProjectParamsControl : UserControl
	{
		public ProjectParamsControl()
		{
			InitializeComponent();
		}

		private void ButtonSave_Click(object sender, RoutedEventArgs e)
		{
			(DataContext as ProjectVM)?.SaveChanges();
		}

		private void ButtonClose_Click(object sender, RoutedEventArgs e)
		{
			var mainVM = this.DataContextAncestors().OfType<MainWindow.MainWindowVM>().FirstOrDefault();
			mainVM?.CloseCurrentProject();
		}
	}
}
