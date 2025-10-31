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

	protected static IReadOnlyList<SlotStageVM> GetStages(ProfileSettings.SaveSlot slot) => slot.AllFiles()
		.Where(fi => fi.Name.ToLowerInvariant().StartsWith("stgdat"))
		.Where(fi => fi.Extension.ToLowerInvariant() == ".bin")
		.Select(fi => new SlotStageVM { StgdatFile = fi })
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

	public string Filename => StgdatFile.Name;

	public string Name => Filename; // TODO add friendly names here
}
