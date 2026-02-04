using Antipasta;
using Blocktavius.AppDQB2.Persistence.V1;
using Blocktavius.AppDQB2.Services;
using Blocktavius.Core;
using Blocktavius.DQB2;
using Blocktavius.DQB2.Mutations;
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

sealed partial class ProjectVM : ViewModelBaseWithCustomTypeDescriptor, IBlockList, IDropTarget, Persistence.IAreaManager, Persistence.IBlockManager
{
	// immutable:
	private readonly IServices services;
	private readonly IStageLoader stageLoader;
	private readonly ChunkGridLayer chunkGridLayer;
	private readonly MinimapLayer? minimapLayer;
	public ICommand CommandExportChunkMask { get; }
	public ICommand CommandExportMinimap { get; }

	// ???
	//public SourceStageVM SourceStage { get; } = new();

	// mutable:
	private ExternalImageManager? imageManager = null;

	// The 2-digit suffixes are for more greppable data binding in XAML files.
	private readonly MyProperty.Profile xProfile;
	private readonly MyProperty.ChunkExpansion xChunkExpansion;
	[ElementAsProperty("SourceSlots82")]
	private readonly I.Project.SourceSlots xSourceSlots;
	[ElementAsProperty("SelectedSourceSlot23")]
	private readonly MyProperty.SelectedSourceSlot xSelectedSourceSlot;
	[ElementAsProperty("DestSlots62")]
	private readonly I.Project.DestSlots xDestSlots;
	[ElementAsProperty("SelectedDestSlot53")]
	private readonly MyProperty.SelectedDestSlot xSelectedDestSlot;
	[ElementAsProperty("SourceStages94")]
	private readonly I.Project.SourceStages xSourceStages;
	[ElementAsProperty("SelectedSourceStage46")]
	private readonly MyProperty.SelectedSourceStage xSelectedSourceStage;
	[ElementAsProperty("SourceFullPath16")]
	private readonly I.Project.SourceFullPath xSourceFullPath;
	[ElementAsProperty("DestFullPath39")]
	private readonly I.Project.DestFullPath xDestFullPath;
	private readonly I.Project.LoadedStage xLoadedStage;
	[ElementAsProperty("Notes61")]
	private readonly MyProperty.Notes xNotes;
	// commands
	public I.Project.CommandEditChunkGrid CommandEditChunkGrid { get; }
	public ICommand CommandAddScript { get; }

	// wrapper properties
	private ProfileSettings getProfile => xProfile.Value;
	private IReadOnlyList<SlotVM> getSourceSlots => xSourceSlots.Value;
	private IReadOnlyList<WritableSlotVM> getDestSlots => xDestSlots.Value;
	public SlotVM? GetSelectedSourceSlot => xSelectedSourceSlot.Value;
	public SlotStageVM? GetSelectedSourceStage => xSelectedSourceStage.Value;
	public WritableSlotVM? GetSelectedDestSlot => xSelectedDestSlot.Value;
	private string? getSourceFullPath => xSourceFullPath.Value;

	/// <summary>
	/// Might include chunks that were already present in the STGDAT file.
	/// </summary>
	private IReadOnlySet<ChunkOffset> chunkExpansion => xChunkExpansion.Value;

