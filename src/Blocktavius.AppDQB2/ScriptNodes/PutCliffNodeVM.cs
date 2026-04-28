using Blocktavius.AppDQB2.Persistence;
using Blocktavius.AppDQB2.ScriptNodes.CliffDesigners;
using Blocktavius.AppDQB2.ScriptNodes.HillDesigners;
using Blocktavius.Core;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace Blocktavius.AppDQB2.ScriptNodes;

sealed class PutCliffNodeVM : ScriptLeafNodeVM, IHaveLongStatusText, IStageMutator, IDynamicScriptNodeVM
{
	[PersistentScriptNode(Discriminator = "PutCliff-6951")]
	sealed record PersistModel : IPersistentScriptNode
	{
		public required string? BlockPersistId { get; init; }
		public required int JauntX { get; init; }
		public required int JauntZ { get; init; }
		public required int JauntY { get; init; }
		public required CardinalDirection OutsideDirection { get; init; }
		public required string? LockedRandomSeed { get; init; }
		public required IPersistentCliffDesigner? CliffDesigner { get; init; }

		public bool TryDeserializeV1(out ScriptNodeVM node, ScriptDeserializationContext context)
		{
			var me = new PutCliffNodeVM();
			me.Block = context.BlockManager.FindBlock(this.BlockPersistId);
			me.JauntX = this.JauntX;
			me.JauntZ = this.JauntZ;
			me.JauntY = this.JauntY;
			me.OutsideDirection = this.OutsideDirection;
			if (this.CliffDesigner?.TryDeserializeV1(context, out var designer) ?? false)
			{
				// need to set SelectedCliffType *before* CliffDesigner!
				me.SelectedCliffType = CliffType.FindTypeOf(designer);
				me.CliffDesigner = designer;
			}
			if (this.LockedRandomSeed != null)
			{
				me.prngSeed = this.LockedRandomSeed;
				me.LockRandomSeed = true;
			}
			node = me;
			return true;
		}
	}

	public IPersistentScriptNode ToPersistModel()
	{
		return new PersistModel
		{
			BlockPersistId = this.Block?.PersistentId,
			JauntX = this.JauntX,
			JauntZ = this.JauntZ,
			JauntY = this.JauntY,
			OutsideDirection = this.OutsideDirection,
			LockedRandomSeed = this.LockRandomSeed ? this.prngSeed : null,
			CliffDesigner = this.CliffDesigner?.ToPersistModel(),
		};
	}

	ScriptNodeVM IDynamicScriptNodeVM.SelfAsVM => this;
	IStageMutator? IDynamicScriptNodeVM.SelfAsMutator => this;

	const string Common = "_Common";

	private IBlockProviderVM? blockProvider = Blockdata.AnArbitraryBlockVM;
	[Editor(typeof(PropGridEditors.BlockProviderEditor), typeof(PropGridEditors.BlockProviderEditor))]
	[Category(Common)]
	public IBlockProviderVM? Block
	{
		get => blockProvider;
		set => ChangeProperty(ref blockProvider, value);
	}

	private int jauntX;
	[Category(Common)]
	public int JauntX
	{
		get => jauntX;
		set => ChangeProperty(ref jauntX, value);
	}

	private int jauntZ;
	[Category(Common)]
	public int JauntZ
	{
		get => jauntZ;
		set => ChangeProperty(ref jauntZ, value);
	}

	private int jauntY;
	[Category(Common)]
	public int JauntY
	{
		get => jauntY;
		set => ChangeProperty(ref jauntY, value);
	}

	private CardinalDirection outsideDir;
	[Category(Common)]
	public CardinalDirection OutsideDirection
	{
		get => outsideDir;
		set => ChangeProperty(ref outsideDir, value);
	}

	private BindableRichText _longStatus = BindableRichText.Empty;
	[Browsable(false)]
	public BindableRichText LongStatus
	{
		get => _longStatus;
		set => ChangeProperty(ref _longStatus, value);
	}

	private CliffType? selectedCliffType;
	[Category(Common)]
	[ItemsSource(typeof(CliffType.PropGridItemsSource))]
	[RefreshProperties(RefreshProperties.All)]
	public CliffType? SelectedCliffType
	{
		get => selectedCliffType;
		set
		{
			ChangeProperty(ref selectedCliffType, value);
			CliffDesigner = value?.CreateNewDesigner();
		}
	}

	private ICliffDesigner? cliffDesigner;
	[Category(Common)]
	[FlattenProperties(CategoryName = "ZZZ")]
	public ICliffDesigner? CliffDesigner
	{
		get => cliffDesigner;
		private set => ChangeProperty(ref cliffDesigner, value);
	}

	private string? prngSeed = null;
	private bool lockRandomSeed;
	[Category(Common)]
	public bool LockRandomSeed
	{
		get => lockRandomSeed;
		set => ChangeProperty(ref lockRandomSeed, value);
	}

	protected override void AfterPropertyChanges()
	{
		RebuildLongStatus();
	}

	private void RebuildLongStatus()
	{
		var rtb = new BindableRichTextBuilder();
		rtb.Append("Put Cliff:");
		rtb.AppendLine().Append("  TODO:");
		LongStatus = rtb.Build();
	}

	public StageMutation? BuildMutation(StageRebuildContext context)
	{
		if (cliffDesigner == null || Block == null)
		{
			return null;
		}

		var point = new Point(new XZ(JauntX, JauntZ), JauntY);
		if (!context.TryParseJaunt(point, outsideDir, out var result))
		{
			return null;
		}

		PRNG prng;
		if (LockRandomSeed && prngSeed != null)
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
			var ctx = new CliffDesignContext
			{
				PositionedJaunt = result,
				Prng = prng,
				FillBlockId = Block.UniformBlockId.Value,
			};
			return cliffDesigner.CreateMutation(ctx);
		}
		return null;
	}
}
