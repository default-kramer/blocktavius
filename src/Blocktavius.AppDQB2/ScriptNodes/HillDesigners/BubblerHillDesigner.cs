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

	private static StageMutation TODO(HillDesignContext context)
	{
		var prng = context.Prng.AdvanceAndClone();

		var jauntSettings = new JauntSettings()
		{
			LaneChangeDirectionProvider = RandomValues.InfiniteDeck(true, true, true, false, false, false),
			MaxLaneCount = 6,
			TotalLength = 100,
			RunLengthProvider = RandomValues.FromRange(2, 5),
		};
		var jaunt = Jaunt.Create(prng, jauntSettings);

		const int middleHeight = 12;

		var config = new FacileCliffBuilder.Config
		{
			BaseHeight = context.Elevation,
			OverhangDepth = 6,
			OverhangHeight = 14,
			Prng = prng,
		};
		var result = FacileCliffBuilder.TODO(jaunt, config);

		var toXZ = new XZ(900, 1075);

		var cliff = result.BaseCliff.TranslateTo(toXZ)
			.Project(i => i == config.BaseHeight ? config.BaseHeight + middleHeight : i);
		var mCliff = StageMutation.CreateHills(cliff, context.FillBlockId);

		var mOverhang = new DQB2.Mutations.PutInvertedHillMutation()
		{
			Block = context.FillBlockId,
			YFloor = config.BaseHeight + middleHeight + 1,
			MaxElevation = config.OverhangHeight,
			Sampler = result.OverhangSampler.TranslateTo(toXZ),
		};

		return StageMutation.Combine([mCliff, mOverhang]);
	}

	protected override StageMutation? CreateMutation(HillDesignContext context, Region region)
	{
		if (1.ToString() == "1")
		{
			return TODO(context);
		}

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
