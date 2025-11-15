using Blocktavius.AppDQB2.Persistence;
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

sealed class WinsomeHillDesigner : RegionBasedHillDesigner
{
	[PersistentHillDesigner(Discriminator = "WinsomeHill-4380")]
	sealed record PersistModel : IPersistentHillDesigner
	{
		public required int? Steepness { get; init; }

		public bool TryDeserializeV1(ScriptDeserializationContext context, out IHillDesigner designer)
		{
			var me = new WinsomeHillDesigner();
			me.Steepness = this.Steepness ?? me.Steepness;
			designer = me;
			return true;
		}
	}

	public override IPersistentHillDesigner ToPersistModel()
	{
		return new PersistModel
		{
			Steepness = this.Steepness,
		};
	}

	protected override StageMutation? CreateMutation(HillDesignContext context, Region region)
	{
		var settings = new WinsomeHill.Settings
		{
			Prng = context.Prng.AdvanceAndClone(),
			MaxElevation = context.Elevation,
			MinElevation = context.Elevation - 30,
			Steepness = Steepness,
			CornerDebug = 0,
		};
		var sampler = WinsomeHill.BuildWinsomeHills(region, settings);
		return StageMutation.CreateHills(sampler, context.FillBlockId);
	}

	private int steepness = 1;
	public int Steepness
	{
		get => steepness;
		set => ChangeProperty(ref steepness, Math.Max(1, value));
	}
}
