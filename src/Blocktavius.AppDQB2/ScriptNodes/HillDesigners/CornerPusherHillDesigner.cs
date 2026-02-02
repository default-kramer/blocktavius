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

		public bool TryDeserializeV1(ScriptDeserializationContext context, out IHillDesigner designer)
		{
			var me = new CornerPusherHillDesigner();
			me.MaxConsecutiveMisses = this.MaxConsecutiveMisses ?? me.MaxConsecutiveMisses;
			designer = me;
			return true;
		}
	}

	public override IPersistentHillDesigner ToPersistModel() => new PersistModel()
	{
		MaxConsecutiveMisses = this.MaxConsecutiveMisses,
	};

	private int _maxConsecutiveMisses = 11;
	public int MaxConsecutiveMisses
	{
		get => _maxConsecutiveMisses;
		set => ChangeProperty(ref _maxConsecutiveMisses, value);
	}

	protected override StageMutation? CreateMutation(HillDesignContext context, Shell shell)
	{
		if (shell.IsHole) { return null; }

		var settings = new CornerPusherHill.Settings
		{
			Prng = context.Prng.AdvanceAndClone(),
			MinElevation = 1,
			MaxElevation = context.Elevation,
			MaxConsecutiveMisses = this.MaxConsecutiveMisses,
		};
		var sampler = CornerPusherHill.BuildHill(settings, shell);
		return StageMutation.CreateHills(sampler, context.FillBlockId);
	}
}
