using Blocktavius.AppDQB2.Persistence;
using Blocktavius.Core;
using Blocktavius.Core.Generators.Hills;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.ScriptNodes.CliffDesigners;

sealed class FacileCliffDesigner : ViewModelBase, ICliffDesigner
{
	[PersistentCliffDesigner(Discriminator = "FacileCliff-7632")]
	sealed record PersistModel : IPersistentCliffDesigner
	{
		public required int BaseCliffElevation { get; init; }
		public required int MiddleHeight { get; init; }
		public required int OverhangHeight { get; init; }
		public required int OverhangDepth { get; init; }

		public bool TryDeserializeV1(ScriptDeserializationContext context, out ICliffDesigner designer)
		{
			var me = new FacileCliffDesigner();
			me.BaseCliffElevation = this.BaseCliffElevation;
			me.MiddleHeight = this.MiddleHeight;
			me.OverhangHeight = this.OverhangHeight;
			me.OverhangDepth = this.OverhangDepth;
			designer = me;
			return true;
		}
	}

	public IPersistentCliffDesigner ToPersistModel()
	{
		return new PersistModel()
		{
			BaseCliffElevation = this.BaseCliffElevation,
			MiddleHeight = this.MiddleHeight,
			OverhangHeight = this.OverhangHeight,
			OverhangDepth = this.OverhangDepth
		};
	}

	private int _baseCliffElevation;
	public int BaseCliffElevation
	{
		get => _baseCliffElevation;
		set => ChangeProperty(ref _baseCliffElevation, value);
	}

	private int _middleHeight;
	public int MiddleHeight
	{
		get => _middleHeight;
		set => ChangeProperty(ref _middleHeight, value);
	}

	private int _overhangHeight;
	public int OverhangHeight
	{
		get => _overhangHeight;
		set => ChangeProperty(ref _overhangHeight, value);
	}

	private int _overhangDepth;
	public int OverhangDepth
	{
		get => _overhangDepth;
		set => ChangeProperty(ref _overhangDepth, value);
	}

	public StageMutation? CreateMutation(CliffDesignContext context)
	{
		var baseCliffAdjust = new XZ(0, 0);
		var overhangAdjust = new XZ(0, 0);
		ushort fillBlockId = 131;

		var config = new FacileCliffBuilder.Config
		{
			BaseHeight = this.BaseCliffElevation,
			OverhangDepth = this.OverhangDepth,
			OverhangHeight = this.OverhangHeight,
			Prng = context.Prng.AdvanceAndClone(),
		};

		var jauntResult = context.PositionedJaunt;
		var result = FacileCliffBuilder.TODO(context.PositionedJaunt, config);

		var toXZ = jauntResult.Bounds.start;

		// The base cliff is EXPECTED to be deeper than the actual Jaunt!
		// That's what overhang does, it pushes the base cliff deeper.
		// Wait, should this 6 always match overhang depth exactly?
		var cliff = result.BaseCliff.TranslateTo(toXZ.Add(baseCliffAdjust))
			.Rotate(jauntResult.Rotation)
			.Project(i => i == config.BaseHeight ? config.BaseHeight + MiddleHeight : i);
		var mCliff = StageMutation.CreateHills(cliff, fillBlockId);

		var mOverhang = new DQB2.Mutations.PutInvertedHillMutation()
		{
			Block = fillBlockId,
			YFloor = config.BaseHeight + MiddleHeight + 1,
			MaxElevation = config.OverhangHeight,
			Sampler = result.OverhangSampler.TranslateTo(toXZ.Add(overhangAdjust)).Rotate(jauntResult.Rotation),
		};

		return StageMutation.Combine([mCliff, mOverhang]);
	}
}
