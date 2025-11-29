using Blocktavius.AppDQB2.Services;
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
		}

		protected override void OnClosed(EventArgs e)
		{
			try { EyeOfRubissDriver?.Dispose(); } catch { }
			base.OnClosed(e);
		}

		internal void DoPreview()
		{
			var vm = (this.DataContext as MainWindowVM)?.CurrentContent as ProjectVM;
			if (EyeOfRubissDriver != null && vm != null && vm.TryRebuildStage(out var scriptedStage))
			{
				EyeOfRubissDriver.WriteStageAsync(scriptedStage).GetAwaiter().GetResult();
			}
		}

		internal sealed class MainWindowVM : ViewModelBase
		{
			private readonly IServices services = DefaultServices.Instance;
			private readonly ProfileSettings profile;

			public MainWindowVM(ProfileSettings profile)
			{
				this.profile = profile;
				_currentContent = BuildStartupVM();
			}

			private StartupVM BuildStartupVM() => new(services, profile, OpenProject);

			private object _currentContent;
			public object CurrentContent
			{
				get => _currentContent;
				private set => ChangeProperty(ref _currentContent, value);
			}

			public void OpenProject(FileInfo projectFile) => OpenProject(projectFile, null);

			public void OpenProject(FileInfo projectFile, ProjectVM? preloadedVM)
			{
				var vm = preloadedVM ?? ProjectVM.Load(services, profile, projectFile);
				Global.SetCurrentProject(vm);
				profile.RecentProjectManager.OnOpened(projectFile.FullName);

				CurrentContent = vm;
			}

			public void CloseCurrentProject()
			{
				Global.ClearCurrentProject();
				CurrentContent = BuildStartupVM();
			}
		}
	}
}
