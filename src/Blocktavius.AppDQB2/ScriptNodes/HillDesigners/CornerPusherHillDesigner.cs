﻿using Blocktavius.Core;
using Blocktavius.Core.Generators.Hills;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.ScriptNodes.HillDesigners;

sealed class CornerPusherHillDesigner : ShellBasedHillDesigner
{
	protected override StageMutation? CreateMutation(HillDesignContext context, Shell shell)
	{
		if (shell.IsHole) { return null; }

		var settings = new CornerPusherHill.Settings
		{
			Prng = context.Prng.AdvanceAndClone(),
			MinElevation = 30,
			MaxElevation = context.Elevation,
		};
		var sampler = CornerPusherHill.BuildHill(settings, shell);
		return StageMutation.CreateHills(sampler, context.FillBlockId);
	}
}
