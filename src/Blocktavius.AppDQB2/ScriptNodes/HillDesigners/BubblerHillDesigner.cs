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

sealed class BubblerHillDesigner : RegionBasedHillDesigner
{
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
		sampler = sampler.Translate(context.ImageCoordTranslation);
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
