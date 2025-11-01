using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2;

class SlotVM
{
	public required ProfileSettings.SaveSlot Slot { get; init; }

	public required IReadOnlyList<SlotStageVM> Stages { get; init; }

	public string Name => Slot.Name;

	public string FullPath => Slot.FullPath;

	protected static IReadOnlyList<SlotStageVM> GetStages(ProfileSettings.SaveSlot slot) => slot.AllFiles()
		.Where(fi => fi.Name.ToLowerInvariant().StartsWith("stgdat"))
		.Where(fi => fi.Extension.ToLowerInvariant() == ".bin")
		.Select(SlotStageVM.Create)
		.OrderBy(x => x.KnownStageSortOrder ?? int.MaxValue)
		.ThenBy(x => x.Name)
		.ToList();

	public static SlotVM Create(ProfileSettings.SaveSlot slot)
	{
		return new SlotVM()
		{
			Slot = slot,
			Stages = GetStages(slot),
		};
	}

	public string GetFullPath(string filename) => Slot.GetFullPath(filename);
}

class WritableSlotVM : SlotVM
{
	public required ProfileSettings.WritableSaveSlot WritableSlot { get; init; }

	public static WritableSlotVM Create(ProfileSettings.WritableSaveSlot slot)
	{
		return new WritableSlotVM
		{
			Slot = slot,
			WritableSlot = slot,
			Stages = GetStages(slot),
		};
	}
}

sealed class SlotStageVM
{
	public required FileInfo StgdatFile { get; init; }
	public required string Name { get; init; }

	/// <summary>
	/// Do NOT rely on this matching the number in the stgdat file.
	/// </summary>
	public required int? KnownStageSortOrder { get; init; }

	public bool IsKnownStage => KnownStageSortOrder.HasValue;
	public string Filename => StgdatFile.Name;

	public static SlotStageVM Create(FileInfo stgatFile)
	{
		var knownInfo = GetKnownStageName(stgatFile.Name);

		string name;
		int? knownOrder;
		if (knownInfo.HasValue)
		{
			name = $"{knownInfo.Value.name} ({stgatFile.Name})";
			knownOrder = knownInfo.Value.sortOrder;
		}
		else
		{
			name = stgatFile.Name;
			knownOrder = null;
		}

		return new SlotStageVM
		{
			StgdatFile = stgatFile,
			Name = name,
			KnownStageSortOrder = knownOrder,
		};
	}

	private static (string name, int sortOrder)? GetKnownStageName(string filename)
	{
		switch (filename.ToLowerInvariant())
		{
			case "stgdat01.bin": return ("Isle of Awakening", 1);
			case "stgdat02.bin": return ("Furrowfield", 2);
			case "stgdat03.bin": return ("Khrumbul-Dun", 3);
			case "stgdat04.bin": return ("Moonbrooke", 4);
			case "stgdat05.bin": return ("Malhalla", 5);
			case "stgdat09.bin": return ("Angler's Isle", 9);
			case "stgdat10.bin": return ("Skelkatraz", 10);
			case "stgdat12.bin": return ("Buildertopia 1", 12);
			case "stgdat13.bin": return ("Buildertopia 2", 13);
			case "stgdat16.bin": return ("Buildertopia 3", 16);
			default: return null;
		}
	}
}
