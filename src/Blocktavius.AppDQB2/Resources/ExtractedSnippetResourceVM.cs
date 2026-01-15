using Blocktavius.AppDQB2.ScriptNodes;
using Blocktavius.AppDQB2.Persistence.V1;
using Blocktavius.AppDQB2.Persistence;
using System.Linq;
using Blocktavius.Core;
using Blocktavius.DQB2;

namespace Blocktavius.AppDQB2.Resources;

interface ISnippetVM
{
	Snippet? LoadSnippet(StageRebuildContext context);
}

sealed class ExtractedSnippetResourceVM : ViewModelBase, ISnippetVM
{
	private string _name = "New Snippet";
	public string Name
	{
		get => _name;
		set => ChangeProperty(ref _name, value);
	}

	public string PersistentId { get; private set; } = Guid.NewGuid().ToString();

	private SlotVM? _sourceSlot;
	public SlotVM? SourceSlot
	{
		get => _sourceSlot;
		set
		{
			if (ChangeProperty(ref _sourceSlot, value))
			{
				SourceStage = null;
			}
		}
	}

	private SlotStageVM? _sourceStage;
	public SlotStageVM? SourceStage
	{
		get => _sourceStage;
		set => ChangeProperty(ref _sourceStage, value);
	}

	public AreaDefinerVM AreaDefiner { get; } = new();

	public ExtractedSnippetV1 ToPersistModel()
	{
		return new ExtractedSnippetV1
		{
			PersistentId = this.PersistentId,
			Name = this.Name,
			SourceSlot = this.SourceSlot?.ToPersistModel(),
			SourceStgdatFilename = this.SourceStage?.Filename,
			AreaPersistentId = this.AreaDefiner.Area?.PersistentId,
			CustomRectArea = this.AreaDefiner.RebuildCustomRect(),
		};
	}

	public static ExtractedSnippetResourceVM Load(ExtractedSnippetV1 persistModel, ResourceDeserializationContext context)
	{
		var vm = new ExtractedSnippetResourceVM
		{
			PersistentId = persistModel.PersistentId ?? Guid.NewGuid().ToString(),
			Name = persistModel.Name ?? "Unnamed Snippet",
		};

		// Lookup Source Slot
		if (persistModel.SourceSlot != null)
		{
			vm.SourceSlot = context.Slots.FirstOrDefault(
				s => s.MatchesByNumber(persistModel.SourceSlot) || s.MatchesByName(persistModel.SourceSlot));
		}

		// Lookup Source Stage
		if (persistModel.SourceStgdatFilename != null && vm.SourceSlot != null)
		{
			vm.SourceStage = vm.SourceSlot.Stages.EmptyIfNull().FirstOrDefault(
				s => string.Equals(s.Filename, persistModel.SourceStgdatFilename, StringComparison.OrdinalIgnoreCase));
		}

		// AreaDefiner
		if (persistModel.AreaPersistentId != null)
		{
			vm.AreaDefiner.Area = context.AreaManager.FindArea(persistModel.AreaPersistentId);
		}
		else if (persistModel.CustomRectArea != null)
		{
			vm.AreaDefiner.Load(persistModel.CustomRectArea);
		}

		return vm;
	}

	public const int TODO_Y_ADJUST = 1; // skip bedrock

	public Snippet? LoadSnippet(StageRebuildContext context)
	{
		if (SourceStage == null)
		{
			return null;
		}

		var bounds = AreaDefiner.RebuildCustomRect()?.ToCoreRect();
		IArea? area = null;
		if (bounds == null && true == AreaDefiner.Area?.IsArea(context.ImageCoordTranslation, out var areaWrapper))
		{
			area = areaWrapper.Area;
			bounds = areaWrapper.Area.Bounds;
		}
		if (bounds == null)
		{
			return null;
		}

		var loadResult = context.StageLoader.LoadStage(SourceStage.StgdatFile).GetAwaiter().GetResult();
		if (loadResult?.Stage == null)
		{
			return null;
		}

		// TODO!!! Need to respect area if not null here!
		// Probably Snippet.Create should accept an IArea or a Rect
		var snippet = Snippet.Create(loadResult.Stage, bounds, floorY: TODO_Y_ADJUST);
		return snippet;
	}
}
