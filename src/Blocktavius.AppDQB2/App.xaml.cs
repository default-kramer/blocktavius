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
		private static readonly Process EyeOfRubissProc;
		public static readonly FileInfo driverFile;
		public static readonly DirectoryInfo driverDir;

		static App()
		{
			var driverPath = Path.Combine(Path.GetTempPath(), "Blocktavius-EyeOfRubiss", Guid.NewGuid().ToString("D"));
			driverDir = new DirectoryInfo(driverPath);
			driverDir.Create();

			driverFile = new FileInfo(Path.Combine(driverPath, "driver.json"));
			EyeOfRubissDriver.DriverFileModel.CreateEmpty().WriteToFile(driverFile);

			var psi = new ProcessStartInfo();
			psi.UseShellExecute = false;
			psi.FileName = @"C:\Users\kramer\Documents\code\DQB2_WorldViewer\.EXPORT\EyeOfRubiss.exe";
			psi.ArgumentList.Add("--driverFile");
			psi.ArgumentList.Add(driverFile.FullName);
			// Do not turn these on; they will block this app from exiting until the child proc is closed:
			//psi.RedirectStandardError = true;
			//psi.RedirectStandardOutput = true;

			var proc = new Process();
			proc.StartInfo = psi;
			proc.Start();
			EyeOfRubissProc = proc;
		}

		protected override void OnExit(ExitEventArgs e)
		{
			try
			{
				EyeOfRubissProc?.Kill();
			}
			catch (Exception) { }
			try
			{
				EyeOfRubissProc?.Dispose();
			}
			catch (Exception) { }

			base.OnExit(e);
		}
	}
}
