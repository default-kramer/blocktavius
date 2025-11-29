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
using ViewmodelDeputy;

namespace Blocktavius.AppDQB2;

[ViewmodelDeputy.DeputizedVM]
sealed partial class ProjectVM : ViewModelBase, IBlockList, IDropTarget, Persistence.IAreaManager, Persistence.IBlockManager
{
	// immutable (probably "constant" would be better?):
	private readonly string ProjectFilePath; // for now let's just assume SaveAs() will create a new VM
	private readonly ChunkGridLayer chunkGridLayer = new();
	private readonly MinimapLayer? minimapLayer;
	private readonly StgdatLoader stgdatLoader = new();
	private readonly ExternalImageManager imageManager;
	public IReadOnlyList<InclusionModeVM> InclusionModes { get; } = InclusionModeVM.BuildChoices().ToList();

	// observable stuff... TODO what to do with this?
	public ICommand CommandExportChunkMask { get; }
	public ICommand CommandExportMinimap { get; }
	public ObservableCollection<ILayerVM> Layers { get; }
	public ObservableCollection<ScriptVM> Scripts { get; } = new();

	// TODO - every settable property Depends On Nothing, correct?
	// mutable:
	private bool _includeStgdatInPreview = true;
	[AssertDependsOn(AssertDependsOnAttribute.Nothing)]
	public bool IncludeStgdatInPreview
	{
		get => _includeStgdatInPreview;
		set => ChangeProperty(ref _includeStgdatInPreview, value, MyProperties.IncludeStgdatInPreview);
	}

	private ILayerVM? _selectedLayer;
	[AssertDependsOn(AssertDependsOnAttribute.Nothing)]
	public ILayerVM? SelectedLayer
	{
		get => _selectedLayer;
		set => ChangeProperty(ref _selectedLayer, value, MyProperties.SelectedLayer);
	}

	private string _notes = "";
	[AssertDependsOn(AssertDependsOnAttribute.Nothing)]
	public string Notes
	{
		get => _notes;
		set => ChangeProperty(ref _notes, value, MyProperties.Notes);
	}

	private IReadOnlySet<ChunkOffset> _chunkExpansion = ImmutableHashSet<ChunkOffset>.Empty;
	/// <summary>
	/// Might include chunks that were already present in the STGDAT file.
	/// </summary>
	[AssertDependsOn(AssertDependsOnAttribute.Nothing)]
	public IReadOnlySet<ChunkOffset> ChunkExpansion
	{
		get => _chunkExpansion;
		private set => ChangeProperty(ref _chunkExpansion, value, MyProperties.ChunkExpansion);
	}

	private ProfileSettings _profile;
	[AssertDependsOn(AssertDependsOnAttribute.Nothing)]
	private ProfileSettings Profile
	{
		get => _profile;
		set => ChangeProperty(ref _profile, value, MyProperties.Profile);
	}

	private ScriptVM? _selectedScript;
	[AssertDependsOn(AssertDependsOnAttribute.Nothing)]
	public ScriptVM? SelectedScript
	{
		get => _selectedScript;
		set
		{
			var prev = _selectedScript;
			if (ChangeProperty(ref _selectedScript, value, MyProperties.SelectedScript))
			{
				value?.SetActive(true);
				prev?.SetActive(false);
			}
		}
	}

	private SlotVM? _selectedSourceSlot = null;
	[AssertDependsOn(AssertDependsOnAttribute.Nothing)]
	public SlotVM? SelectedSourceSlot
	{
		get => _selectedSourceSlot;
		set
		{
			var prev = SelectedSourceStage;
			if (ChangeProperty(ref _selectedSourceSlot, value, MyProperties.SelectedSourceSlot))
			{
				SelectedSourceStage = value?.Stages?.FirstOrDefault(s => s.Filename.Equals(prev?.Filename, StringComparison.OrdinalIgnoreCase));
			}
		}
	}

	private SlotStageVM? _selectedSourceStage = null;
	[AssertDependsOn(AssertDependsOnAttribute.Nothing)]
	public SlotStageVM? SelectedSourceStage
	{
		get => _selectedSourceStage;
		set
		{
			if (ChangeProperty(ref _selectedSourceStage, value, MyProperties.SelectedSourceStage))
			{
				RebuildImages();
			}
		}
	}

	// TODO - InclusionModes is constant, so it doesn't feel like it should be considered
	// a dependency here, right?
	private InclusionModeVM? _selectedInclusionMode;
	[AssertDependsOn(AssertDependsOnAttribute.Nothing)]
	public InclusionModeVM SelectedInclusionMode
	{
		get => _selectedInclusionMode ?? InclusionModes.First();
		set => ChangeProperty(ref _selectedInclusionMode, value);
	}

	private WritableSlotVM? _selectedDestSlot = null;
	[AssertDependsOn(AssertDependsOnAttribute.Nothing)]
	public WritableSlotVM? SelectedDestSlot
	{
		get => _selectedDestSlot;
		set => ChangeProperty(ref _selectedDestSlot, value, nameof(SelectedDestSlot), nameof(DestFullPath));
	}

