using Blocktavius.AppDQB2.EyeOfRubissDriver;
using Blocktavius.Core;
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
		private MainVM vm = new();

		public MainWindow()
		{
			InitializeComponent();

			vm.Layers.Add(LayerVM.BuildChunkMask());
			vm.Layers.Add(new LayerVM());
			vm.SelectedLayer = vm.Layers.First();
			DataContext = vm;
		}

		private void PreviewButtonClicked(object sender, RoutedEventArgs e)
		{
			if (vm.Layers.Count < 2)
			{
				return;
			}

			var prng = PRNG.Create(new Random());

			var layer = vm.Layers[1];
			var tagger = SetupTagger(layer.TileGridPainterVM);
			var sampler = tagger.BuildHills(true, prng);
			var world = RebuildWorld(sampler);

			WriteChunkFiles(world.Chunks);
			WriteDriverFile(world.Chunks);
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

		private static World RebuildWorld(I2DSampler<int> sampler)
		{
			const ushort grass = 4;
			var world = new World();

			foreach (var xz in sampler.Bounds.Enumerate())
			{
				if (xz.X < 0 || xz.Z < 0)
				{
					continue;
				}

				var chunk = world.GetOrCreateChunk(xz);

				int elevation = sampler.Sample(xz);
				for (int y = 0; y < elevation; y++)
				{
					chunk.Set(xz, y, grass);
				}
			}

			return world;
		}

		private static void WriteChunkFiles(IEnumerable<Chunk> chunks)
		{
			foreach (var chunk in chunks)
			{
				var fullPath = System.IO.Path.Combine(App.driverDir.FullName, chunk.Filename);
				using var stream = File.OpenWrite(fullPath);
				chunk.WriteBytes(stream);
				stream.Flush();
				stream.Close();
			}
		}

		private static void WriteDriverFile(IEnumerable<Chunk> chunks)
		{
			var chunkInfos = chunks.Select(c => new DriverFileModel.ChunkInfo()
			{
				OffsetX = c.Offset32.X * 32,
				OffsetZ = c.Offset32.Z * 32,
				RelativePath = c.Filename,
			});

			var content = new DriverFileModel()
			{
				UniqueValue = Guid.NewGuid().ToString(),
				ChunkInfos = chunkInfos.ToList(),
			};

			content.WriteToFile(App.driverFile);
		}

		class World
		{
			private Dictionary<XZ, Chunk> chunks = new();

			private static XZ GetChunkKey(XZ coord) => new XZ(coord.X / 32, coord.Z / 32);

			public Chunk GetOrCreateChunk(XZ xz)
			{
				var key = GetChunkKey(xz);
				if (!chunks.TryGetValue(key, out var chunk))
				{
					chunk = new Chunk() { Offset32 = key };
					chunks[key] = chunk;
				}
				return chunk;
			}

			public IEnumerable<Chunk> Chunks => chunks.Values;
		}

		class Chunk
		{
			private readonly ushort[] blockdata;
			const int size = 32 * 32 * 96;

			public required XZ Offset32 { get; init; }

			public string Filename => $"chunk.{Offset32.X}.{Offset32.Z}.bin";

			public Chunk()
			{
				blockdata = new ushort[size];
				blockdata.AsSpan().Fill(0);
			}

			private static int GetIndex(XZ xz, int y)
			{
				// XZ could be global so we assume we are the correct chunk and mod 32 here.
				int x = xz.X % 32;
				int z = xz.Z % 32;
				int index = 0
					+ y * 32 * 32
					+ z * 32
					+ x;
				return index;
			}

			public ushort Get(XZ xz, int y) => blockdata[GetIndex(xz, y)];

			public void Set(XZ xz, int y, ushort value)
			{
				blockdata[GetIndex(xz, y)] = value;
			}

			public void WriteBytes(Stream stream)
			{
				if (BitConverter.IsLittleEndian)
				{
					var bytes = MemoryMarshal.AsBytes<ushort>(blockdata);
					stream.Write(bytes);
				}
				else
				{
					var bytes = new byte[blockdata.Length * 2];
					for (int i = 0; i < blockdata.Length; i++)
					{
						ushort val = blockdata[i];
						byte lo = (byte)(val & 0xFF);
						byte hi = (byte)(val >> 8);
						bytes[i] = lo;
						bytes[i + 1] = hi;
					}
					stream.Write(bytes);
				}
			}
		}
	}
}