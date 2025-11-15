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

sealed class BubblerHillDesigner : RegionBasedHillDesigner
{
	[PersistentHillDesigner(Discriminator = "BubblerHill-5400")]
	sealed record PersistModel : IPersistentHillDesigner
	{
		public required int? BubbleFactor { get; init; }
		public required int? BubbleScale { get; init; }
		public required int? MinBubbleChance { get; init; }
		public required int? Smoothness { get; init; }

		public bool TryDeserializeV1(ScriptDeserializationContext context, out IHillDesigner designer)
		{
			var me = new BubblerHillDesigner();
			me.BubbleFactor = this.BubbleFactor ?? me.BubbleFactor;
			me.BubbleScale = this.BubbleScale ?? me.BubbleScale;
			me.MinBubbleChance = this.MinBubbleChance ?? me.MinBubbleChance;
			me.Smoothness = this.Smoothness ?? me.Smoothness;
			designer = me;
			return true;
		}
	}

	public override IPersistentHillDesigner ToPersistModel()
	{
		return new PersistModel
		{
			BubbleFactor = this.BubbleFactor,
			BubbleScale = this.BubbleScale,
			MinBubbleChance = this.MinBubbleChance,
			Smoothness = this.Smoothness,
		};
	}

	protected override StageMutation? CreateMutation(HillDesignContext context, Region region)
	{
		var settings = new BUBBLER.Settings
		{
			Prng = context.Prng.AdvanceAndClone(),
			MaxElevation = context.Elevation,
			MinElevation = 30,
			Where = region.Bounds.start,
			BubbleFactor = BubbleFactor,
			Scale = BubbleScale,
			MinBubbleChance = MinBubbleChance / 100m,
			Smoothness = Smoothness,
		};
		settings.Validate(out settings);
		BubbleFactor = settings.BubbleFactor;
		BubbleScale = settings.Scale;
		MinBubbleChance = Convert.ToInt32(settings.MinBubbleChance * 100);
		Smoothness = settings.Smoothness;
		var sampler = BUBBLER.Build(settings);
		return StageMutation.CreateHills(sampler, context.FillBlockId);
	}

	private int bubbleFactor = 3;
	public int BubbleFactor
	{
		get => bubbleFactor;
		set => ChangeProperty(ref bubbleFactor, value);
	}

	private int bubbleScale = 6;
	public int BubbleScale
	{
		get => bubbleScale;
		set => ChangeProperty(ref bubbleScale, value);
	}

	private int minBubbleChance = 10;
	public int MinBubbleChance
	{
		get => minBubbleChance;
		set => ChangeProperty(ref minBubbleChance, value);
	}

	private int smoothness = 3;
	public int Smoothness
	{
		get => smoothness;
		set => ChangeProperty(ref smoothness, value);
	}
}
