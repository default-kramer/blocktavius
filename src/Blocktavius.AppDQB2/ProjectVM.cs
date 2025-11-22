using Blocktavius.AppDQB2.Persistence.V1;
using Blocktavius.Core;
using Blocktavius.DQB2;
using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Blocktavius.AppDQB2;

sealed class ProjectVM : ViewModelBase, IBlockList, IDropTarget, Persistence.IAreaManager, Persistence.IBlockManager
{
	// immutable:
	private readonly ChunkGridLayer chunkGridLayer = new();
	private readonly MinimapLayer? minimapLayer;
	private readonly StgdatLoader stgdatLoader = new();
	public ICommand CommandExportChunkMask { get; }
	public ICommand CommandExportMinimap { get; }

	// mutable:
	private ExternalImageManager? imageManager = null;
	private ProfileSettings profile;

	public ProjectVM(ProfileSettings profile)
	{
		Layers = new();
		if (MinimapRenderer.IsEnabled)
		{
			minimapLayer = new();
			Layers.Add(minimapLayer);
		}
		Layers.Add(chunkGridLayer);
		SelectedLayer = Layers.FirstOrDefault();

		this.profile = profile;
		ForceUpdateProfile(profile);

		CommandExportChunkMask = new RelayCommand(_ => chunkGridLayer.ChunkGridImage != null, ExportChunkMask);
		CommandExportMinimap = new RelayCommand(_ => minimapLayer?.MinimapImage != null, ExportMinimap);
	}

	private void ExportChunkMask(object? arg) => ExportImage(chunkGridLayer.ChunkGridImage, "exported-chunk-mask.png", 1.0);

	private void ExportMinimap(object? arg) => ExportImage(minimapLayer?.MinimapImage, "exported-minimap.png", 0.5);

	private void ExportImage(BitmapSource? img, string filename, double scale)
	{
		var dir = new FileInfo(ProjectFilePath ?? "").Directory;
		if (img == null || dir == null)
		{
			return;
		}

		var frame = BitmapFrame.Create(img);
		if (scale != 1.0)
		{
			frame = BitmapFrame.Create(new TransformedBitmap(frame, new ScaleTransform(scale, scale)));
		}

		var file = Path.Combine(dir.FullName, filename);
		BitmapEncoder encoder = new PngBitmapEncoder();
		encoder.Frames.Add(frame);
		using (var fileStream = new FileStream(file, FileMode.Create))
		{
			encoder.Save(fileStream);
		}
	}

	public static ProjectVM CreateNew(ProfileSettings profile, FileInfo projectFile)
	{
		var vm = new ProjectVM(profile);
		vm.SetProjectFilePath(projectFile.FullName);

		vm.Scripts.Add(new ScriptVM().SetScriptName("Main"));
		vm.Scripts.Add(new ScriptVM().SetScriptName("Script 2"));
		vm.SelectedScript = vm.Scripts.First();

		return vm;
	}

	internal bool BackupsEnabled(out DirectoryInfo backupDir)
	{
		backupDir = profile.BackupDir!;
		return backupDir != null;
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
	}

