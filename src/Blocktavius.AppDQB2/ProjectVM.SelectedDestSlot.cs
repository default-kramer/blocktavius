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
		public sealed class SelectedDestSlot : SettableDerivedProp<SelectedDestSlot, WritableSlotVM?>, I.Project.SelectedDestSlot
		{
			private readonly I.Project.Profile profile;
			private readonly I.Project.DestSlots destSlots;
			private string profileHash;

			public SelectedDestSlot(I.Project.Profile profile, I.Project.DestSlots destSlots)
			{
				this.profile = ListenTo(profile);
				this.destSlots = ListenTo(destSlots);
				this.profileHash = profile.Value.VerificationHash;
			}

			protected override bool AcceptsNull(out WritableSlotVM? nullValue)
			{
				nullValue = null;
				return true;
			}

			protected override bool AcceptSetValueRequest(IPropagationContext context, ref WritableSlotVM? value) => true;

			protected override WritableSlotVM? Recompute()
			{
				if (profileHash != profile.Value.VerificationHash)
				{
					// Profile has changed, user must confirm the slot they want to use
					profileHash = profile.Value.VerificationHash;
					return null;
				}
				return destSlots.Value.FirstOrDefault(x => x.Name == CachedValue?.Name);
			}
		}
	}
}
