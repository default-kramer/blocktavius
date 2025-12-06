using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Blocktavius.AppDQB2.Services;

public interface IDialog<TResult>
{
	(bool? windowResult, TResult result) ShowDialog(IWindowManager windowManager);
}

public interface IWindowManager
{
	bool? ShowDialog(Window dialog);
}

sealed class WindowManager : IWindowManager
{
	private WindowManager() { }
	public static readonly WindowManager Instance = new WindowManager();

	public bool? ShowDialog(Window dialog) => dialog.ShowDialog();
}
