using Antipasta;
using System;
using System.Collections.Generic;

namespace Blocktavius.AppDQB2;

partial class ProjectVM
{
	static partial class MyProperty
	{
		public sealed class SourceStages : DerivedProp<SourceStages, IReadOnlyList<SlotStageVM>>, I.Project.SourceStages, IImmediateNotifyNode
		{
			string IImmediateNotifyNode.PropertyName => nameof(ProjectVM.SourceStages);

			private readonly I.Project.SelectedSourceSlot selectedSourceSlot;

			public SourceStages(I.Project.SelectedSourceSlot selectedSourceSlot)
			{
				this.selectedSourceSlot = ListenTo(selectedSourceSlot);
			}

			protected override IReadOnlyList<SlotStageVM> Recompute()
			{
				return selectedSourceSlot.Value?.Stages ?? Array.Empty<SlotStageVM>();
			}
		}
	}
}
