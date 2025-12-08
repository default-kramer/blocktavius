using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

/// <summary>
/// Used to cache information specific to a single propagation and test whether that cached
/// information is relevant to the current propagation or leftover from a prior one.
/// </summary>
readonly struct PropagationId
{
	// In order to overflow a long, you would need to create about 29M instances per second for 10,000 years,
	// so we're just going to assume it will never overflow.
	private static long sharedCounter = 0;
	public readonly long Counter;

	private PropagationId(long counter) { this.Counter = counter; }

	public static PropagationId None => new PropagationId(0);

	public static PropagationId Create()
	{
		return new PropagationId(Interlocked.Increment(ref sharedCounter));
	}
}
