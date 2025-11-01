using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

namespace Blocktavius.AppDQB2;

/// <summary>
/// Interaction logic for PlanScriptDialog.xaml
/// </summary>
public partial class PlanScriptDialog : Window
{
	public PlanScriptDialog()
	{
		InitializeComponent();
	}

	internal void ShowDialog(ProjectVM project)
	{
		using var vm = new PlanScriptVM(project);
		this.DataContext = vm;

		this.ShowDialog();
	}

	private async void OkButton_Click(object sender, RoutedEventArgs e)
	{
		var vm = (PlanScriptVM)DataContext;
		await vm.Execute();
		DialogResult = true;
	}

	/// <summary>
	/// Subset of properties from the <see cref="ProjectVM"/> which
	/// require us to rebuild the plan when they are changed.
	/// </summary>
	sealed record ProjectDeps
	{
		public required SlotVM? SelectedSourceSlot { get; init; }
		public required WritableSlotVM? SelectedDestSlot { get; init; }
		public required InclusionModeVM SelectedInclusionMode { get; init; }
		public required bool DestIsSource { get; init; }

		public static ProjectDeps Rebuild(ProjectVM project) => new ProjectDeps
		{
			SelectedSourceSlot = project.SelectedSourceSlot,
			SelectedDestSlot = project.SelectedDestSlot,
			SelectedInclusionMode = project.SelectedInclusionMode,
			DestIsSource = project.SelectedSourceSlot != null
				&& project.SelectedSourceSlot.FullPath.Equals(project.SelectedDestSlot?.FullPath, StringComparison.OrdinalIgnoreCase),
		};
	}

	sealed class PlanScriptVM : ViewModelBase, IDisposable
	{
		private readonly object subscribeKey = new();
		private readonly DirectoryInfo? backupLocation;
		private readonly Task<IStage?> rebuildTask;

		public ObservableCollection<IPlanItemVM> PlanItems { get; } = new();
		public ProjectVM Project { get; }
		public string BackupMessage { get; }
		public string BackupWarning { get; }

		public PlanScriptVM(ProjectVM project)
		{
			this.Project = project;
			project.Subscribe(subscribeKey, this);

			rebuildTask = Task.Run(() =>
			{
				if (project.TryRebuildStage(out var stage) && stage.Saver.CanSave)
				{
					return stage;
				}
				return null;
			});

			if (project.BackupsEnabled(out var backupDir))
			{
				backupLocation = new DirectoryInfo(Path.Combine(backupDir.FullName, DateTime.UtcNow.ToString("yyyyMMdd.HHmmss.fff")));
				BackupMessage = $"Backups will be created at {backupLocation.FullName}";
				BackupWarning = "";
			}
			else
			{
				backupLocation = null;
				BackupMessage = "";
				BackupWarning = "Backups will not be created! Be careful!";
			}

			_deps = ProjectDeps.Rebuild(project);
			UpdatePlan();
		}

		private ProjectDeps _deps;
		private ProjectDeps Deps
		{
			get => _deps;
			set
			{
				if (ChangeProperty(ref _deps, value))
				{
					UpdatePlan();
				}
			}
		}

		protected override void OnSubscribedPropertyChanged(ViewModelBase sender, PropertyChangedEventArgs e)
		{
			Deps = ProjectDeps.Rebuild(Project);
		}

		void IDisposable.Dispose()
		{
			Project.Unsubscribe(subscribeKey);
		}

		private void UpdatePlan()
		{
			PlanItems.Clear();

			var mode = Project.SelectedInclusionMode.InclusionMode;
			var sourceSlot = Project.SelectedSourceSlot;
			var destSlot = Project.SelectedDestSlot;

			if (sourceSlot == null)
			{
				return;
			}

			TryPlanSimpleCopy(sourceSlot, "AUTOCMNDAT.BIN", mode == InclusionMode.Automatic);
			TryPlanSimpleCopy(sourceSlot, "AUTOSTGDAT.BIN", mode == InclusionMode.Automatic);
			TryPlanSimpleCopy(sourceSlot, "CMNDAT.BIN", mode == InclusionMode.Automatic || mode == InclusionMode.JustCmndat);

			var sortedStages = sourceSlot.Stages
				.OrderBy(stage => stage == Project.SelectedSourceStage ? 0 : 1)
				.ThenBy(stage => stage.KnownStageSortOrder ?? int.MaxValue)
				.ThenBy(stage => stage.Name)
				.ToList();

			foreach (var stage in sortedStages)
			{
				IPlanItemVM planItem;

				if (stage == Project.SelectedSourceStage)
				{
					planItem = new CopyWithModificationsPlanItemVM()
					{
						DestIsSource = Deps.DestIsSource,
						RebuildStageTask = rebuildTask,
						SourceStgdatFile = stage.StgdatFile,
						BackupDir = this.backupLocation,
						ShortName = stage.Name,
					};
				}
				else
				{
					bool shouldCopy = mode == InclusionMode.Automatic && stage.IsKnownStage;
					planItem = new SimpleCopyPlanItemVM
					{
						DestIsSource = Deps.DestIsSource,
						BackupDir = this.backupLocation,
						SourceFile = stage.StgdatFile,
						ShouldCopy = shouldCopy,
						ShortName = stage.Name,
					};
				}

				PlanItems.Add(planItem);
			}
		}

