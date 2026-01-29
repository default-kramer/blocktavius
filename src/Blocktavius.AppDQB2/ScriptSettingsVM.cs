using Blocktavius.DQB2;
using Blocktavius.DQB2.Mutations;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace Blocktavius.AppDQB2;

sealed class ScriptSettingsVM : ScriptLeafNodeVM
{
	public StageMutation? BuildFinalMutation(List<StageMutation> mutations)
	{
		var mode = this.ExpandBedrock ? ColumnCleanupMode.ExpandBedrock : ColumnCleanupMode.ConstrainToBedrock;

		if (RepairSeaSettings.Enabled && RepairSeaSettings.SeaLevel > 0)
		{
			mutations.Add(new RepairSeaMutation()
			{
				ColumnCleanupMode = mode,
				SeaLevel = RepairSeaSettings.SeaLevel,
				LiquidFamily = RepairSeaSettings.GetLiquid(),
			});
			return StageMutation.Combine(mutations);
		}
		else if (mutations.Count > 0)
		{
			return StageMutation.Combine(mutations, mode);
		}
		else
		{
			// No need to perform column cleanup if nothing else happened
			return null;
		}
	}

	private bool _expandBedrock = false;
	[DisplayName("Expand Bedrock?")]
	[Description("When checked, the script will be allowed to place blocks beyond the current bounds of the stage.")]
	public bool ExpandBedrock
	{
		get => _expandBedrock;
		set => ChangeProperty(ref _expandBedrock, value);
	}

	private string? _scriptName = null;
	[DisplayName("Script Name")]
	[Description("You may give this script any name.")]
	public string? ScriptName
	{
		get => _scriptName;
		set => ChangeProperty(ref _scriptName, value);
	}

	[FlattenProperties(CategoryName = "Repair Sea")]
	public RepairSeaSettingsVM RepairSeaSettings { get; } = new();

	public sealed class RepairSeaSettingsVM : ViewModelBase
	{
		private bool _enabled;
		[DisplayName("Enabled?")]
		[Description("Check to run the Repair Sea logic after the rest of the script.")]
		public bool Enabled
		{
			get => _enabled;
			set => ChangeProperty(ref _enabled, value);
		}

		private int _seaLevel = 31;
		[ItemsSource(typeof(SeaLevelItemsSource))]
		[DisplayName("Sea Level")]
		[Description(SeaLevelDescription)]
		public int SeaLevel
		{
			get => _seaLevel;
			set => ChangeProperty(ref _seaLevel, value);
		}

		const string SeaLevelDescription = @"11 - Iridescent Island, Skelkatraz
21 - Moonbrooke
31 - IoA (home), Furrowfield, Angler's Isle, Soggy Skerry, Blossom Bay, Rimey Reef, Laguna Parfuma, Unholy Holm, Defiled Isle
65 - Khrumbul-Dun, Sunny Sands
74 - Coral Cay";

		private LiquidFamilyIndex _seaType = LiquidFamilyIndex.Seawater;
		[ItemsSource(typeof(SeaTypeItemsSource))]
		[DisplayName("Sea Type")]
		public LiquidFamilyIndex SeaType
		{
			get => _seaType;
			set => ChangeProperty(ref _seaType, value);
		}

		public DQB2.RepairSea.ILiquid GetLiquid()
		{
			if (SeaType == LiquidFamilyIndex.None)
			{
				return DQB2.RepairSea.NoLiquid.Instance;
			}
			else if (LiquidFamily.TryGetByIndex(SeaType, out var family))
			{
				return family;
			}
			else
			{
				return LiquidFamily.Seawater;
			}
		}
	}

	sealed class SeaLevelItemsSource : IItemsSource
	{
		public ItemCollection GetValues()
		{
			var items = new ItemCollection();
			items.Add(11, "11");
			items.Add(21, "21");
			items.Add(31, "31");
			items.Add(65, "65");
			items.Add(74, "74");
			return items;
		}
	}

	sealed class SeaTypeItemsSource : IItemsSource
	{
		public ItemCollection GetValues()
		{
			var items = new ItemCollection();
			items.Add(LiquidFamilyIndex.Seawater, "Seawater");
			items.Add(LiquidFamilyIndex.None, "None (remove sea)");
			items.Add(LiquidFamilyIndex.ClearWater, "Clear Water");
			items.Add(LiquidFamilyIndex.HotWater, "Hot Water");
			items.Add(LiquidFamilyIndex.Poison, "Poison");
			items.Add(LiquidFamilyIndex.Lava, "Lava");
			items.Add(LiquidFamilyIndex.BottomlessSwamp, "Bottmless Swamp");
			items.Add(LiquidFamilyIndex.MuddyWater, "Muddy Water");
			items.Add(LiquidFamilyIndex.Plasma, "Plasma");
			return items;
		}
	}
}
