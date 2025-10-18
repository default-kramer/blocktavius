using Blocktavius.Core;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.ScriptNodes.HillDesigners;

abstract class RegionBasedHillDesigner : ViewModelBase, IHillDesigner
{
	public StageMutation? CreateMutation(HillDesignContext context)
	{
		if (context.AreaVM.IsRegional(out var tagger))
		{
			var regions = tagger.GetRegions(true, context.ImageCoordTranslation);
			context = context with { ImageCoordTranslation = XZ.Zero };
			return CreateMutation(context, regions);
		}

		if (context.AreaVM.IsArea(context.ImageCoordTranslation, out var area))
		{
			if (area.TryConvertToRegions(MinTileSize, out var regions))
			{
				context = context with { ImageCoordTranslation = XZ.Zero };
				return CreateMutation(context, regions);
			}
		}

		return null;
	}

	protected virtual int MinTileSize => 4;

	public virtual StageMutation? CreateMutation(HillDesignContext context, IReadOnlyList<Region> regions)
	{
		var mutations = new List<StageMutation>();
		foreach (var region in regions)
		{
			var mutation = CreateMutation(context, region);
			if (mutation != null)
			{
				mutations.Add(mutation);
			}
		}

		if (mutations.Count > 1)
		{
			return StageMutation.Combine(mutations);
		}
		return mutations.SingleOrDefault();
	}

	protected abstract StageMutation? CreateMutation(HillDesignContext context, Region region);
}
