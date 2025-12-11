using Blocktavius.AppDQB2.Persistence;
using Blocktavius.AppDQB2.ScriptNodes.HillDesigners;
using Blocktavius.Core;
using Blocktavius.Core.Generators.Hills;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace Blocktavius.AppDQB2.ScriptNodes;

sealed class PutHillNodeVM : ScriptLeafNodeVM, IHaveLongStatusText, IStageMutator, IDynamicScriptNodeVM
{
	[PersistentScriptNode(Discriminator = "PutHill-4481")]
	sealed record PersistModel : IPersistentScriptNode
	{
		public required int? Elevation { get; init; }
		public required IPersistentHillDesigner? HillDesigner { get; init; }
		public required string? AreaPersistId { get; init; }
		public required string? BlockPersistId { get; init; }
		public required bool? LockRandomSeed { get; init; }

		public bool TryDeserializeV1(out ScriptNodeVM node, ScriptDeserializationContext context)
		{
			var me = new PutHillNodeVM();
			me.Elevation = this.Elevation.GetValueOrDefault(me.Elevation);
			if (this.HillDesigner?.TryDeserializeV1(context, out var designer) == true)
			{
				// need to set SelectedHillType *before* HillDesigner!
				me.SelectedHillType = HillType.FindTypeOf(designer);
				me.HillDesigner = designer;
			}
			me.Area = context.AreaManager.FindArea(this.AreaPersistId);
			me.Block = context.BlockManager.FindBlock(this.BlockPersistId);
			node = me;
			return true;
		}
	}

	public IPersistentScriptNode ToPersistModel()
	{
		return new PersistModel
		{
			Elevation = this.Elevation,
			HillDesigner = this.HillDesigner?.ToPersistModel(),
			AreaPersistId = this.Area?.PersistentId,
			BlockPersistId = this.Block?.PersistentId,
			LockRandomSeed = this.LockRandomSeed,
		};
	}

	IStageMutator? IDynamicScriptNodeVM.SelfAsMutator => this;
	ScriptNodeVM IDynamicScriptNodeVM.SelfAsVM => this;

	const string Common = "_Common";

	public PutHillNodeVM()
	{
		RebuildLongStatus();
	}

	private HillType? selectedHillType;
	[Category(Common)]
	[ItemsSource(typeof(HillType.PropGridItemsSource))]
	[RefreshProperties(RefreshProperties.All)]
	public HillType? SelectedHillType
	{
		get => selectedHillType;
		set
		{
			ChangeProperty(ref selectedHillType, value);
			HillDesigner = value?.CreateNewDesigner();
		}
	}

	private IHillDesigner? hillDesigner;
	[Category(Common)]
	[FlattenProperties(CategoryName = "ZZZ")]
	public IHillDesigner? HillDesigner
	{
		get => hillDesigner;
		private set => ChangeProperty(ref hillDesigner, value);
	}

	private int elevation;
	[Category(Common)]
	public int Elevation
	{
		get => elevation;
		set => ChangeProperty(ref elevation, value);
	}

	private IAreaVM? area;
	[ItemsSource(typeof(Global.AreasItemsSource))]
	[Category(Common)]
	public IAreaVM? Area
	{
		get => area;
		set => ChangeProperty(ref area, value);
	}

	private IBlockProviderVM? blockProvider = Blockdata.AnArbitraryBlockVM;
	[Editor(typeof(PropGridEditors.BlockProviderEditor), typeof(PropGridEditors.BlockProviderEditor))]
	[Category(Common)]
	public IBlockProviderVM? Block
	{
		get => blockProvider;
		set => ChangeProperty(ref blockProvider, value);
	}

	private string? prngSeed = null;
	private bool lockRandomSeed;
	[Category(Common)]
	public bool LockRandomSeed
	{
		get => lockRandomSeed;
		set => ChangeProperty(ref lockRandomSeed, value);
	}

	private BindableRichText _longStatus = BindableRichText.Empty;
	[Browsable(false)]
	public BindableRichText LongStatus
	{
		get => _longStatus;
		private set => ChangeProperty(ref _longStatus, value);
	}

	protected override void AfterPropertyChanges()
	{
		RebuildLongStatus();
	}

	private void RebuildLongStatus()
	{
		var rtb = new BindableRichTextBuilder();
		rtb.Append("Put Hill:");
		rtb.AppendLine().Append("  Area: ").FallbackIfNull("None Selected", Area?.DisplayName);
		rtb.AppendLine().Append("  Kind: ").FallbackIfNull("None Selected", HillDesigner?.GetType()?.Name);
		rtb.AppendLine().Append("  Elevation: ").Append(Elevation.ToString()).Append(", Block: ").FallbackIfNull("None Selected", Block?.DisplayName);
		LongStatus = rtb.Build();
	}

	public StageMutation? BuildMutation(StageRebuildContext context)
	{
		if (area == null || Block == null || hillDesigner == null)
		{
			return null;
		}

		PRNG prng;
		if (lockRandomSeed && prngSeed != null)
		{
			prng = PRNG.Deserialize(prngSeed);
		}
		else
		{
			prng = PRNG.Create(new Random());
			prngSeed = prng.Serialize();
		}

		if (Block.UniformBlockId.HasValue)
		{
			var hillContext = new HillDesignContext()
			{
				AreaVM = area,
				FillBlockId = Block.UniformBlockId.Value,
				ImageCoordTranslation = context.ImageCoordTranslation,
				Prng = prng,
				Elevation = elevation,
			};
			return hillDesigner.CreateMutation(hillContext);
		}

		return null; // TODO support mottlers....
	}
}