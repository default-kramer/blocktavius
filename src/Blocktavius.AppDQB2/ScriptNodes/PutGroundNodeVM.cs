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
			me.AreaDefiner.Area = context.AreaManager.FindArea(this.AreaPersistId);
			me.Block = context.BlockManager.FindBlock(this.BlockPersistId);
			if (CustomRectArea != null)
			{
				me.AreaDefiner.Load(CustomRectArea);
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
			AreaPersistId = this.AreaDefiner.Area?.PersistentId,
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

	[FlattenProperties]
	public AreaDefinerVM AreaDefiner { get; } = new();

	private IAreaVM? Area => AreaDefiner.Area;

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

	private RectV1? RebuildCustomRect() => AreaDefiner.RebuildCustomRect();

	public StageMutation? BuildMutation(StageRebuildContext context)
	{
		if (Block == null)
		{
			return null;
		}

		List<IArea> areas = new();
		var customRect = RebuildCustomRect()?.ToCoreRectInclusive();
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
			areas.Add(customRect.AsArea());
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
