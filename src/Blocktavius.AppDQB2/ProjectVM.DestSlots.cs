using Antipasta;
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
		public sealed class DestSlots : DerivedProp<DestSlots, IReadOnlyList<WritableSlotVM>>, I.Project.DestSlots
		{
			private readonly I.Project.Profile profile;

			public DestSlots(I.Project.Profile profile)
			{
				this.profile = ListenTo(profile);
			}

			protected override IReadOnlyList<WritableSlotVM> Recompute()
			{
				return profile.Value.WritableSaveSlots.Select(WritableSlotVM.Create).ToList();
			}
		}
	}
}
