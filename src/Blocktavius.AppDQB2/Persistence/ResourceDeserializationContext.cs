using Blocktavius.AppDQB2.Resources;

namespace Blocktavius.AppDQB2.Persistence;

sealed class ResourceDeserializationContext
{
	public required IAreaManager AreaManager { get; init; }
	public required IReadOnlyList<SlotVM> Slots { get; init; }
}