	private ExternalImageManager SetProjectFilePath(string path)
	{
		ExternalImageManager Rebuild() => new ExternalImageManager(new DirectoryInfo(Path.GetDirectoryName(path) ?? "....fail"));

		// TODO wait... probably this path and the image manager should be immutable too?
		// Or not, because what happens on "Save As" then?
		if (ChangeProperty(ref _projectFilePath, path, nameof(ProjectFilePath), nameof(ProjectFilePathToDisplay)))
		{
			// TODO - what cleanup/reassignment do we have to do here?
			imageManager?.Dispose();
			imageManager = Rebuild();
			return imageManager;
		}
		else
		{
			imageManager = imageManager ?? Rebuild();
			return imageManager;
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

	public ObservableCollection<ILayerVM> Layers { get; }

	private ILayerVM? _selectedLayer;
	public ILayerVM? SelectedLayer
	{
		get => _selectedLayer;
		set => ChangeProperty(ref _selectedLayer, value);
	}

	private string _notes = "";
	public string Notes
	{
		get => _notes;
		set => ChangeProperty(ref _notes, value);
	}

	public ObservableCollection<ScriptVM> Scripts { get; } = new();

	private ScriptVM? _selectedScript;
	public ScriptVM? SelectedScript
	{
		get => _selectedScript;
		set
		{
			var prev = _selectedScript;
			if (ChangeProperty(ref _selectedScript, value))
			{
				value?.SetActive(true);
				prev?.SetActive(false);
			}
		}
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
		RebuildImages();
	}

	private void RebuildImages()
	{
		if (TryLoadStage(out var result))
		{
			chunkGridLayer.RebuildImage(result.Stage.ChunksInUse.Concat(ChunkExpansion));
			minimapLayer?.RebuildImage(this);
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

	public bool TryLoadMutableStage(out IMutableStage stage, bool expandChunks)
	{
		if (!TryLoadStage(out var loadResult))
		{
			stage = null!;
			return false;
		}

		stage = loadResult.Stage.Clone();
		if (expandChunks)
		{
			stage.ExpandChunks(ChunkExpansion);
		}
		return true;
	}

	public bool TryRebuildStage(out IStage stage)
	{
		if (!TryLoadMutableStage(out var workingStage, expandChunks: true))
		{
			stage = null!;
			return false;
		}

		var context = new StageRebuildContext(workingStage);
		var mutation = this.SelectedScript?.BuildMutation(context);
		if (mutation != null)
		{
			workingStage.Mutate(mutation);
		}

		stage = workingStage;
		return true;
	}

	public void OnImagesSelected(ImageChooserDialog.VM result)
	{
		var chunkGridLayer = Layers.LastOrDefault() as ChunkGridLayer;
		var minimapLayer = Layers.FirstOrDefault() as MinimapLayer;

		// Remove special/fixed layers to make the merge logic easier.
		// We will re-add these layers at the end.
		if (chunkGridLayer != null) { Layers.Remove(chunkGridLayer); }
		if (minimapLayer != null) { Layers.Remove(minimapLayer); }

		bool changed = false;
		foreach (var img in result.Images)
		{
			bool wasChecked = result.AlreadyChecked.Contains(img.ExternalImage);

			if (img.IsChecked && !wasChecked)
			{
				int where = Layers.Count;
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

		if (minimapLayer != null) { Layers.Insert(0, minimapLayer); }
		if (chunkGridLayer != null) { Layers.Add(chunkGridLayer); }

		if (changed)
		{
			// Force property grid to reload now that the layer choices have changed
			SelectedScript?.RefreshPropertyGrid();
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
				RebuildImages();
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
				return SelectedDestSlot.GetFullPath(SelectedSourceStage.Filename);
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

	public IReadOnlyList<InclusionModeVM> InclusionModes { get; } = InclusionModeVM.BuildChoices().ToList();

	private InclusionModeVM? _selectedInclusionMode;
	public InclusionModeVM SelectedInclusionMode
	{
		get => _selectedInclusionMode ?? InclusionModes.First();
		set => ChangeProperty(ref _selectedInclusionMode, value);
	}

	public void SaveChanges()
	{
		var path = ProjectFilePath;
		if (string.IsNullOrWhiteSpace(path))
		{
			throw new Exception("Assert fail - cannot save when ProjectFilePath is empty");
		}

		ProjectV1 persistModel = ToPersistModel();
		using var stream = File.Open(path, FileMode.Create, FileAccess.Write);
		persistModel.Save(stream);
		stream.Flush();
		stream.Close();
	}

	public ProjectV1 ToPersistModel()
	{
		return new ProjectV1()
		{
			ProfileVerificationHash = profile.VerificationHash,
			SourceSlot = SelectedSourceSlot?.ToPersistModel(),
			DestSlot = SelectedDestSlot?.ToPersistModel(),
			SourceStgdatFilename = SelectedSourceStage?.Filename,
			ChunkExpansion = this.ChunkExpansion.Select(ChunkOffsetV1.FromCore).ToList(),
			Notes = this.Notes,
			Images = Layers.OfType<ExternalImageLayerVM>().Select(vm => vm.ToPersistModel()).ToList(),
			MinimapVisible = minimapLayer?.IsVisible,
			ChunkGridVisible = chunkGridLayer.IsVisible,
			Scripts = this.Scripts.Select(s => s.ToPersistModelConcrete()).ToList(),
			SelectedScriptIndex = SelectedScript == null ? null : Scripts.IndexOf(SelectedScript),
		};
	}

	public static ProjectVM Load(ProfileSettings profile, FileInfo projectFile)
	{
		var json = File.ReadAllText(projectFile.FullName);
		var project = ProjectV1.Load(json);
		if (project == null)
		{
			throw new Exception($"Invalid project file: {projectFile.FullName}");
		}

		var vm = new ProjectVM(profile);
		var imageManager = vm.SetProjectFilePath(projectFile.FullName);
		vm.Reload(project, imageManager);
		return vm;
	}

	private void Reload(ProjectV1 project, ExternalImageManager imageManager)
	{
		using var changeset = DeferChanges();

		project = project.VerifyProfileHash(profile);

		SelectedSourceSlot = SourceSlots.FirstOrDefault(s => s.MatchesByNumber(project.SourceSlot))
			?? SourceSlots.FirstOrDefault(s => s.MatchesByName(project.SourceSlot));
		SelectedDestSlot = DestSlots.FirstOrDefault(s => s.MatchesByNumber(project.DestSlot))
			?? DestSlots.FirstOrDefault(s => s.MatchesByNumber(project.DestSlot));

		SelectedSourceStage = SelectedSourceSlot?.Stages.EmptyIfNull().FirstOrDefault(s =>
			string.Equals(s.Filename, project.SourceStgdatFilename, StringComparison.OrdinalIgnoreCase));

		Notes = project.Notes ?? "";
		ChunkExpansion = project.ChunkExpansion.EmptyIfNull().Select(o => o.ToCore()).ToHashSet();

		Layers.Clear();
		if (minimapLayer != null)
		{
			minimapLayer.IsVisible = project.MinimapVisible.GetValueOrDefault(true);
			Layers.Add(minimapLayer);
		}
		foreach (var image in project.Images.EmptyIfNull())
		{
			if (!string.IsNullOrWhiteSpace(image.RelativePath))
			{
				var imageVM = imageManager.FindOrCreate(image.RelativePath, out _);
				var layerVM = new ExternalImageLayerVM
				{
					Image = imageVM,
					IsVisible = image.IsVisible.GetValueOrDefault(false),
				};
				Layers.Add(layerVM);
			}
		}
		chunkGridLayer.IsVisible = project.ChunkGridVisible.GetValueOrDefault(true);
		Layers.Add(chunkGridLayer);
		SelectedLayer = Layers.FirstOrDefault();

		var scriptContext = new Persistence.ScriptDeserializationContext
		{
			AreaManager = this,
			BlockManager = this,
		};

		Scripts.Clear();
		foreach (var script in project.Scripts.EmptyIfNull())
		{
			var scriptVM = ScriptVM.Load(script, scriptContext);
			Scripts.Add(scriptVM);
		}
		SelectedScript = Scripts.ElementAtOrDefault(project.SelectedScriptIndex.GetValueOrDefault(-1));

		changeset.Complete();

		ExpandChunks(ChunkExpansion);
	}

	IAreaVM? Persistence.IAreaManager.FindArea(string? persistentId)
	{
		if (persistentId == null)
		{
			return null;
		}
		return Layers.Select(l => l.SelfAsAreaVM).WhereNotNull().FirstOrDefault(a => a.PersistentId == persistentId);
	}

	IBlockProviderVM? Persistence.IBlockManager.FindBlock(string? persistentId)
	{
		if (persistentId == null)
		{
			return null;
		}
		return Blockdata.AllBlockVMs.FirstOrDefault(x => x.PersistentId == persistentId);
	}
}