	public ProjectVM(ProfileSettings profile, FileInfo projectFile)
	{
		ProjectFilePath = projectFile.FullName;
		Layers = new();
		if (MinimapRenderer.IsEnabled)
		{
			minimapLayer = new();
			Layers.Add(minimapLayer);
		}
		Layers.Add(chunkGridLayer);
		SelectedLayer = Layers.FirstOrDefault();

		this._profile = profile;
		ForceUpdateProfile(profile);

		imageManager = new ExternalImageManager(new DirectoryInfo(projectFile.Directory?.FullName ?? "....fail"));

		CommandExportChunkMask = new RelayCommand(_ => chunkGridLayer.ChunkGridImage != null, ExportChunkMask);
		CommandExportMinimap = new RelayCommand(_ => minimapLayer?.MinimapImage != null, ExportMinimap);
	}

	[AssertDependsOn(nameof(ProjectFilePath))]
	public string ProjectFilePathToDisplay => string.IsNullOrWhiteSpace(ProjectFilePath) ? "<< set during Save >>" : ProjectFilePath;

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
		var vm = new ProjectVM(profile, projectFile);

		vm.Scripts.Add(new ScriptVM().SetScriptName("Main"));
		vm.Scripts.Add(new ScriptVM().SetScriptName("Script 2"));
		vm.SelectedScript = vm.Scripts.First();

		return vm;
	}

	internal bool BackupsEnabled(out DirectoryInfo backupDir)
	{
		backupDir = Profile.BackupDir!;
		return backupDir != null;
	}

	public void SwitchProfile(ProfileSettings newProfile)
	{
		if (Profile.VerificationHash != newProfile.VerificationHash)
		{
			ForceUpdateProfile(newProfile);
		}
	}

	[AssertDependsOn(nameof(Profile))]
	[ComputedProperty(PropertyName = "SourceSlots")]
	private IReadOnlyList<SlotVM> ComputeSourceSlots()
	{
		return Profile.SaveSlots.Select(SlotVM.Create).ToList();
	}

	[AssertDependsOn(nameof(Profile))]
	[ComputedProperty(PropertyName = "DestSlots")]
	private IReadOnlyList<WritableSlotVM> ComputeDestSlots()
	{
		return Profile.WritableSaveSlots.Select(WritableSlotVM.Create).ToList();
	}

	private void ForceUpdateProfile(ProfileSettings profile)
	{
		Profile = profile;
		SelectedSourceSlot = null;
		SelectedDestSlot = null;
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

	[AssertDependsOn(AssertDependsOnAttribute.Nothing)]
	IReadOnlyList<BlockVM> IBlockList.Blocks => Blockdata.AllBlockVMs;

	public ExternalImageManager? ImageManager() => imageManager;

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

	sealed record LoadedStageInput
	{
		public required StgdatLoader Loader { get; init; } // constant, always reference-equal to itself
		public required string? StgdatFilePath { get; init; }
	}

	private LoadedStageInput _LoadedStageInput => new()
	{
		Loader = this.stgdatLoader,
		StgdatFilePath = this.StgdatFilePath,
	};

	[AssertDependsOn(nameof(StgdatFilePath))]
	// Maybe: If there is only one property having type LoadedStageInput it will be used as Input by default?
	[ComputedProperty(PropertyName = "LoadedStage", AccessModifier = "private", Input = nameof(_LoadedStageInput))]
	private static async Task _GetLoadedStageAsync(IAsyncGetterContext<LoadedStageInput, StgdatLoader.LoadResult> ctx)
	{
		ctx.SetValue(null);
		if (string.IsNullOrWhiteSpace(ctx.Input.StgdatFilePath))
		{
			return;
		}

		// TODO should check if `IsCached(out result)` and if it is cached
		// then SetValue(result) and return without calling Unblock()

		ctx.Unblock();

		// TODO should actually make TryLoad async now:
		if (ctx.Input.Loader.TryLoad(ctx.Input.StgdatFilePath, out var result, out _))
		{
			ctx.SetValue(result);
		}

		await Task.CompletedTask;
	}

	// TODO - just make the property public, right?
	public bool TryLoadStage(out StgdatLoader.LoadResult loadResult)
	{
		loadResult = LoadedStage!;
		return loadResult != null;
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

	[AssertDependsOn(nameof(SelectedSourceSlot))]
	public IReadOnlyList<SlotStageVM> SourceStages => SelectedSourceSlot?.Stages ?? Array.Empty<SlotStageVM>();

	[AssertDependsOn(nameof(SelectedSourceStage))]
	public string? StgdatFilePath => SelectedSourceStage?.StgdatFile?.FullName;

	[AssertDependsOn(nameof(SelectedDestSlot), nameof(SelectedSourceStage))]
	public string? DestFullPath
	{
		get
		{
			if (SelectedDestSlot != null && SelectedSourceStage != null)
			{
				// TODO: Warn/Error if Filename is not constant relative to SelectedSourceStage ??
				return SelectedDestSlot.GetFullPath(SelectedSourceStage.Filename);
			}
			return null;
		}
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
			ProfileVerificationHash = Profile.VerificationHash,
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

		var vm = new ProjectVM(profile, projectFile);
		vm.Reload(project);
		return vm;
	}

	private void Reload(ProjectV1 project) // TODO why is this unused now?
	{
		using var changeset = DeferChanges();

		project = project.VerifyProfileHash(Profile);

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
