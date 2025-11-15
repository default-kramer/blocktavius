using Blocktavius.AppDQB2.Persistence;
using Blocktavius.Core;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.ScriptNodes.HillDesigners;

abstract class ShellBasedHillDesigner : ViewModelBase, IHillDesigner
{
	public StageMutation? CreateMutation(HillDesignContext context)
	{
		if (context.AreaVM.IsArea(context.ImageCoordTranslation, out var area))
		{
			context = context with { ImageCoordTranslation = XZ.Zero };
			return CreateMutation(context, area.Shells);
		}

		if (context.AreaVM.IsRegional(out var tagger))
		{
			var regions = tagger.GetRegions(true, context.ImageCoordTranslation);
			context = context with { ImageCoordTranslation = XZ.Zero };
			var shells = regions.SelectMany(ShellLogic.ComputeShells).ToList();
			return CreateMutation(context, shells);
		}

		return null;
	}

	private StageMutation? CreateMutation(HillDesignContext context, IReadOnlyList<Shell> shells)
	{
		List<StageMutation> mutations = new();
		foreach (var shell in shells)
		{
			var m = CreateMutation(context with { Prng = context.Prng.AdvanceAndClone() }, shell);
			if (m != null)
			{
				mutations.Add(m);
			}
		}
		return StageMutation.Combine(mutations);
	}

	protected abstract StageMutation? CreateMutation(HillDesignContext context, Shell shell);

	public abstract IPersistentHillDesigner ToPersistModel();
}
