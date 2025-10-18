using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Shapes;

namespace Blocktavius.AppDQB2;

/// <summary>
/// Interaction logic for ImageChooserDialog.xaml
/// </summary>
public partial class ImageChooserDialog : Window
{
	internal sealed class ImageVM : ViewModelBase
	{
		public required ExternalImageVM ExternalImage { get; init; }

		public string DisplayName => ExternalImage.RelativePath;

		public bool _isChecked;
		public bool IsChecked
		{
			get => _isChecked;
			set => ChangeProperty(ref _isChecked, value);
		}
	}

	internal sealed class VM : ViewModelBase
	{
		public required IReadOnlyList<ImageVM> Images { get; init; }

		public required IReadOnlySet<ExternalImageVM> AlreadyChecked { get; init; }
	}

	public ImageChooserDialog()
	{
		InitializeComponent();
	}

	internal bool ShowDialog(ExternalImageManager imageManager, IReadOnlySet<ExternalImageVM> alreadyChecked, out VM result)
	{
		var images = imageManager.ExternalImages.Select(img => new ImageVM
		{
			ExternalImage = img,
			IsChecked = alreadyChecked.Contains(img),
		}).ToList();

		var vm = new VM
		{
			Images = images,
			AlreadyChecked = alreadyChecked,
		};
		this.DataContext = vm;

		result = vm;
		return this.ShowDialog() == true;
	}

	private void CancelClick(object sender, RoutedEventArgs e)
	{
		DialogResult = false;
		Close();
	}

	private void OkClick(object sender, RoutedEventArgs e)
	{
		DialogResult = true;
		Close();
	}
}
