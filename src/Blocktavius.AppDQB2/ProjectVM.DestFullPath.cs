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
		public sealed class DestFullPath : DerivedProp<DestFullPath, string?>, I.Project.DestFullPath
		{
			private readonly I.Project.SelectedSourceStage selectedSourceStage;
			private readonly I.Project.SelectedDestSlot selectedDestSlot;

			public DestFullPath(I.Project.SelectedSourceStage selectedSourceStage, I.Project.SelectedDestSlot selectedDestSlot)
			{
				this.selectedSourceStage = ListenTo(selectedSourceStage);
				this.selectedDestSlot = ListenTo(selectedDestSlot);
			}

			protected override string? Recompute()
			{
				if (selectedDestSlot.Value != null && selectedSourceStage.Value != null)
				{
					return selectedDestSlot.Value.GetFullPath(selectedSourceStage.Value.Filename);
				}
				return null;
			}
		}
	}
}
