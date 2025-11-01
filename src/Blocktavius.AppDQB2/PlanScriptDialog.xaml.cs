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
using System.Windows.Shapes;

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

		if (this.ShowDialog().GetValueOrDefault(false))
		{

		}
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

		public static ProjectDeps Rebuild(ProjectVM project) => new ProjectDeps
		{
			SelectedSourceSlot = project.SelectedSourceSlot,
			SelectedDestSlot = project.SelectedDestSlot,
			SelectedInclusionMode = project.SelectedInclusionMode
		};
	}

	sealed class PlanScriptVM : ViewModelBase, IDisposable
	{
		private readonly object subscribeKey = new();

		public ObservableCollection<IPlanItemVM> PlanItems { get; } = new();
		public ProjectVM Project { get; }

		public PlanScriptVM(ProjectVM project)
		{
			this.Project = project;
			project.Subscribe(subscribeKey, this);
			_deps = ProjectDeps.Rebuild(project);
			UpdatePlan();
		}

		private object _deps; // Don't expose any properties; this is just for change detection.
		private ProjectDeps Deps
		{
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
			if (Project.SelectedSourceSlot == null)
			{
				return;
			}

			var mode = Project.SelectedInclusionMode.InclusionMode;
			var sourceSlot = Project.SelectedSourceSlot;

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
						ShortName = stage.Name,
					};
				}
				else
				{
					bool willBeCopied = mode == InclusionMode.Automatic && stage.IsKnownStage;
					planItem = new SimpleCopyPlanItemVM
					{
						SourceFile = stage.StgdatFile,
						WillBeCopied = willBeCopied,
						ShortName = stage.Name,
					};
				}

				PlanItems.Add(planItem);
			}
		}

		private bool TryPlanSimpleCopy(SlotVM sourceSlot, string name, bool willBeCopied)
		{
			var sourceFile = new FileInfo(sourceSlot.GetFullPath(name));
			if (sourceFile.Exists)
			{
				PlanItems.Add(new SimpleCopyPlanItemVM
				{
					SourceFile = sourceFile,
					WillBeCopied = willBeCopied,
					ShortName = name,
				});
				return true;
			}
			return false;
		}
	}

	interface IPlanItemVM
	{
		bool WillBeModified { get; }
		bool WillBeCopied { get; }
		string ShortName { get; }
	}

	class SimpleCopyPlanItemVM : IPlanItemVM
	{
		public required FileInfo SourceFile { get; init; }
		public required bool WillBeCopied { get; init; }
		public required string ShortName { get; init; }

		public bool WillBeModified => false;
	}

	class CopyWithModificationsPlanItemVM : IPlanItemVM
	{
		public required string ShortName { get; init; }

		public bool WillBeModified => true;
		public bool WillBeCopied => true;

		public void TODO()
		{
			// old code here... should RebuildStage in advance on a background thread and maybe
			// even save it to a temp file so when they click OK we can respond very quickly

			ProjectVM project = null!;
			IWritableSaveSlot target = null!;

			if (project == null
				|| target == null
				|| !project.TryRebuildStage(out var stage)
				|| !stage.Saver.CanSave)
			{
				return;
			}

			stage.Saver.Save(target, stage);
		}
	}
}
