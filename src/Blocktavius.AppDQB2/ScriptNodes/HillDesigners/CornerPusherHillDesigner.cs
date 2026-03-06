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

sealed class CornerPusherHillDesigner : ShellBasedHillDesigner
{
	[PersistentHillDesigner(Discriminator = "CornerPusherHill-4527")]
	sealed record PersistModel : IPersistentHillDesigner
	{
		public required int? MaxConsecutiveMisses { get; init; }
		public required int? OverhangAmount { get; init; }

		public bool TryDeserializeV1(ScriptDeserializationContext context, out IHillDesigner designer)
		{
			var me = new CornerPusherHillDesigner();
			me.MaxConsecutiveMisses = this.MaxConsecutiveMisses ?? me.MaxConsecutiveMisses;
			me.OverhangAmount = this.OverhangAmount ?? me.OverhangAmount;
			designer = me;
			return true;
		}
	}

	public override IPersistentHillDesigner ToPersistModel() => new PersistModel()
	{
		MaxConsecutiveMisses = this.MaxConsecutiveMisses,
		OverhangAmount = this.OverhangAmount,
	};

	private int _maxConsecutiveMisses = 11;
	public int MaxConsecutiveMisses
	{
		get => _maxConsecutiveMisses;
		set => ChangeProperty(ref _maxConsecutiveMisses, value);
	}

	private int _overhangAmount = -1;
	public int OverhangAmount
	{
		get => _overhangAmount;
		set => ChangeProperty(ref _overhangAmount, value);
	}

	protected override StageMutation? CreateMutation(HillDesignContext context, Shell shell)
	{
		if (shell.IsHole) { return null; }

		int overhang = Math.Clamp(OverhangAmount, 0, context.Elevation);

		var mainSettings = new CornerPusherHill.Settings
		{
			Prng = context.Prng.AdvanceAndClone(),
			MinElevation = 1,
			MaxElevation = context.Elevation - overhang,
			MaxConsecutiveMisses = this.MaxConsecutiveMisses,
		};
		var mainSampler = CornerPusherHill.BuildHill(mainSettings, shell);
		var mainMutation = StageMutation.CreateHills(mainSampler, context.FillBlockId);

		if (OverhangAmount < 1)
		{
			return mainMutation;
		}

		const int OverhangFixup = 1; // aesthetics, compensates for top layer quirkiness
									 // (That quirkiness is desirable for the non-overhang case.)

		int overhangFloor = Math.Max(1, mainSettings.MaxElevation - OverhangFixup);
		var overhangSettings = mainSettings with
		{
			Prng = context.Prng.AdvanceAndClone(),
			MaxElevation = context.Elevation - overhangFloor,
		};
		var overhangSampler = CornerPusherHill.BuildHill(overhangSettings, shell);
		var overhangMutation = new DQB2.Mutations.PutInvertedHillMutation
		{
			Block = context.FillBlockId,
			MaxElevation = overhangSettings.MaxElevation,
			Sampler = overhangSampler,
			YFloor = overhangFloor,
		};

		return StageMutation.Combine([mainMutation, overhangMutation]);
	}
}
