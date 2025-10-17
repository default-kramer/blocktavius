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

			List<StageMutation> mutations = new();
			foreach (var shell in area.Shells)
			{
				var m = CreateMutation(context with { Prng = context.Prng.AdvanceAndClone() }, shell);
				if (m != null)
				{
					mutations.Add(m);
				}
			}
			return StageMutation.Combine(mutations);
		}
		return null;
	}

	protected abstract StageMutation? CreateMutation(HillDesignContext context, Shell shell);
}
