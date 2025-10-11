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

sealed class QuaintHillNodeVM : ScriptNodeVM
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

	private int steepness = 1;
	[Category(Common)]
	public int Steepness
	{
		get => steepness;
		set => ChangeProperty(ref steepness, Math.Max(1, value));
	}

	private IAreaVM? area;
	[ItemsSource(typeof(Global.LayersItemsSource))]
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

	private int mode;
	[RefreshProperties(RefreshProperties.All)]
	[Category(Common)]
	public int Mode
	{
		get => mode;
		set
		{
			ChangeProperty(ref mode, value);
		}
	}

	private bool lockRandomSeed;
	[Category(Common)]
	public bool LockRandomSeed
	{
		get => lockRandomSeed;
		set => ChangeProperty(ref lockRandomSeed, value);
	}

	private string? prngSeed = null;

	private int cornerDebug;
	[Category(Common)]
	public int CornerDebug
	{
		get => cornerDebug;
		set => ChangeProperty(ref cornerDebug, value);
	}

	private int bubbleFactor = 3;
	[Category(Common)]
	public int BubbleFactor
	{
		get => bubbleFactor;
		set => ChangeProperty(ref bubbleFactor, value);
	}

	private int bubbleScale = 6;
	[Category(Common)]
	public int BubbleScale
	{
		get => bubbleScale;
		set => ChangeProperty(ref bubbleScale, value);
	}

	private int minBubbleChance = 10;
	[Category(Common)]
	public int MinBubbleChance
	{
		get => minBubbleChance;
		set => ChangeProperty(ref minBubbleChance, value);
	}

	private int smoothness = 3;
	[Category(Common)]
	public int Smoothness
	{
		get => smoothness;
		set => ChangeProperty(ref smoothness, value);
	}

	public override StageMutation? BuildMutation(StageRebuildContext context)
	{
		if (area == null || Block == null)
		{
			return null;
		}

		var tagger = area.BuildTagger();
		var regions = tagger.GetRegions(true);
		if (regions.Count == 0)
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

		if (hillDesigner != null && Block.UniformBlockId.HasValue)
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