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
		private ProjectVM vm = new();

		public MainWindow()
		{
			InitializeComponent();

			vm.StgdatFilePath = @"C:\Users\kramer\Documents\My Games\DRAGON QUEST BUILDERS II\Steam\76561198073553084\SD\B02\STGDAT01.BIN";
			vm.ProjectFilePath = @"C:\Users\kramer\Documents\code\HermitsHeresy\examples\STB\foo.blocktaviusproject";

			vm.Scripts.Add(new ScriptVM() { Name = "Main", IsMain = true });
			vm.SelectedScript = vm.Scripts.First();

			DataContext = vm;
			Global.SetCurrentProject(vm);
		}

		protected override void OnClosed(EventArgs e)
		{
			App.ShutdownEyeOfRubiss();
			base.OnClosed(e);
		}

		private void PreviewButtonClicked(object sender, RoutedEventArgs e)
		{
			if (vm.TryRebuildStage(out var scriptedStage))
			{
				App.eyeOfRubissDriver.WriteStageAsync(scriptedStage).GetAwaiter().GetResult();
			}
		}
	}
}