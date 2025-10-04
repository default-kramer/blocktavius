using Blocktavius.Core;
using Blocktavius.Core.Generators.Hills;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace Blocktavius.AppDQB2.ScriptNodes;

sealed class QuaintHillNodeVM : ScriptNodeVM
{
	private int elevation;
	public int Elevation
	{
		get => elevation;
		set => ChangeProperty(ref elevation, value);
	}

	private int steepness = 1;
	public int Steepness
	{
		get => steepness;
		set => ChangeProperty(ref steepness, Math.Max(1, value));
	}

	private IAreaVM? area;
	[ItemsSource(typeof(Global.LayersItemsSource))]
	public IAreaVM? Area
	{
		get => area;
		set => ChangeProperty(ref area, value);
	}

	private IBlockProviderVM? blockProvider = Blockdata.AnArbitraryBlockVM;
	[Editor(typeof(PropGridEditors.BlockProviderEditor), typeof(PropGridEditors.BlockProviderEditor))]
	public IBlockProviderVM? Block
	{
		get => blockProvider;
		set => ChangeProperty(ref blockProvider, value);
	}

	private int mode;
	public int Mode
	{
		get => mode;
		set => ChangeProperty(ref mode, value);
	}

	private bool lockRandomSeed;
	public bool LockRandomSeed
	{
		get => lockRandomSeed;
		set => ChangeProperty(ref lockRandomSeed, value);
	}

	private string? prngSeed = null;

	private int cornerDebug;
	public int CornerDebug
	{
		get => cornerDebug;
		set => ChangeProperty(ref cornerDebug, value);
	}

	public override StageMutation? BuildMutation(StageRebuildContext context)
	{
		if (area == null || Block == null)
		{
			return null;
		}

		var tagger = area.BuildTagger();
		var regions = tagger.GetRegions(true);

		PRNG prng;
		if (lockRandomSeed && prngSeed != null)
		{
			prng = PRNG.Deserialize(prngSeed);
		}
		else
		{
			prng = PRNG.Create(new Random());
			prngSeed = prng.Serialize();
		}

		I2DSampler<Elevation> sampler;
		if (mode == 0)
		{
			sampler = Core.Generators.CornerShifterHill.BuildNewHill(regions.Single().Bounds, prng, new Elevation(elevation - 10), new Elevation(elevation));

		}
		else if (mode == 1)
		{
			var settings = new WinsomeHill.Settings
			{
				Prng = prng,
				MaxElevation = Elevation,
				MinElevation = Elevation - 30,
				Steepness = Steepness,
				CornerDebug = CornerDebug,
			};
			sampler = WinsomeHill.BuildWinsomeHills(regions.Single(), settings);
		}
		else if (mode == 2)
		{
			var settings = new PlainHill.Settings
			{
				MaxElevation = this.elevation,
				MinElevation = this.elevation - 10,
				Steepness = this.steepness,
			};
			if (!settings.Validate(out settings))
			{
				this.Elevation = settings.MaxElevation;
				this.Steepness = settings.Steepness;
			}
			sampler = PlainHill.BuildPlainHill(regions.Single(), settings);
		}
		else if (mode == 3)
		{
			var settings = new AdamantHill.Settings
			{
				Prng = prng,
				CornerDebug = CornerDebug,
				MaxElevation = Elevation,
				CliffConfig = AdamantCliffBuilder.Config.Default,
				// Perhaps steepness should control cliffConfig.MinSeparation?
			};
			sampler = AdamantHill.BuildAdamantHills(regions.Single(), settings);
		}
		else
		{
			sampler = QuaintHill.BuildQuaintHills(regions, prng, elevation);
		}

		// TODO can I avoid this pitfall?
		sampler = sampler.Translate(context.ImageCoordTranslation);

		if (Block.UniformBlockId.HasValue)
		{
			return StageMutation.CreateHills(sampler, Block.UniformBlockId.Value);
		}

		return null; // TODO support mottlers....
	}
}