		private bool TryPlanSimpleCopy(SlotVM sourceSlot, string name, bool shouldCopy)
		{
			var sourceFile = new FileInfo(sourceSlot.GetFullPath(name));
			if (sourceFile.Exists)
			{
				PlanItems.Add(new SimpleCopyPlanItemVM
				{
					DestIsSource = Deps.DestIsSource,
					BackupDir = this.backupLocation,
					SourceFile = sourceFile,
					ShouldCopy = shouldCopy,
					ShortName = name,
				});
				return true;
			}
			return false;
		}

		public async Task Execute()
		{
			if (Project.SelectedDestSlot == null)
			{
				return;
			}

			var stage = await rebuildTask;
			if (stage == null || !stage.Saver.CanSave)
			{
				return; // TODO show some kind of error
			}

			if (backupLocation != null && PlanItems.Any(p => p.WillBeBackedUp))
			{
				backupLocation.Create();
			}

			foreach (var item in PlanItems)
			{
				await item.Execute(Project.SelectedDestSlot);
			}
		}
	}

	interface IPlanItemVM
	{
		bool WillBeBackedUp { get; }
		bool WillBeModified { get; }
		bool WillBeCopied { get; }
		string ShortName { get; }
		Task Execute(WritableSlotVM targetSlot);
	}

	class SimpleCopyPlanItemVM : IPlanItemVM
	{
		public required bool DestIsSource { get; init; }
		public required DirectoryInfo? BackupDir { get; init; }
		public required FileInfo SourceFile { get; init; }
		public required bool ShouldCopy { get; init; }
		public required string ShortName { get; init; }

		public bool WillBeCopied => !DestIsSource && ShouldCopy;
		public bool WillBeBackedUp => WillBeCopied && !DestIsSource && BackupDir != null;
		public bool WillBeModified => false;

		public async Task Execute(WritableSlotVM targetSlot)
		{
			var targetFile = new FileInfo(targetSlot.GetFullPath(SourceFile.Name));
			if (WillBeBackedUp && targetFile.Exists)
			{
				if (BackupDir == null)
				{
					throw new Exception("Assert fail! Promised to create backup, but BackupDir is null");
				}
				await Task.Run(() => targetFile.CopyTo(Path.Combine(BackupDir.FullName, SourceFile.Name), overwrite: true));
			}
			if (WillBeCopied)
			{
				await Task.Run(() => SourceFile.CopyTo(targetFile.FullName, overwrite: true));
			}
		}
	}

	class CopyWithModificationsPlanItemVM : IPlanItemVM
	{
		public required bool DestIsSource { get; init; }
		public required FileInfo SourceStgdatFile { get; init; }
		public required Task<IStage?> RebuildStageTask { get; init; }
		public required DirectoryInfo? BackupDir { get; init; }
		public required string ShortName { get; init; }

		public bool WillBeModified => true;
		public bool WillBeCopied => !DestIsSource;
		public bool WillBeBackedUp => BackupDir != null;

		public async Task Execute(WritableSlotVM targetSlot)
		{
			var stage = await RebuildStageTask;
			if (stage == null || !stage.Saver.CanSave)
			{
				throw new Exception("Assert fail - cannot save, should not have even started the plan!");
			}

			var targetFile = new FileInfo(targetSlot.GetFullPath(SourceStgdatFile.Name));
			if (WillBeBackedUp && targetFile.Exists)
			{
				if (BackupDir == null)
				{
					throw new Exception("Assert fail! Promised to create backup, but BackupDir is null");
				}
				await Task.Run(() => targetFile.CopyTo(Path.Combine(BackupDir.FullName, targetFile.Name), overwrite: true));
			}

			await Task.Run(() => stage.Saver.Save(targetSlot.WritableSlot, stage, targetFile));
		}
	}
}
