using Antipasta;
using System.Collections.Generic;
using System.Linq;

namespace Blocktavius.AppDQB2;

partial class ProjectVM
{
	static partial class MyProperty
	{
		public sealed class SourceSlots : DerivedProp<SourceSlots, IReadOnlyList<SlotVM>>, I.Project.SourceSlots
		{
			private readonly I.Project.Profile profile;

			public SourceSlots(I.Project.Profile profile)
			{
				this.profile = ListenTo(profile);
			}

			protected override IReadOnlyList<SlotVM> Recompute()
			{
				return profile.Value.SaveSlots.Select(SlotVM.Create).ToList();
			}
		}
	}
}
