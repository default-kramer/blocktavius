using Blocktavius.AppDQB2.ScriptNodes.HillDesigners;
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

sealed class PutHillNodeVM : ScriptNodeVM, IStageMutator
{
	const string Common = "_Common";

	private HillType? selectedHillType;
	[Category(Common)]
	[ItemsSource(typeof(HillType.PropGridItemsSource))]
	[RefreshProperties(RefreshProperties.All)]
	public HillType? SelectedHillType
	{
		get => selectedHillType;
		set
		{
			ChangeProperty(ref selectedHillType, value);
			HillDesigner = value?.CreateNewDesigner();
		}
	}

	private IHillDesigner? hillDesigner;
	[Category(Common)]
	[FlattenProperties(CategoryName = "ZZZ")]
	public IHillDesigner? HillDesigner
	{
		get => hillDesigner;
		private set => ChangeProperty(ref hillDesigner, value);
	}

	private int elevation;
	[Category(Common)]
	public int Elevation
	{
		get => elevation;
		set => ChangeProperty(ref elevation, value);
	}

	private IAreaVM? area;
	[ItemsSource(typeof(Global.AreasItemsSource))]
	[Category(Common)]
	public IAreaVM? Area
	{
		get => area;
		set => ChangeProperty(ref area, value);
	}

	private IBlockProviderVM? blockProvider = Blockdata.AnArbitraryBlockVM;
	[Editor(typeof(PropGridEditors.BlockProviderEditor), typeof(PropGridEditors.BlockProviderEditor))]
	[Category(Common)]
	public IBlockProviderVM? Block
	{
		get => blockProvider;
		set => ChangeProperty(ref blockProvider, value);
	}

	private string? prngSeed = null;
	private bool lockRandomSeed;
	[Category(Common)]
	public bool LockRandomSeed
	{
		get => lockRandomSeed;
		set => ChangeProperty(ref lockRandomSeed, value);
	}

	public StageMutation? BuildMutation(StageRebuildContext context)
	{
		if (area == null || Block == null || hillDesigner == null)
		{
			return null;
		}

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

		if (Block.UniformBlockId.HasValue)
		{
			var hillContext = new HillDesignContext()
			{
				AreaVM = area,
				FillBlockId = Block.UniformBlockId.Value,
				ImageCoordTranslation = context.ImageCoordTranslation,
				Prng = prng,
				Elevation = elevation,
			};
			return hillDesigner.CreateMutation(hillContext);
		}

		return null; // TODO support mottlers....
	}
}