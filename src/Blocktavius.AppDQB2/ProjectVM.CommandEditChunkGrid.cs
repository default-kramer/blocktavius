using Blocktavius.AppDQB2.Services;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2;

partial class ProjectVM
{
	static partial class MyProperty
	{
		public sealed class CommandEditChunkGrid : CommandNode, I.Project.CommandEditChunkGrid
		{
			public required ProjectVM ProjectVM { get; init; }
			public required IWindowManager WindowManager { get; init; }
			private readonly I.Project.LoadedStage loadedStage;
			private readonly I.Project.ChunkExpansion chunkExpansion;

			public CommandEditChunkGrid(I.Project.LoadedStage loadedStage, I.Project.ChunkExpansion chunkExpansion)
			{
				this.loadedStage = ListenTo(loadedStage);
				this.chunkExpansion = ListenTo(chunkExpansion);
			}

			protected override Action? CanExecute()
			{
				var stage = loadedStage.Value?.Stage;
				if (stage == null)
				{
					return null;
				}
				return () => Execute(stage);
			}

			private void Execute(IStage stage)
			{
				var window = EditChunkGridDialog.Create(stage, chunkExpansion.Value);
				var (windowResult, result) = window.ShowDialog(WindowManager);
				if (windowResult.GetValueOrDefault(false))
				{
					ProjectVM.ExpandChunks(result.ExpandedChunks);
				}
			}
		}
	}
}
