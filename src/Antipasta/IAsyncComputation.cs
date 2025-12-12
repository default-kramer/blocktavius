using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

/// <summary>
/// The static interface for async elements.
/// </summary>
public interface IAsyncComputation<TInput, TOutput>
	where TInput : IEquatable<TInput>
	where TOutput : class
{
	static abstract Task Compute(IAsyncContext<TOutput> context, TInput input);
}
