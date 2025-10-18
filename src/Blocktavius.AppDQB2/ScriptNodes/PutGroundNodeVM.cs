using Blocktavius.Core;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace Blocktavius.AppDQB2.ScriptNodes;

sealed class PutGroundNodeVM : ScriptNodeVM
{
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

	private int yMin = 37;
	public int YMin
	{
		get => yMin;
		set => ChangeProperty(ref yMin, value);
	}

	private int yMax = 45;
	public int YMax
	{
		get => yMax;
		set => ChangeProperty(ref yMax, value);
	}

	public override StageMutation? BuildMutation(StageRebuildContext context)
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
		if (yMin == yMax)
		{
			elevationSampler = new ConstantSampler<int> { Bounds = fullRect, Value = yMin };
		}
		else
		{
			var prng = PRNG.Create(new Random());

			// Because of how interpolation works, it's very rare to hit yMin or yMax.
			// So increase the range by 1 on both ends, and then clamp.
			int rangeMin = yMin - 1;
			int rangeMax = yMax + 1;
			int rangeSpan = rangeMax - rangeMin;
			elevationSampler = Interpolator2D.Create(fullRect.Size, scale, prng, defaultValue: -1)
				.Project(dubl => dubl < 0 ? -1 : Math.Clamp(rangeMin + Convert.ToInt32(dubl * rangeSpan), yMin, yMax))
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
