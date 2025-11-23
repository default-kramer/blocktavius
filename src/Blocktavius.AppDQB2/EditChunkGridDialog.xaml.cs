using Blocktavius.Core;
using Blocktavius.DQB2;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Blocktavius.AppDQB2;

/// <summary>
/// Interaction logic for EditChunkGridDialog.xaml
/// </summary>
public partial class EditChunkGridDialog : Window
{
	public EditChunkGridDialog()
	{
		InitializeComponent();
	}

	internal static void ShowDialog(ProjectVM project)
	{
		if (!project.TryLoadStage(out var loadResult))
		{
			return;
		}

		var stage = loadResult.Stage;

		var vm = new VM();
		foreach (var chunk in stage.ChunksInUse.Concat(project.ChunkExpansion))
		{
			vm[chunk].SetStatus(ChunkStatus.Expanded);
		}
		foreach (var chunk in stage.OriginalChunksInUse)
		{
			vm[chunk].SetStatus(ChunkStatus.Original);
		}

		var dialog = new EditChunkGridDialog();
		dialog.DataContext = vm;
		if (dialog.ShowDialog() == true)
		{
			var expansion = vm.AllCells
				.Where(cell => cell.Status == ChunkStatus.Expanded)
				.Select(cell => cell.ChunkOffset)
				.ToHashSet();
			project.ExpandChunks(expansion);
		}
	}

	const int i64 = 64; // chunk grid is 64x64

	sealed class VM : ViewModelBase
	{
		public IReadOnlyList<RowVM> Rows { get; }

		public int OnCellCount => 42;

		public VM()
		{
			Rows = Enumerable.Range(0, i64).Select(z => new RowVM(this, z)).ToList();
		}

		public CellVM this[ChunkOffset offset]
		{
			get => Rows[offset.OffsetZ].Cells[offset.OffsetX];
		}

		public IEnumerable<CellVM> AllCells => Rows.SelectMany(row => row.Cells);
	}

	sealed class RowVM : ViewModelBase
	{
		public int RowIndex { get; }
		public IReadOnlyList<CellVM> Cells { get; }

		public RowVM(VM vm, int rowIndex)
		{
			this.RowIndex = rowIndex;
			var sw = System.Diagnostics.Stopwatch.StartNew();
			this.Cells = Enumerable.Range(0, i64).Select(x => new CellVM(vm, new ChunkOffset(x, rowIndex))).ToList();
			sw.Stop();
			var asdf = sw.Elapsed.TotalSeconds;
		}
	}

	sealed class CellVM : ViewModelBase
	{
		private readonly VM vm;
		public readonly ChunkOffset ChunkOffset;

		public record struct Members
		{
			public required ChunkStatus ChunkStatus { get; init; }
			public Brush Color => ChunkStatus switch
			{
				ChunkStatus.Expanded => Brushes.HotPink,
				ChunkStatus.Original => Brushes.Blue,
				_ => Brushes.LightGray,
			};
		}

		private readonly ReactiveProperty<Members> _members;
		private Members members
		{
			get => _members.Value;
			set => _members.Value = value;
		}

		private readonly ObservableAsPropertyHelper<ChunkStatus> _status;
		public ChunkStatus Status => _status.Value;


		private readonly ObservableAsPropertyHelper<Brush> _color;
		public Brush Color => _color.Value;

		public CellVM(VM vm, ChunkOffset chunkOffset)
		{
			this.vm = vm;
			this.ChunkOffset = chunkOffset;

			_members = new ReactiveProperty<Members>(new Members { ChunkStatus = ChunkStatus.Off });
			_status = _members.Select(x => x.ChunkStatus).ToProperty(this, nameof(Status));
			_color = _members.Select(x => x.Color).ToProperty(this, nameof(Color));
		}

		public void Expand()
		{
			if (Status == ChunkStatus.Off)
			{
				SetStatus(ChunkStatus.Expanded);
			}
		}

		public void Unexpand()
		{
			if (Status == ChunkStatus.Expanded)
			{
				SetStatus(ChunkStatus.Off);
			}
		}

		internal void SetStatus(ChunkStatus status)
		{
			members = members with { ChunkStatus = status };
		}
	}

	enum ChunkStatus
	{
		Off,
		Original,
		Expanded,
	}

	private void Cell_MouseHandler(object sender, MouseEventArgs e)
	{
		var cell = (e.Source as FrameworkElement)?.DataContext as CellVM;
		if (cell == null)
		{
			throw new Exception("Assert fail, expected CellVM here");
		}

		if (e.LeftButton == MouseButtonState.Pressed)
		{
			cell.Expand();
		}
		else if (e.RightButton == MouseButtonState.Pressed)
		{
			cell.Unexpand();
		}
	}

	private void Ok_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = true;
		Close();
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = false;
		Close();
	}
}