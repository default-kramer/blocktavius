using System;
using System.IO;
using System.Text;
using System.Windows;

namespace Blocktavius.AppDQB2
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private ProjectVM vm = new();
		private ExternalImageManager imageManager;

		public MainWindow()
		{
			InitializeComponent();

			vm.StgdatFilePath = @"C:\Users\kramer\Documents\My Games\DRAGON QUEST BUILDERS II\Steam\76561198073553084\SD\STGDAT01.BIN";

			imageManager = new ExternalImageManager(new DirectoryInfo(@"C:\Users\kramer\Documents\code\HermitsHeresy\examples\STB\"));
			imageManager.ExternalImages.CollectionChanged += (a, b) => { Resync(vm, imageManager); };

			vm.Layers.Add(LayerVM.BuildChunkMask());
			vm.Layers.Add(new LayerVM());
			vm.SelectedLayer = vm.Layers.First();

			vm.Scripts.Add(new ScriptVM() { Name = "Main", IsMain = true });
			vm.SelectedScript = vm.Scripts.First();

			DataContext = vm;
			Global.SetCurrentProject(vm);
		}

		// TODO!!! Should not recreate layers!
		// Should allow user to choose an external image to create a new layer.
		private static void Resync(ProjectVM vm, ExternalImageManager imageManager)
		{
			var old = vm.Layers.Where(l => l is ExternalImageLayerVM).ToList();
			foreach (var layer in old)
			{
				vm.Layers.Remove(layer);
			}
			foreach (var image in imageManager.ExternalImages)
			{
				vm.Layers.Add(new ExternalImageLayerVM { Image = image });
			}
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