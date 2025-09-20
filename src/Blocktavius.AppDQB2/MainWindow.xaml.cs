using Blocktavius.Core;
using Blocktavius.DQB2;
using Blocktavius.DQB2.EyeOfRubiss;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Blocktavius.AppDQB2
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private ProjectVM vm = new();

		public MainWindow()
		{
			InitializeComponent();

			vm.StgdatFilePath = @"C:\Users\kramer\Documents\My Games\DRAGON QUEST BUILDERS II\Steam\76561198073553084\SD\STGDAT01.BIN";

			vm.Layers.Add(LayerVM.BuildChunkMask());
			vm.Layers.Add(new LayerVM());
			vm.SelectedLayer = vm.Layers.First();

			vm.Scripts.Add(new ScriptVM() { Name = "Main", IsMain = true });
			vm.SelectedScript = vm.Scripts.First();

			DataContext = vm;
			Global.SetCurrentProject(vm);
		}

		protected override void OnClosed(EventArgs e)
		{
			App.ShutdownEyeOfRubiss();
			base.OnClosed(e);
		}

		private void PreviewButtonClicked(object sender, RoutedEventArgs e)
		{
			if (vm.TryRebuildStage(out var scriptedStage))
			{
				App.eyeOfRubissDriver.WriteStageAsync(scriptedStage).GetAwaiter().GetResult();
			}
		}
	}
}