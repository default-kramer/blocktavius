using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2;

/// <summary>
/// When we save a STGDAT file, what else should be copied from the source slot?
/// </summary>
enum InclusionMode
{
	/// <summary>
	/// Copies AUTOCMNDAT, AUTOSTGDAT, CMNDAT and all known STGDAT files from the source slot to the dest slot.
	/// (e.g. A file like "STGDAT01 - Copy.BIN" would not be recognized as a "known" stgdat file.)
	/// </summary>
	Automatic,
	JustCmndat,
	Nothing,
};

sealed class InclusionModeVM
{
	public required InclusionMode InclusionMode { get; init; }

	public required string DisplayName { get; init; }

	internal static IEnumerable<InclusionModeVM> BuildChoices()
	{
		yield return new InclusionModeVM
		{
			InclusionMode = InclusionMode.Automatic,
			DisplayName = "Automatic (Safest)",
		};
		yield return new InclusionModeVM
		{
			InclusionMode = InclusionMode.JustCmndat,
			DisplayName = "CMNDAT only (Advanced)",
		};
		yield return new InclusionModeVM
		{
			InclusionMode = InclusionMode.Nothing,
			DisplayName = "Nothing (Advanced)",
		};
	}
}
