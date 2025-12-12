using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

public interface IAsyncContext<TOutput> where TOutput : class
{
	/// <summary>
	/// Typically the first `await` of your async code should be calling this method.
	/// This hints that the UI thread should be allowed to do other work while the
	/// remainder of this async method completes.
	/// (It is a "hint" because the scheduling model might unblock
	///  the UI thread immediately and unconditionally.
	///  This would typically be a project-level decision.)
	/// </summary>
	ContextUnblocker UnblockAsync();

	/// <summary>
	/// Updates the value of the <see cref="IElement{TOutput}"/>.
	/// Safe to call from any thread; the scheduler will dispatch to the UI thread if needed.
	/// </summary>
	void UpdateValue(TOutput? output);

	CancellationToken CancellationToken { get; }
}
