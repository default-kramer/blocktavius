using Blocktavius.DQB2;
using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2;

sealed class ProjectVM : ViewModelBase, IBlockList, IDropTarget
{
	private readonly StgdatLoader stgdatLoader = new();
	private ExternalImageManager? imageManager = null;

	public ProjectVM()
	{
		Layers = new();
		Layers.Add(chunkGridLayer);
		SelectedLayer = Layers.FirstOrDefault();
	}

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
		set
		{
			ChangeProperty(ref _stgdatFilePath, value);
			chunkGridLayer.StgdatPath = value ?? "";
		}
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

	private readonly ChunkGridLayer chunkGridLayer = new();

	public ExternalImageManager? ImageManager() => imageManager;

	public string ProjectFilePathToDisplay => string.IsNullOrWhiteSpace(_projectFilePath) ? "<< set during Save >>" : _projectFilePath;

	private bool _includeStgdatInPreview = true;
	public bool IncludeStgdatInPreview
	{
		get => _includeStgdatInPreview;
		set => ChangeProperty(ref _includeStgdatInPreview, value);
	}

	public ObservableCollection<ILayerVM> Layers { get; }

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


	/// <summary>
	/// Might include chunks that were already present in the STGDAT file.
	/// </summary>
	public IReadOnlySet<ChunkOffset> ChunkExpansion
	{
		get => _chunkExpansion;
		private set => ChangeProperty(ref _chunkExpansion, value);
	}
	private IReadOnlySet<ChunkOffset> _chunkExpansion = ImmutableHashSet<ChunkOffset>.Empty;

	public void ExpandChunks(IReadOnlySet<ChunkOffset> expansion)
	{
		ChunkExpansion = expansion;
	}

	public bool TryLoadStage(out StgdatLoader.LoadResult loadResult)
	{
		if (string.IsNullOrWhiteSpace(this.StgdatFilePath))
		{
			loadResult = default!;
			return false;
		}

		return stgdatLoader.TryLoad(this.StgdatFilePath, out loadResult, out _);
	}

	public bool TryRebuildStage(out IStage stage)
	{
		if (!TryLoadStage(out var loadResult))
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
		int keepAtEnd = 0;
		if (Layers.LastOrDefault() is ChunkGridLayer)
		{
			keepAtEnd++;
		}

		bool changed = false;
		foreach (var img in result.Images)
		{
			bool wasChecked = result.AlreadyChecked.Contains(img.ExternalImage);

			if (img.IsChecked && !wasChecked)
			{
				int where = Layers.Count - keepAtEnd;
				Layers.Insert(where, new ExternalImageLayerVM { Image = img.ExternalImage });
				changed = true;
			}
			else if (!img.IsChecked && wasChecked)
			{
				var removeItems = this.Layers.Where(x => x.ExternalImage.Contains(img.ExternalImage)).ToList();
				foreach (var item in removeItems)
				{
					Layers.Remove(item);
				}
				changed = true;
			}
		}

		if (changed)
		{
			// Force property grid to reload now that the layer choices have changed
			var temp = SelectedScriptNode;
			SelectedScriptNode = null;
			SelectedScriptNode = temp;
		}
	}
}
