using Blocktavius.AppDQB2.Persistence;
using Blocktavius.AppDQB2.Persistence.V1;
using Blocktavius.Core;
using Blocktavius.DQB2;
using Blocktavius.DQB2.Mutations;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace Blocktavius.AppDQB2.ScriptNodes;

sealed class ReplaceLayerZeroNodeVM : ScriptLeafNodeVM, IHaveLongStatusText, IStageMutator, IDynamicScriptNodeVM
{
	IStageMutator? IDynamicScriptNodeVM.SelfAsMutator => this;
	ScriptNodeVM IDynamicScriptNodeVM.SelfAsVM => this;

	[PersistentScriptNode(Discriminator = "ReplaceLayerZero-9162")]
	sealed record PersistModel : IPersistentScriptNode
	{
		public required string? AreaPersistId { get; init; }
		public required RectV1? CustomRectArea { get; init; }

		public bool TryDeserializeV1(out ScriptNodeVM node, ScriptDeserializationContext context)
		{
			var me = new ReplaceLayerZeroNodeVM();
			me.area = context.AreaManager.FindArea(AreaPersistId);
			if (CustomRectArea != null)
			{
				me.RectAreaBeginX = CustomRectArea.X0;
				me.RectAreaBeginZ = CustomRectArea.Z0;
				me.RectAreaEndX = CustomRectArea.X1;
				me.RectAreaEndZ = CustomRectArea.Z1;
			}
			node = me;
			return true;
		}
	}

	public IPersistentScriptNode ToPersistModel()
	{
		return new PersistModel()
		{
			AreaPersistId = this.Area?.PersistentId,
			CustomRectArea = RebuildCustomRect(),
		};
	}

	private IAreaVM? area;
	[Category("Area")]
	[ItemsSource(typeof(Global.AreasItemsSource))]
	public IAreaVM? Area
	{
		get => area;
		set
		{
			if (ChangeProperty(ref area, value) && value != null)
			{
				RectAreaBeginX = null;
				RectAreaBeginZ = null;
				RectAreaEndX = null;
				RectAreaEndZ = null;
			}
		}
	}

	private int? _beginX;
	[Category("Area")]
	public int? RectAreaBeginX
	{
		get => _beginX;
		set
		{
			if (ChangeProperty(ref _beginX, value) && value.HasValue)
			{
				Area = null;
			}
		}
	}

	private int? _beginZ;
	[Category("Area")]
	public int? RectAreaBeginZ
	{
		get => _beginZ;
		set
		{
			if (ChangeProperty(ref _beginZ, value) && value.HasValue)
			{
				Area = null;
			}
		}
	}

	private int? _endX;
	[Category("Area")]
	public int? RectAreaEndX
	{
		get => _endX;
		set
		{
			if (ChangeProperty(ref _endX, value) && value.HasValue)
			{
				Area = null;
			}
		}
	}

	private int? _endZ;
	[Category("Area")]
	public int? RectAreaEndZ
	{
		get => _endZ;
		set
		{
			if (ChangeProperty(ref _endZ, value) && value.HasValue)
			{
				Area = null;
			}
		}
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
		rtb.Append("Replace Layer 0 (Bedrock):");

		string? areaText = null;
		if (Area != null)
		{
			areaText = Area.DisplayName;
		}
		else if (RebuildCustomRect() != null)
		{
			areaText = "Custom Rectangle";
		}
		rtb.AppendLine().Append("  Area: ").FallbackIfNull("None Selected", areaText);

		LongStatus = rtb.Build();
	}

	private RectV1? RebuildCustomRect()
	{
		if (RectAreaBeginX.HasValue && RectAreaBeginZ.HasValue && RectAreaEndX.HasValue && RectAreaEndZ.HasValue)
		{
			return new RectV1()
			{
				X0 = RectAreaBeginX.Value,
				Z0 = RectAreaBeginZ.Value,
				X1 = RectAreaEndX.Value,
				Z1 = RectAreaEndZ.Value,
			};
		}
		return null;
	}

	private IArea? GetArea(XZ imageCoordTranslation)
	{
		if (Area != null)
		{
			if (Area.IsArea(imageCoordTranslation, out var wrapper))
			{
				return wrapper.Area;
			}
			return null;
		}
		return RebuildCustomRect()?.ToCoreRect()?.AsArea();
	}

	public StageMutation? BuildMutation(StageRebuildContext context)
	{
		var area = GetArea(context.ImageCoordTranslation);
		if (area == null)
		{
			return null;
		}

		return new ReplaceLayerZeroMutation
		{
			Area = area,
			//ProcessNonEmptyColumns = true,
		};
	}
}
