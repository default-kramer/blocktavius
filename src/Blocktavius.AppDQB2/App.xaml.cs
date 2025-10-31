﻿using Blocktavius.DQB2.EyeOfRubiss;
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
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			// Set the main window *before* showing the EditProfileWindow﻿, otherwise WPF
			// assumes that EditProfileWindow is the main window and exits when it is closed.
			var mainWindow = new MainWindow();
			this.MainWindow = mainWindow;

			var appdata = AppData.LoadOrCreate();

			var profileDialog = new EditProfileWindow();
			if (!profileDialog.ShowDialog(appdata, out var selectedProfile))
			{
				Shutdown();
				return;
			}

			Global.CurrentProfile = selectedProfile;

			Driver? eyeOfRubissDriver = null;
			if (appdata.EyeOfRubissExePath != null)
			{
				eyeOfRubissDriver = Driver.CreateAndStart(new Driver.Config()
				{
					EyeOfRubissExePath = appdata.EyeOfRubissExePath,
					UseCmdShell = true,
				});
			}

			mainWindow.EyeOfRubissDriver = eyeOfRubissDriver;
			mainWindow.Show();
		}
	}
}
