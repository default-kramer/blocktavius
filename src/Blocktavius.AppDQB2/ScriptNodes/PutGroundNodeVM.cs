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

sealed class PutGroundNodeVM : ScriptLeafNodeVM, IHaveLongStatusText, IStageMutator, IDynamicScriptNodeVM
{
	[PersistentScriptNode(Discriminator = "PutGround-3656")]
	sealed record PersistModel : IPersistentScriptNode
	{
		public required int? Scale { get; init; }
		public required int? YMin { get; init; }
		public required int? YRange { get; init; }
		public required int? YFloor { get; init; }
		public required string? AreaPersistId { get; init; }
		public required string? BlockPersistId { get; init; }
		public required RectV1? CustomRectArea { get; init; }

		public bool TryDeserializeV1(out ScriptNodeVM node, ScriptDeserializationContext context)
		{
			var me = new PutGroundNodeVM();
			me.Scale = this.Scale.GetValueOrDefault(me.Scale);
			me.YMin = this.YMin.GetValueOrDefault(me.YMin);
			me.YRange = this.YRange.GetValueOrDefault(me.YRange);
			me.YFloor = this.YFloor ?? me.YFloor;
			me.Area = context.AreaManager.FindArea(this.AreaPersistId);
			me.Block = context.BlockManager.FindBlock(this.BlockPersistId);
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
		return new PersistModel
		{
			Scale = this.Scale,
			YMin = this.YMin,
			YRange = this.YRange,
			YFloor = this.YFloor,
			AreaPersistId = this.Area?.PersistentId,
			BlockPersistId = this.Block?.PersistentId,
			CustomRectArea = this.RebuildCustomRect(),
		};
	}

	IStageMutator? IDynamicScriptNodeVM.SelfAsMutator => this;
	ScriptNodeVM IDynamicScriptNodeVM.SelfAsVM => this;

	public PutGroundNodeVM()
	{
		RebuildLongStatus();
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

	private IBlockProviderVM? blockProvider = Blockdata.AnArbitraryBlockVM;
	[Editor(typeof(PropGridEditors.BlockProviderEditor), typeof(PropGridEditors.BlockProviderEditor))]
	public IBlockProviderVM? Block
	{
		get => blockProvider;
		set => ChangeProperty(ref blockProvider, value);
	}

	private int scale = 23;
	public int Scale
	{
		get => scale;
		set => ChangeProperty(ref scale, value);
	}

	private int _yFloor = 1;
	public int YFloor
	{
		get => _yFloor;
		set => ChangeProperty(ref _yFloor, value);
	}

	private int _yMin = 37;
	public int YMin
	{
		get => _yMin;
		set => ChangeProperty(ref _yMin, value, nameof(YMin), nameof(YMax));
	}

	public int YMax => YMin + YRange - 1;

	private int _yRange = 5;
	public int YRange
	{
		get => _yRange;
		set => ChangeProperty(ref _yRange, Math.Max(1, value), nameof(YRange), nameof(YMax));
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
		rtb.Append("Put Ground:");

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

		rtb.AppendLine().Append("  Block: ").FallbackIfNull("None Selected", Block?.DisplayName);
		rtb.AppendLine().Append($"  Min Elevation: {YMin}, Max Elevation: {YMax}");
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

	public StageMutation? BuildMutation(StageRebuildContext context)
	{
		if (Block == null)
		{
			return null;
		}

		List<IArea> areas = new();
		var customRect = RebuildCustomRect()?.ToCoreRect();
		if (Area != null)
		{
			if (Area.IsArea(context.ImageCoordTranslation, out var areaWrapper))
			{
				areas.Add(areaWrapper.Area);
			}
			else if (Area.IsRegional(out var tagger))
			{
				var regions = tagger.GetRegions(true, context.ImageCoordTranslation);
				areas.AddRange(regions);
			}
		}
		else if (customRect != null)
		{
			// make end inclusive
			areas.Add(new Rect(customRect.start, customRect.end.Add(1, 1)).AsArea());
		}

		if (areas.Count == 0)
		{
			return null;
		}

		var fullRect = Rect.Union(areas.Select(a => a.Bounds));
		I2DSampler<int> elevationSampler;
		if (YMin == YMax)
		{
			elevationSampler = new ConstantSampler<int> { Bounds = fullRect, Value = YMin };
		}
		else
		{
			var prng = PRNG.Create(new Random());

			// Because of how interpolation works, it's very rare to hit yMin or yMax.
			// So increase the range by 1 on both ends, and then clamp.
			int rangeMin = YMin - 1;
			int rangeMax = YMax + 1;
			int rangeSpan = rangeMax - rangeMin;
			elevationSampler = Interpolator2D.Create(fullRect.Size, scale, prng, defaultValue: -1)
				.Project(dubl => dubl < 0 ? -1 : Math.Clamp(rangeMin + Convert.ToInt32(dubl * rangeSpan), YMin, YMax))
				.TranslateTo(fullRect.start);
		}

		if (Block.UniformBlockId == null)
		{
			throw new Exception($"TODO - need to handle {Block.GetType()}");
		}

		var mutations = new List<StageMutation>();
		foreach (var area in areas)
		{
			var sampler = area.AsSampler().Project((inArea, xz) => inArea ? elevationSampler.Sample(xz) : -1);
			mutations.Add(new PutHillMutation()
			{
				Block = Block.UniformBlockId.Value,
				Sampler = sampler,
				YFloor = YFloor,
			});
		}

		return StageMutation.Combine(mutations);
	}
}
