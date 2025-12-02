using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

public static class AntipastaThreadLocal
{
	/// <summary>
	/// Useful for detecting if you are in the situation where an ItemsSource has changed and WPF
	/// is trying to set your SelectedItem to null
	/// </summary>
	public static bool IsPropagating => isPropagating.Value;
	private static readonly ThreadLocal<bool> isPropagating = new ThreadLocal<bool>();

	internal readonly ref struct PropagationScope : IDisposable
	{
		private readonly bool _originalValue;
		internal PropagationScope(bool originalValue)
		{
			_originalValue = originalValue;
		}

		public void Dispose()
		{
			isPropagating.Value = _originalValue;
		}
	}

	internal static PropagationScope UsePropagationScope()
	{
		var originalValue = isPropagating.Value;
		isPropagating.Value = true;
		return new PropagationScope(originalValue);
	}
}
