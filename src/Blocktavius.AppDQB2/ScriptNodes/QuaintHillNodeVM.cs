using Blocktavius.Core;
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
			sampler = TODO.BuildHills(regions, prng, elevation);
		}
		else
		{
			sampler = Core.Generators.CornerShifterHill.BuildNewHill(regions.Single().Bounds, prng, new Elevation(elevation - 10), new Elevation(elevation));
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
