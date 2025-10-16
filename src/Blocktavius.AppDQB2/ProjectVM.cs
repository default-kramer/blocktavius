using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2;

sealed class ProjectVM : ViewModelBase, IBlockList
{
	private readonly StgdatLoader stgdatLoader = new();

	IReadOnlyList<BlockVM> IBlockList.Blocks => Blockdata.AllBlockVMs;

	private string? _stgdatFilePath;
	public string? StgdatFilePath
	{
		get => _stgdatFilePath;
		set => ChangeProperty(ref _stgdatFilePath, value);
	}

	private string? _projectFilePath;
	public string? ProjectFilePath
	{
		get => _projectFilePath;
		set
		{
			ChangeProperty(ref _projectFilePath, value);
			OnPropertyChanged(nameof(ProjectFilePathToDisplay));
		}
	}

	public string ProjectFilePathToDisplay => string.IsNullOrWhiteSpace(_projectFilePath) ? "<< set during Save >>" : _projectFilePath;

	private bool _includeStgdatInPreview = true;
	public bool IncludeStgdatInPreview
	{
		get => _includeStgdatInPreview;
		set => ChangeProperty(ref _includeStgdatInPreview, value);
	}

	public ObservableCollection<ILayerVM> Layers { get; } = new ObservableCollection<ILayerVM>();

	private ILayerVM? _selectedLayer;
	public ILayerVM? SelectedLayer
	{
		get => _selectedLayer;
		set => ChangeProperty(ref _selectedLayer, value);
	}

	public ObservableCollection<ScriptVM> Scripts { get; } = new();

	private ScriptVM? _selectedScript;
	public ScriptVM? SelectedScript
	{
		get => _selectedScript;
		set
		{
			if (value != null)
			{
				SelectedScriptNode = value;
			}
			ChangeProperty(ref _selectedScript, value);
		}
	}

	private object? _selectedScriptNode;
	public object? SelectedScriptNode
	{
		get => _selectedScriptNode;
		private set
		{
			void SetSelected(bool selected)
			{
				if (_selectedScriptNode is ScriptNodeVM node)
				{
					node.IsSelected = selected;
				}
				if (_selectedScriptNode is ScriptVM script)
				{
					script.IsSelected = selected;
				}
			}
			if (value != _selectedScriptNode)
			{
				SetSelected(false);
				ChangeProperty(ref _selectedScriptNode, value);
				SetSelected(true);
			}
		}
	}

	public void UpdateSelectedScriptNode(ScriptNodeVM? node)
	{
		SelectedScriptNode = node;
	}

	public void OnScriptListViewClicked()
	{
		SelectedScriptNode = SelectedScript;
	}

	public bool TryRebuildStage(out IStage stage)
	{
		if (string.IsNullOrWhiteSpace(this.StgdatFilePath))
		{
			stage = null!;
			return false;
		}

		if (!stgdatLoader.TryLoad(this.StgdatFilePath, out var loadResult, out string error))
		{
			stage = null!;
			return false;
		}

		IMutableStage workingStage = loadResult.Stage.Clone();

		var context = new StageRebuildContext(workingStage);

		var script = this.Scripts.Where(s => s.IsMain).SingleOrDefault();
		if (script != null)
		{
			foreach (var mutation in script.RebuildMutations(context))
			{
				workingStage.Mutate(mutation);
			}
		}

		stage = workingStage;
		return true;
	}
}
