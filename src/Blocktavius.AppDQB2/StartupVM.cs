using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Blocktavius.AppDQB2;

sealed class StartupVM : ViewModelBase
{
	private readonly ProfileSettings Profile;
	private readonly Action<FileInfo> LoadRecentProjectHandler;

	public IReadOnlyList<RecentProjectVM> RecentProjects { get; }
	public ICommand CommandLoadRecent { get; }

	public Visibility HasRecentProjectsVisibility => RecentProjects.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
	public Visibility NoRecentProjectsVisibility => RecentProjects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

	public StartupVM(ProfileSettings profile, Action<FileInfo> loadRecentProjectHandler)
	{
		this.Profile = profile;
		this.LoadRecentProjectHandler = loadRecentProjectHandler;

		RecentProjects = profile.RecentProjectManager.Projects.Select(RecentProjectVM.Create).ToList();
		CommandLoadRecent = new RelayCommand(_ => SelectedRecentProject != null, LoadRecent);
	}

	private RecentProjectVM? _selectedRecentProject = null;
	public RecentProjectVM? SelectedRecentProject
	{
		get => _selectedRecentProject;
		set => ChangeProperty(ref _selectedRecentProject, value);
	}

	private void LoadRecent(object? _)
	{
		if (SelectedRecentProject == null)
		{
			return;
		}
		LoadRecentProjectHandler(SelectedRecentProject.ProjectFile);
	}

	public ProjectVM CreateAndSaveProject(FileInfo projectFile)
	{
		var project = ProjectVM.CreateNew(Profile, projectFile);
		project.SaveChanges();
		return project;
	}

	public sealed class RecentProjectVM : ViewModelBase
	{
		public required FileInfo ProjectFile { get; init; }

		public string DisplayName => ProjectFile.FullName;

		public static RecentProjectVM Create(RecentProjectManager.RecentProject project) => new RecentProjectVM
		{
			ProjectFile = new FileInfo(project.FullPath),
		};
	}
}
