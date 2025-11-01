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
	private ProfileSettings profile;

	public ProjectVM(ProfileSettings profile)
	{
		Layers = new();
		Layers.Add(chunkGridLayer);
		SelectedLayer = Layers.FirstOrDefault();

		this.profile = profile;
		ForceUpdateProfile(profile);
	}

	public void SwitchProfile(ProfileSettings newProfile)
	{
		if (profile.VerificationHash != newProfile.VerificationHash)
		{
			ForceUpdateProfile(newProfile);
		}
	}

	private void ForceUpdateProfile(ProfileSettings profile)
	{
		this.profile = profile;

		SelectedSourceSlot = null;
		SourceSlots.Clear();
		foreach (var slot in profile.SaveSlots)
		{
			SourceSlots.Add(SlotVM.Create(slot));
		}

		SelectedDestSlot = null;
		DestSlots.Clear();
		foreach (var slot in profile.WritableSaveSlots)
		{
			DestSlots.Add(WritableSlotVM.Create(slot));
		}
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
			var prev = _selectedScript;
			if (ChangeProperty(ref _selectedScript, value))
			{
				value?.SetActive(true);
				prev?.SetActive(false);
			}
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
		if (TryLoadStage(out var result))
		{
			chunkGridLayer.RebuildImage(result.Stage.ChunksInUse.Concat(expansion));
		}
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
		workingStage.ExpandChunks(ChunkExpansion);

		var context = new StageRebuildContext(workingStage);

		var script = this.SelectedScript;
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

	public ObservableCollection<SlotVM> SourceSlots { get; } = new();

	private SlotVM? _selectedSourceSlot = null;
	public SlotVM? SelectedSourceSlot
	{
		get => _selectedSourceSlot;
		set
		{
			var prev = SelectedSourceStage;
			if (ChangeProperty(ref _selectedSourceSlot, value, nameof(SelectedSourceSlot), nameof(SourceStages)))
			{
				SelectedSourceStage = value?.Stages?.FirstOrDefault(s => s.Filename.Equals(prev?.Filename, StringComparison.OrdinalIgnoreCase));
			}
		}
	}

	public IReadOnlyList<SlotStageVM> SourceStages => SelectedSourceSlot?.Stages ?? Array.Empty<SlotStageVM>();

	private SlotStageVM? _selectedSourceStage = null;
	public SlotStageVM? SelectedSourceStage
	{
		get => _selectedSourceStage;
		set
		{
			if (ChangeProperty(ref _selectedSourceStage, value, nameof(SelectedSourceStage), nameof(StgdatFilePath), nameof(DestFullPath)))
			{
				chunkGridLayer.StgdatPath = StgdatFilePath ?? "";
			}
		}
	}

	public string? StgdatFilePath => SelectedSourceStage?.StgdatFile?.FullName;

	public string? DestFullPath
	{
		get
		{
			if (SelectedDestSlot != null && SelectedSourceStage != null)
			{
				return SelectedDestSlot.GetFullPath(SelectedSourceStage.Name);
			}
			return null;
		}
	}

	public ObservableCollection<WritableSlotVM> DestSlots { get; } = new();

	private WritableSlotVM? _selectedDestSlot = null;
	public WritableSlotVM? SelectedDestSlot
	{
		get => _selectedDestSlot;
		set => ChangeProperty(ref _selectedDestSlot, value, nameof(SelectedDestSlot), nameof(DestFullPath));
	}
}
