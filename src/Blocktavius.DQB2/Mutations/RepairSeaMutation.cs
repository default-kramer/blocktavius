using Blocktavius.DQB2.RepairSea;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2.Mutations;

public sealed class RepairSeaMutation : StageMutation
{
	public required ColumnCleanupMode ColumnCleanupMode { get; init; }
	public required int SeaLevel { get; init; }
	public ILiquid LiquidFamily { get; init; } = DQB2.LiquidFamily.Seawater;

	internal override void Apply(IMutableStage stage)
	{
		var p = new RepairSeaOperation.Params
		{
			ColumnCleanupMode = this.ColumnCleanupMode,
			SeaLevel = this.SeaLevel,
			LiquidFamily = LiquidFamily,
			SeaSurfaceType = LiquidAmountIndex.SurfaceLow,
			Stage = stage,
			Policy = Policy.DefaultPolicy(),
		};
		RepairSeaOperation.RepairSea(p);
	}
}
