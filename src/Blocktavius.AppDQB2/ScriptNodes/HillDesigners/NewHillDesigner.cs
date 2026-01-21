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

	private int _minRunLength = 4;
	public int MinRunLength
	{
		get => _minRunLength;
		set => ChangeProperty(ref _minRunLength, value);
	}

	private int _maxRunLength = 15;
	public int MaxRunLength
	{
		get => _maxRunLength;
		set => ChangeProperty(ref _maxRunLength, value);
	}

	private int _coveragePerTier = 80;
	public int CoveragePerTier
	{
		get => _coveragePerTier;
		set => ChangeProperty(ref _coveragePerTier, Math.Clamp(value, 0, 100));
	}

	private int _minDrop = 1;
	public int MinDrop
	{
		get => _minDrop;
		set => ChangeProperty(ref _minDrop, value);
	}

	private int _maxDrop = 3;
	public int MaxDrop
	{
		get => _maxDrop;
		set => ChangeProperty(ref _maxDrop, value);
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
			MinElevation = 14,
			MinRunLength = MinRunLength,
			RunLengthRand = Math.Max(0, 1 + MaxRunLength - MinRunLength),
			RequiredCoveragePerTier = CoveragePerTier,
			PRNG = context.Prng.AdvanceAndClone(),
			MinDrop = MinDrop,
			DropRand = Math.Max(0, 1 + MaxDrop - MinDrop),
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
						ushort topperId;
						if (item.Slab != null)
						{
							topperId = Convert.ToUInt16(item.Slab.AncestorCount % 2 + 4);
						}
						else
						{
							topperId = 3;
						}

						for (int y = 1; y < item.Elevation; y++)
						{
							chunk.SetBlock(new Point(xz, y), 5);
						}
						chunk.SetBlock(new Point(xz, item.Elevation), topperId);
					}
				}
			}
		}
	}
}
