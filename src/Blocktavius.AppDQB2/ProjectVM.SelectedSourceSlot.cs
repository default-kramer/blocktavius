using Antipasta;
using System.Linq;

namespace Blocktavius.AppDQB2;

partial class ProjectVM
{
	static partial class MyProperty
	{
		public sealed class SelectedSourceSlot : SettableDerivedProp<SelectedSourceSlot, SlotVM?>, I.Project.SelectedSourceSlot, IImmediateNotifyNode
		{
			string IImmediateNotifyNode.PropertyName => nameof(ProjectVM.SelectedSourceSlot);

			private readonly I.Project.Profile profile;
			private readonly I.Project.SourceSlots sourceSlots;
			private string profileHash;

			public SelectedSourceSlot(I.Project.Profile profile, I.Project.SourceSlots sourceSlots)
			{
				this.profile = ListenTo(profile);
				this.sourceSlots = ListenTo(sourceSlots);
				profileHash = profile.Value.VerificationHash;
			}

			protected override bool AcceptSetValueRequest(ref SlotVM? newValue) => true;

			protected override SlotVM? Recompute()
			{
				if (profileHash != profile.Value.VerificationHash)
				{
					// Profile has changed, user must confirm the slot they want to use
					profileHash = profile.Value.VerificationHash;
					return null;
				}
				return sourceSlots.Value.FirstOrDefault(x => x.Name == CachedValue?.Name);
			}
		}
	}
}
