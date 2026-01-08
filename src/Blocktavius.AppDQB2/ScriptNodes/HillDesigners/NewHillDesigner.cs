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

sealed class NewHillDesigner : ShellBasedHillDesigner
{
	public override IPersistentHillDesigner ToPersistModel()
	{
		throw new NotImplementedException();
	}

	protected override StageMutation? CreateMutation(HillDesignContext context, Shell shell)
	{
		if (shell.IsHole)
		{
			return null;
		}

		var settings = new NewHill.Settings()
		{
			MaxElevation = 80,
			MinElevation = 60,
			PRNG = context.Prng.AdvanceAndClone(),
		};
		var hill = NewHill.BuildNewHill(settings, shell);
		return new Mutation { Sampler = hill };
	}

	class Mutation : StageMutation
	{
		public required I2DSampler<NewHill.HillItem> Sampler { get; init; }

		public override void Apply(IMutableStage stage)
		{
			foreach (var chunk in Enumerate(Sampler.Bounds, stage))
			{
				foreach (var xz in chunk.Offset.Bounds.Intersection(Sampler.Bounds).Enumerate())
				{
					var item = Sampler.Sample(xz);
					if (item.Elevation > 0)
					{
						ushort blockId;
						if (item.Slab != null)
						{
							blockId = Convert.ToUInt16(item.Slab.AncestorCount % 2 + 4);
						}
						else
						{
							blockId = 3;
						}

						for (int y = 1; y <= item.Elevation; y++)
						{
							chunk.SetBlock(new Point(xz, y), blockId);
						}
					}
				}
			}
		}
	}
}
