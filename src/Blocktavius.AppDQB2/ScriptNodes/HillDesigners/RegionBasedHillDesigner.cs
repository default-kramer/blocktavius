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
	public virtual StageMutation? CreateMutation(HillDesignContext context)
	{
		// TODO can we apply the drawing offset here?
		var regions = context.AreaVM.BuildTagger().GetRegions(true);
		if (regions.Count == 0)
		{
			return null;
		}
		return CreateMutation(context, regions);
	}

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
