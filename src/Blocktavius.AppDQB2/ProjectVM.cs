using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2;

sealed class ProjectVM : ViewModelBase, IBlockList
{
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

	public ObservableCollection<LayerVM> Layers { get; } = new ObservableCollection<LayerVM>();

	private LayerVM? _selectedLayer;
	public LayerVM? SelectedLayer
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
}
