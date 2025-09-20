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

	public override StageMutation? BuildMutation(StageRebuildContext context)
	{
		if (area == null || Block == null)
		{
			return null;
		}

		var tagger = area.BuildTagger();
		var sampler = tagger.BuildHills(true, context.PRNG, this.elevation)
			.Translate(context.ImageCoordTranslation);

		if (Block.UniformBlockId.HasValue)
		{
			return StageMutation.CreateHills(sampler, Block.UniformBlockId.Value);
		}

		return null; // TODO support mottlers....
	}
}
