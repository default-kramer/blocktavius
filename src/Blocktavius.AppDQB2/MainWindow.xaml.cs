using Blocktavius.DQB2.EyeOfRubiss;
using System;
using System.IO;
using System.Text;
using System.Windows;

namespace Blocktavius.AppDQB2
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public Driver? EyeOfRubissDriver { get; set; }

		public MainWindow()
		{
			InitializeComponent();

			this.DataContextChanged += MainWindow_DataContextChanged;
		}

		private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			if (e.NewValue is ProjectVM vm)
			{
				Global.SetCurrentProject(vm);
			}
		}

		protected override void OnClosed(EventArgs e)
		{
			try { EyeOfRubissDriver?.Dispose(); } catch { }
			base.OnClosed(e);
		}

		private void PreviewButtonClicked(object sender, RoutedEventArgs e)
		{
			var vm = this.DataContext as ProjectVM;
			if (EyeOfRubissDriver != null && vm != null && vm.TryRebuildStage(out var scriptedStage))
			{
				EyeOfRubissDriver.WriteStageAsync(scriptedStage).GetAwaiter().GetResult();
			}
		}
	}
}