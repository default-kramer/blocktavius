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
		public sealed class SourceFullPath : DerivedProp<SourceFullPath, string?>, I.Project.SourceFullPath
		{
			private readonly I.Project.SelectedSourceStage selectedSourceStage;
			public SourceFullPath(I.Project.SelectedSourceStage selectedSourceStage)
			{
				this.selectedSourceStage = ListenTo(selectedSourceStage);
			}

			protected override string? Recompute()
			{
				return selectedSourceStage.Value?.StgdatFile?.FullName;
			}
		}
	}
}
