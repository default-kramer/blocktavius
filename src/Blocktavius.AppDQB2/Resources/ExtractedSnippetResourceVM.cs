using Blocktavius.AppDQB2.ScriptNodes;
using Blocktavius.AppDQB2.Persistence.V1;
using Blocktavius.AppDQB2.Persistence;
using System.Linq;
using Blocktavius.Core;

namespace Blocktavius.AppDQB2.Resources;

sealed class ExtractedSnippetResourceVM : ViewModelBase
{
	private string _name = "New Snippet";
	public string Name
	{
		get => _name;
		set => ChangeProperty(ref _name, value);
	}

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
}
