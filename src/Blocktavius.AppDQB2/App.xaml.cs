using Blocktavius.DQB2.EyeOfRubiss;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Blocktavius.AppDQB2
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public static readonly Driver eyeOfRubissDriver;

		static App()
		{
			eyeOfRubissDriver = Driver.CreateAndStart(new Driver.Config()
			{
				EyeOfRubissExePath = @"C:\Users\kramer\Documents\code\DQB2_WorldViewer\.EXPORT\EyeOfRubiss.exe",
				UseCmdShell = true,
			});
		}

		protected override void OnExit(ExitEventArgs e)
		{
			ShutdownEyeOfRubiss();
			base.OnExit(e);
		}

		public static void ShutdownEyeOfRubiss()
		{
			try { eyeOfRubissDriver?.Dispose(); } catch { }
		}
	}
}
