using Blocktavius.AppDQB2.Persistence;
using Blocktavius.Core;
using Blocktavius.Core.Generators.Hills;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.ScriptNodes.HillDesigners;

sealed class AdamantHillDesigner : RegionBasedHillDesigner
{
	[PersistentHillDesigner(Discriminator = "AdamantHill-3708")]
	sealed record PersistModel : IPersistentHillDesigner
	{
		public bool TryDeserializeV1(ScriptDeserializationContext context, out IHillDesigner designer)
		{
			designer = new AdamantHillDesigner();
			return true;
		}
	}

	public override IPersistentHillDesigner ToPersistModel() => new PersistModel();

	protected override StageMutation? CreateMutation(HillDesignContext context, Region region)
	{
		var settings = new AdamantHill.Settings
		{
			Prng = context.Prng.AdvanceAndClone(),
			CornerDebug = 0, //CornerDebug,
			MaxElevation = context.Elevation,
			CliffConfig = AdamantCliffBuilder.Config.Default,
			// Perhaps steepness should control cliffConfig.MinSeparation?
		};

		var sampler = AdamantHill.BuildAdamantHills(region, settings);
		return StageMutation.CreateHills(sampler, context.FillBlockId);
	}
}
