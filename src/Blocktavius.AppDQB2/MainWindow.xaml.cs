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

			//vm.ProjectFilePath = "foo.blocktaviusproject";
			vm.StgdatFilePath = "STGDAT01.bin";

			vm.Layers.Add(LayerVM.BuildChunkMask());
			vm.Layers.Add(new LayerVM());
			vm.SelectedLayer = vm.Layers.First();

			vm.Scripts.Add(new ScriptVM() { Name = "Main" });
			vm.SelectedScript = vm.Scripts.First();

			DataContext = vm;
			Global.SetCurrentProject(vm);
		}

		protected override void OnClosed(EventArgs e)
		{
			App.ShutdownEyeOfRubiss();
			base.OnClosed(e);
		}

		private Blocktavius.DQB2.ICloneableStage? stage = null;

		private void PreviewButtonClicked(object sender, RoutedEventArgs e)
		{
			if (vm.Layers.Count < 2)
			{
				return;
			}

			stage = stage ?? DQB2.ImmutableStage.LoadStgdat(@"C:\Users\kramer\Documents\My Games\DRAGON QUEST BUILDERS II\Steam\76561198073553084\SD\STGDAT01.BIN");

			if (stage is null)
			{
				return;
			}

			// Anytime we convert a painted layer into XZ coordinates, we need to add this offset:
			var offsetX = stage.ChunksInUse.Select(o => o.NorthwestCorner.X).Min();
			var offsetZ = stage.ChunksInUse.Select(o => o.NorthwestCorner.Z).Min();

			var clone = stage.Clone();

			var prng = PRNG.Create(new Random());

			var layer = vm.Layers[1];
			var tagger = SetupTagger(layer.TileGridPainterVM);
			var sampler = tagger.BuildHills(true, prng)
				.Translate(new XZ(offsetX, offsetZ))
				.AdjustElevation(50);
			var hills = StageMutation.CreateHills(sampler, block: 4); // grassy earth
			clone.Mutate(hills);

			App.eyeOfRubissDriver.WriteStageAsync(clone).GetAwaiter().GetResult();
		}

		private static TileTagger<bool> SetupTagger(ITileGridPainterVM gridData)
		{
			var unscaledSize = new XZ(gridData.ColumnCount, gridData.RowCount);
			var scale = new XZ(gridData.TileSize, gridData.TileSize);
			var tagger = new TileTagger<bool>(unscaledSize, scale);
			foreach (var xz in new Core.Rect(XZ.Zero, unscaledSize).Enumerate())
			{
				tagger.AddTag(xz, gridData.GetStatus(xz));
			}
			return tagger;
		}
	}
}