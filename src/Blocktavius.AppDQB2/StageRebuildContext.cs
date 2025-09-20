using Blocktavius.Core;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2;

sealed class StageRebuildContext
{
	private readonly List<string> errors = new();
	private readonly IMutableStage stage;
	public XZ ImageCoordTranslation { get; }
	public PRNG PRNG { get; init; } = PRNG.Create(new Random());

	public StageRebuildContext(IMutableStage stage)
	{
		this.stage = stage;
		var minX = stage.ChunksInUse.Select(o => o.NorthwestCorner.X).Min();
		var minZ = stage.ChunksInUse.Select(o => o.NorthwestCorner.Z).Min();
		ImageCoordTranslation = new XZ(minX, minZ);
	}

	public void AddError(string error)
	{
		this.errors.Add(error);
	}
}