	public ProjectVM(IServices services, ProfileSettings profile)
	{
		I.CreateGraphFile();

		this.services = services;
		this.stageLoader = services.StageLoader();

		xProfile = new() { InitialValue = profile, Owner = this };
		xChunkExpansion = new() { InitialValue = new HashSet<ChunkOffset>(), Owner = this };
		xSourceSlots = new MyProperty.SourceSlots(xProfile) { Owner = this };
		xDestSlots = new MyProperty.DestSlots(xProfile) { Owner = this };
		xSelectedSourceSlot = new MyProperty.SelectedSourceSlot(xProfile, xSourceSlots) { Owner = this };
		xSelectedDestSlot = new(xProfile, xDestSlots) { Owner = this };
		xSourceStages = new MyProperty.SourceStages(xSelectedSourceSlot) { Owner = this };
		xSelectedSourceStage = new MyProperty.SelectedSourceStage(xSourceStages) { Owner = this };
		xSourceFullPath = new MyProperty.SourceFullPath(xSelectedSourceStage) { Owner = this };
		xDestFullPath = new MyProperty.DestFullPath(xSelectedSourceStage, xSelectedDestSlot) { Owner = this };
		xLoadedStage = new MyProperty.LoadedStage(xSelectedSourceStage, stageLoader) { Owner = this };
		xNotes = new MyProperty.Notes() { Owner = this, InitialValue = null };
		CommandEditChunkGrid = new MyProperty.CommandEditChunkGrid(xLoadedStage, xChunkExpansion)
		{
			Owner = this,
			ProjectVM = this,
			WindowManager = services.WindowManager,
		};
		chunkGridLayer = new(xChunkExpansion, xLoadedStage);

		Layers = new();
		if (MinimapRenderer.IsEnabled)
		{
			minimapLayer = new(xSelectedSourceStage, xLoadedStage, xChunkExpansion);
			Layers.Add(minimapLayer);
		}
		Layers.Add(chunkGridLayer);
		SelectedLayer = Layers.FirstOrDefault();

		CommandExportChunkMask = new RelayCommand(_ => chunkGridLayer.ChunkGridImage != null, ExportChunkMask);
		CommandExportMinimap = new RelayCommand(_ => minimapLayer?.MinimapImage != null, ExportMinimap);
		CommandAddScript = new RelayCommand(_ => true, AddScript);

		ForceUpdateProfile(profile);
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

	private void AddScript(object? _)
	{
		string name = $"Script {Scripts.Count + 1}";
		Scripts.Add(new ScriptVM().SetScriptName(name));
	}

	public static ProjectVM CreateNew(IServices services, ProfileSettings profile, FileInfo projectFile)
	{
		var vm = new ProjectVM(services, profile);
		vm.SetProjectFilePath(projectFile.FullName);

		vm.Scripts.Add(new ScriptVM().SetScriptName("Main"));
		vm.Scripts.Add(new ScriptVM().SetScriptName("Script 2"));
		vm.SelectedScript = vm.Scripts.First();

		return vm;
	}

	internal bool BackupsEnabled(out DirectoryInfo backupDir)
	{
		backupDir = getProfile.BackupDir!;
		return backupDir != null;
	}

	private void ForceUpdateProfile(ProfileSettings profile)
	{
		SetElement(xProfile, profile);
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

	private void ExpandChunks(IReadOnlySet<ChunkOffset> expansion)
	{
		SetElement(xChunkExpansion, expansion);
	}

	public async Task<LoadStageResult?> TryLoadStage()
	{
		if (string.IsNullOrWhiteSpace(this.getSourceFullPath))
		{
			return null;
		}

		return await stageLoader.LoadStage(new FileInfo(this.getSourceFullPath));
	}

	public async Task<IMutableStage?> TryLoadMutableStage(bool expandChunks)
	{
		var loadResult = await TryLoadStage();
		if (loadResult == null)
		{
			return null;
		}

		var stage = loadResult.Stage.Clone();
		if (expandChunks)
		{
			stage.ExpandChunks(chunkExpansion);
		}
		return stage;
	}

	public async Task<IStage?> TryRebuildStage()
	{
		var workingStage = await TryLoadMutableStage(expandChunks: true);
		if (workingStage == null)
		{
			return null;
		}

		if (this.SelectedScript == Scripts.FirstOrDefault()) // NOMERGE!!
		{
			DropTheHammer(workingStage);
			return workingStage;
		}

		var context = new StageRebuildContext(workingStage);
		var mutation = this.SelectedScript?.BuildMutation(context);
		if (mutation != null)
		{
			workingStage.Mutate(mutation);
		}

		return workingStage;
	}

	private static void DropTheHammer(IMutableStage stage)
	{
		var prng = PRNG.Deserialize("1-2-3-67-67-67");

		var hill = WIP.Blah(prng.AdvanceAndClone());
		var mut = new PutHillMutation()
		{
			Block = 21,
			Sampler = hill.TranslateTo(new XZ(1093, 1118)),
			YFloor = 1,
		};

		stage.Mutate(mut);
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
			ProfileVerificationHash = getProfile.VerificationHash,
			SourceSlot = GetSelectedSourceSlot?.ToPersistModel(),
			DestSlot = GetSelectedDestSlot?.ToPersistModel(),
			SourceStgdatFilename = GetSelectedSourceStage?.Filename,
			ChunkExpansion = this.chunkExpansion.Select(ChunkOffsetV1.FromCore).ToList(),
			Notes = xNotes.Value,
			Images = Layers.OfType<ExternalImageLayerVM>().Select(vm => vm.ToPersistModel()).ToList(),
			MinimapVisible = minimapLayer?.IsVisible,
			ChunkGridVisible = chunkGridLayer.IsVisible,
			Scripts = this.Scripts.Select(s => s.ToPersistModelConcrete()).ToList(),
			SelectedScriptIndex = SelectedScript == null ? null : Scripts.IndexOf(SelectedScript),
		};
	}

	public static ProjectVM Load(IServices services, ProfileSettings profile, FileInfo projectFile)
	{
		var json = File.ReadAllText(projectFile.FullName);
		var project = ProjectV1.Load(json);
		if (project == null)
		{
			throw new Exception($"Invalid project file: {projectFile.FullName}");
		}

		var vm = new ProjectVM(services, profile);
		var imageManager = vm.SetProjectFilePath(projectFile.FullName);
		vm.Reload(project, imageManager);
		return vm;
	}

	private void Reload(ProjectV1 project, ExternalImageManager imageManager)
	{
		using var changeset = DeferChanges();

		project = project.VerifyProfileHash(getProfile);

		var wantSourceSlot = getSourceSlots.FirstOrDefault(s => s.MatchesByNumber(project.SourceSlot))
			?? getSourceSlots.FirstOrDefault(s => s.MatchesByName(project.SourceSlot));
		var wantDestSlot = getDestSlots.FirstOrDefault(s => s.MatchesByNumber(project.DestSlot))
			?? getDestSlots.FirstOrDefault(s => s.MatchesByNumber(project.DestSlot));

		SetElement(xSelectedSourceSlot, wantSourceSlot);
		SetElement(xSelectedDestSlot, wantDestSlot);

		var wantSourceStage = wantSourceSlot?.Stages.EmptyIfNull().FirstOrDefault(s =>
			string.Equals(s.Filename, project.SourceStgdatFilename, StringComparison.OrdinalIgnoreCase));
		SetElement(xSelectedSourceStage, wantSourceStage);

		SetElement(xNotes, project.Notes ?? "");
		SetElement(xChunkExpansion, project.ChunkExpansion.EmptyIfNull().Select(o => o.ToCore()).ToHashSet());

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

	static partial class MyProperty
	{
		public sealed class Profile : NotnullOriginProp<Profile, ProfileSettings>, I.Project.Profile { }

		public sealed class ChunkExpansion : NotnullOriginProp<ChunkExpansion, IReadOnlySet<ChunkOffset>>, I.Project.ChunkExpansion { }

		public sealed class Notes : NullableOriginProp<Notes, string>, I.Project.Notes { }
	}
}
