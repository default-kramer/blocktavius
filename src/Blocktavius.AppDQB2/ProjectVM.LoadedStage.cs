using Antipasta;
using Blocktavius.AppDQB2.Services;
using Blocktavius.Core;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2;

partial class ProjectVM
{
	partial class MyProperty
	{
		public sealed class LoadedStage : AsyncDerivedProp<LoadedStage, LoadedStage.Input, LoadStageResult>,
			I.Project.LoadedStage,
			IAsyncComputation<LoadedStage.Input, LoadStageResult>
		{
			public sealed record Input
			{
				public required string? FullPath { get; init; }
				public required IStageLoader StageLoader { get; init; } // should always be reference-equal
			}

			private readonly I.Project.SelectedSourceStage sourceStage;
			private readonly IStageLoader stageLoader;

			public LoadedStage(I.Project.SelectedSourceStage sourceStage, IStageLoader stageLoader)
			{
				this.sourceStage = ListenTo(sourceStage);
				this.stageLoader = stageLoader;
			}

			protected override Input BuildInput() => new()
			{
				FullPath = sourceStage.Value?.StgdatFile?.FullName,
				StageLoader = stageLoader,
			};

			public static async Task Compute(IAsyncContext<LoadStageResult> context, Input input)
			{
				var threadA = Thread.CurrentThread.ManagedThreadId;

				context.UpdateValue(null);
				if (string.IsNullOrWhiteSpace(input.FullPath))
				{
					return;
				}

				await context.UnblockAsync();

				Task<Minimap?> getMinimap;
				var cmndatPath = Path.Combine(new FileInfo(input.FullPath).Directory?.FullName ?? "<<FAIL>>", "CMNDAT.BIN");
				var cmndatFile = new FileInfo(cmndatPath);
				if (cmndatFile.Exists)
				{
					getMinimap = Task.Run(() => Minimap.FromCmndatFile(cmndatFile).SafeCast<Minimap?>());
				}
				else
				{
					Minimap? map = null;
					getMinimap = Task.FromResult(map);
				}

				var result = await input.StageLoader.LoadStage(new FileInfo(input.FullPath));
				result = result with
				{
					Minimap = await getMinimap,
				};

				var threadB = Thread.CurrentThread.ManagedThreadId;
				context.UpdateValue(result);
			}
		}
	}
}
