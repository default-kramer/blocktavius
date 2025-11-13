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

public partial class LayeredImagesControl : UserControl
{
	public LayeredImagesControl()
	{
		InitializeComponent();
	}

	private void ChooseImages_Click(object sender, RoutedEventArgs e)
	{
		var project = this.DataContext as ProjectVM;
		var imageManager = project?.ImageManager();
		if (project == null || imageManager == null)
		{
			return;
		}

		var alreadyChecked = project.Layers.SelectMany(vm => vm.ExternalImage).ToHashSet();

		var window = new ImageChooserDialog();
		window.Owner = this.VisualTreeAncestors().OfType<Window>().First();
		window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

		if (window.ShowDialog(imageManager, alreadyChecked, out var resultVM))
		{
			project.OnImagesSelected(resultVM);
		}
	}
}
