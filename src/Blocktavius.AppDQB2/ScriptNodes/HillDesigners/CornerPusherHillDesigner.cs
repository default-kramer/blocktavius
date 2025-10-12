using Blocktavius.Core;
using Blocktavius.Core.Generators.Hills;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.ScriptNodes.HillDesigners;

// TODO this is not really region-based, it should be shell (image) based
sealed class CornerPusherHillDesigner : RegionBasedHillDesigner
{
	protected override StageMutation? CreateMutation(HillDesignContext context, Region region)
	{
		var settings = new CornerPusherHill.Settings
		{
			Prng = context.Prng.AdvanceAndClone(),
			MinElevation = 30,
			MaxElevation = context.Elevation,
		};
		var sampler = CornerPusherHill.BuildHill(settings, region);
		return StageMutation.CreateHills(sampler, context.FillBlockId);
	}
}
