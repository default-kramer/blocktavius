using Blocktavius.AppDQB2.Persistence.V1;
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

	internal SlotReferenceV1 ToPersistModel()
	{
		return new SlotReferenceV1()
		{
			SlotNumber = Slot.SlotNumber,
			SlotName = Slot.Name,
		};
	}

	internal bool MatchesByNumber(SlotReferenceV1? slotRef)
	{
		return slotRef != null
			&& slotRef.SlotNumber.HasValue
			&& slotRef.SlotNumber == this.Slot.SlotNumber;
	}

	internal bool MatchesByName(SlotReferenceV1? slotRef)
	{
		return slotRef != null
			&& slotRef.SlotName == this.Slot.Name;
	}
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

	public required IReadOnlyList<int> MinimapIslandIds { get; init; }

	public bool IsKnownStage => KnownStageSortOrder.HasValue;
	public string Filename => StgdatFile.Name;

	public static SlotStageVM Create(FileInfo stgatFile)
	{
		var knownInfo = GetKnownStageName(stgatFile.Name);

		string name;
		int? knownOrder;
		IReadOnlyList<int> minimapIds;
		if (knownInfo != null)
		{
			name = $"{knownInfo.Name} ({stgatFile.Name})";
			knownOrder = knownInfo.SortOrder;
			minimapIds = knownInfo.MinimapIslandIds;
		}
		else
		{
			name = stgatFile.Name;
			knownOrder = null;
			minimapIds = [];
		}

		return new SlotStageVM
		{
			StgdatFile = stgatFile,
			Name = name,
			KnownStageSortOrder = knownOrder,
			MinimapIslandIds = minimapIds,
		};
	}

	sealed record KnownStageInfo
	{
		public required string Name { get; init; }
		public required int SortOrder { get; init; }
		public required IReadOnlyList<int> MinimapIslandIds { get; init; }
	}

	private static KnownStageInfo? GetKnownStageName(string filename)
	{
		/* Sapphire's minimap Python script has this for island IDs:
Island_names = ((0,"Isle of\nAwakening\n\nからっぽ島","aqua"),(1,"Furrowfield\n\nモンゾーラ島","medium sea green"),
			(2,"Khrumbul-Dun\nオッカムル島","orange"),(20,"Upper level","#C47400"),(21,"Lower level","#944D00"),
			(3,"Moonbrooke\n\nムーンブルク島","#00FFC0"),(4,"Malhalla\n\n破壊天体シドー","#BBBBFF"),
			(8,"Skelkatraz\n\n監獄島","grey"),(10,"Buildertopia 1\n\nかいたく島1","#FFDDDD"),(11,"Buildertopia 2\n\nかいたく島2","#FFE6DD"),
			(13,"Buildertopia 3\n\nかいたく島3","#FFEEDD"),(7,"Angler's Isle\n\nツリル島","#DDF0FF"),(12,"Battle Atoll   バトル島","#FF9999"))
		*/

		switch (filename.ToLowerInvariant())
		{
			case "stgdat01.bin":
				return new KnownStageInfo
				{
					Name = "Isle of Awakening",
					SortOrder = 1,
					MinimapIslandIds = [0],
				};
			case "stgdat02.bin":
				return new KnownStageInfo
				{
					Name = "Furrowfield",
					SortOrder = 2,
					MinimapIslandIds = [1],
				};
			case "stgdat03.bin":
				return new KnownStageInfo
				{
					Name = "Khrumbul-Dun",
					SortOrder = 3,
					MinimapIslandIds = [2, 20, 21],
				};
			case "stgdat04.bin":
				return new KnownStageInfo
				{
					Name = "Moonbrooke",
					SortOrder = 4,
					MinimapIslandIds = [3],
				};
			case "stgdat05.bin":
				return new KnownStageInfo
				{
					Name = "Malhalla",
					SortOrder = 5,
					MinimapIslandIds = [4],
				};
			case "stgdat09.bin":
				return new KnownStageInfo
				{
					Name = "Angler's Isle",
					SortOrder = 9,
					MinimapIslandIds = [7],
				};
			case "stgdat10.bin":
				return new KnownStageInfo
				{
					Name = "Skelkatraz",
					SortOrder = 10,
					MinimapIslandIds = [8],
				};
			case "stgdat12.bin":
				return new KnownStageInfo
				{
					Name = "Buildertopia 1",
					SortOrder = 12,
					MinimapIslandIds = [10],
				};
			case "stgdat13.bin":
				return new KnownStageInfo
				{
					Name = "Buildertopia 2",
					SortOrder = 13,
					MinimapIslandIds = [11],
				};
			case "stgdat16.bin":
				return new KnownStageInfo
				{
					Name = "Buildertopia 3",
					SortOrder = 16,
					MinimapIslandIds = [13],
				};
			default: return null;
		}
	}
}
