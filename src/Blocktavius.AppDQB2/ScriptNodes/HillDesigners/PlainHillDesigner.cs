using Blocktavius.Core;
using Blocktavius.Core.Generators.Hills;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.ScriptNodes.HillDesigners;

sealed class PlainHillDesigner : RegionBasedHillDesigner
{
	protected override StageMutation? CreateMutation(HillDesignContext context, Region region)
	{
		var settings = new PlainHill.Settings
		{
			MaxElevation = context.Elevation,
			MinElevation = context.Elevation - 10,
			Steepness = this.steepness,
		};
		if (!settings.Validate(out settings))
		{
			this.Steepness = settings.Steepness;
		}
		var sampler = PlainHill.BuildPlainHill(region, settings);
		return StageMutation.CreateHills(sampler, context.FillBlockId);
	}

	private int steepness = 1;
	public int Steepness
	{
		get => steepness;
		set => ChangeProperty(ref steepness, value);
	}
}
