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
		private Driver? eyeOfRubissDriver = null;
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

		private bool firstTime = true;
		protected override void OnActivated(EventArgs e)
		{
			base.OnActivated(e);

			if (firstTime)
			{
				firstTime = false;

				this.Hide();
				var profileDialog = new EditProfileWindow();
				profileDialog.Owner = this.Owner;
				profileDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
				var oldProfile = Global.Profile;
				var dialogResult = profileDialog.ShowDialog(oldProfile, out var newProfile);
				if (dialogResult.GetValueOrDefault(false))
				{
					this.Show();

					bool isAlreadySaved = newProfile.ConfigFile.Exists && oldProfile.Equals(newProfile);
					if (!isAlreadySaved)
					{
						newProfile.Save();
					}
					Global.Profile = newProfile;

					eyeOfRubissDriver = Driver.CreateAndStart(new Driver.Config()
					{
						EyeOfRubissExePath = @"C:\Users\kramer\Documents\code\DQB2_WorldViewer\.EXPORT\EyeOfRubiss.exe",
						UseCmdShell = true,
					});
				}
				else
				{
					this.Close();
				}
			}
		}

		protected override void OnClosed(EventArgs e)
		{
			try { eyeOfRubissDriver?.Dispose(); } catch { }
			base.OnClosed(e);
		}

		private void PreviewButtonClicked(object sender, RoutedEventArgs e)
		{
			if (eyeOfRubissDriver != null && vm.TryRebuildStage(out var scriptedStage))
			{
				eyeOfRubissDriver.WriteStageAsync(scriptedStage).GetAwaiter().GetResult();
			}
		}
	}
}