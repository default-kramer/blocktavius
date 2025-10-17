using Blocktavius.DQB2;
using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2;

sealed class ProjectVM : ViewModelBase, IBlockList, IDropTarget
{
	private readonly StgdatLoader stgdatLoader = new();
	private ExternalImageManager? imageManager = null;

	void IDropTarget.DragOver(IDropInfo dropInfo)
	{
		dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
		dropInfo.Effects = System.Windows.DragDropEffects.Move;
	}

	void IDropTarget.Drop(IDropInfo dropInfo)
	{
		if (dropInfo.Data is ILayerVM layer)
		{
			int oldIndex = Layers.IndexOf(layer);
			int newIndex = dropInfo.InsertIndex;

			if (oldIndex < newIndex)
			{
				newIndex--;
			}

			if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex)
			{
				Layers.Move(oldIndex, newIndex);
			}
		}
	}

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
			// TODO - here we should probably check if any images are still referenced by the project
			// and keep those in the new image manager (probably without watching them though... and with a warning?)
			imageManager?.Dispose();
			imageManager = new ExternalImageManager(new System.IO.DirectoryInfo(System.IO.Path.GetDirectoryName(ProjectFilePath) ?? ".....fail"));
		}
	}

	public ExternalImageManager? ImageManager() => imageManager;

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

	public void OnImagesSelected(ImageChooserDialog.VM result)
	{
		foreach (var img in result.Images)
		{
			bool wasChecked = result.AlreadyChecked.Contains(img.ExternalImage);

			if (img.IsChecked && !wasChecked)
			{
				Layers.Add(new ExternalImageLayerVM { Image = img.ExternalImage });
			}
			else if (!img.IsChecked && wasChecked)
			{
				var removeItems = this.Layers.Where(x => x.ExternalImage.Contains(img.ExternalImage)).ToList();
				foreach (var item in removeItems)
				{
					Layers.Remove(item);
				}
			}
		}
	}
}
