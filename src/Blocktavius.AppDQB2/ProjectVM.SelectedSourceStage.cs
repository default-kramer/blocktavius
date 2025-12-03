using Antipasta;
using System.Linq;

namespace Blocktavius.AppDQB2;

partial class ProjectVM
{
	static partial class MyProperty
	{
		public sealed class SelectedSourceStage : SettableDerivedProp<SelectedSourceStage, SlotStageVM?>, I.Project.SelectedSourceStage, IImmediateNotifyNode
		{
			string IImmediateNotifyNode.PropertyName => nameof(ProjectVM.SelectedSourceStage);

			private readonly I.Project.SourceStages sourceStages;

			public SelectedSourceStage(I.Project.SourceStages sourceStages)
			{
				this.sourceStages = ListenTo(sourceStages);
			}

			private SlotStageVM? FindMatch(SlotStageVM? slot)
			{
				// Our first choice is finding the stage we already have.
				// But probably this is firing because the sourceSlot has changed, in which
				// case we want to find the same-named stage from the new slot.
				var stages = sourceStages.Value;
				var result = stages?.FirstOrDefault(x => x == slot)
					?? stages?.FirstOrDefault(x => x.Name == slot?.Name);
				return result;
			}

			// TODO should I attempt to change the signature of AcceptSetValueRequest
			// so that Recompute() can use it instead of requiring FindMatch here?
			protected override SlotStageVM? Recompute() => FindMatch(CachedValue);

			protected override bool AcceptSetValueRequest(ref SlotStageVM? newValue)
			{
				if (AntipastaThreadLocal.IsPropagating) { return false; }
				newValue = FindMatch(newValue);
				return true;
			}
		}
	}
}
