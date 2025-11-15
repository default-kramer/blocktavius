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

sealed class PutGroundNodeVM : ScriptLeafNodeVM, IHaveLongStatusText, IStageMutator, IDynamicScriptNodeVM
{
	[Persistence.PersistentScriptNode(Discriminator = "PutGround-3656")]
	sealed record PersistModel : Persistence.IPersistentScriptNode
	{
		public required int? Scale { get; init; }
		public required int? YMin { get; init; }
		public required int? YRange { get; init; }

		public bool TryDeserializeV1(out ScriptNodeVM node, ScriptDeserializationContext context)
		{
			var me = new PutGroundNodeVM();
			me.Scale = this.Scale.GetValueOrDefault(me.Scale);
			me.YMin = this.YMin.GetValueOrDefault(me.YMin);
			me.YRange = this.YRange.GetValueOrDefault(me.YRange);
			node = me;
			return true;
		}
	}

	public Persistence.IPersistentScriptNode ToPersistModel()
	{
		return new PersistModel
		{
			Scale = this.Scale,
			YMin = this.YMin,
			YRange = this.YRange,
		};
	}

	IStageMutator? IDynamicScriptNodeVM.SelfAsMutator => this;
	ScriptNodeVM IDynamicScriptNodeVM.SelfAsVM => this;

	public PutGroundNodeVM()
	{
		RebuildLongStatus();
	}

	private IAreaVM? area;
	[ItemsSource(typeof(Global.AreasItemsSource))]
	public IAreaVM? Area
	{
		get => area;
		set => ChangeProperty(ref area, value);
	}

	private int scale = 23;
	public int Scale
	{
		get => scale;
		set => ChangeProperty(ref scale, value);
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
		rtb.AppendLine().Append("  Area: ").FallbackIfNull("None Selected", Area?.DisplayName);
		rtb.AppendLine().Append($"  Min Elevation: {YMin}, Max Elevation: {YMax}");
		LongStatus = rtb.Build();
	}

	public StageMutation? BuildMutation(StageRebuildContext context)
	{
		if (area == null)
		{
			return null;
		}

		List<IArea> areas = new();
		if (area.IsArea(context.ImageCoordTranslation, out var areaWrapper))
		{
			areas.Add(areaWrapper.Area);
		}
		else if (area.IsRegional(out var tagger))
		{
			var regions = tagger.GetRegions(true, context.ImageCoordTranslation);
			areas.AddRange(regions);
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

		var mutations = new List<StageMutation>();
		foreach (var area in areas)
		{
			var sampler = area.AsSampler().Project((inArea, xz) => inArea ? elevationSampler.Sample(xz) : -1);
			mutations.Add(StageMutation.CreateHills(sampler, 500));
		}

		return StageMutation.Combine(mutations);
	}
}
