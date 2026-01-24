using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Core;

readonly struct ExpansionId
{
	public required int Value { get; init; }

	public ExpansionId Next() => new ExpansionId { Value = this.Value + 1 };

	/// <summary>
	/// By convention, this should identify the initial state (before any expansion).
	/// </summary>
	public static readonly ExpansionId Zero = new ExpansionId { Value = 0 };

	public static readonly ExpansionId MaxValue = new ExpansionId { Value = int.MaxValue };
}
