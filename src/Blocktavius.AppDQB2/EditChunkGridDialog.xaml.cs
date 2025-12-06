using Blocktavius.AppDQB2.Services;
using Blocktavius.Core;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Linq;
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
public partial class EditChunkGridDialog : Window, IDialog<EditChunkGridDialog.Result>
{
	[Obsolete("for designer only")]
	public EditChunkGridDialog()
	{
		InitializeComponent();
	}

	private EditChunkGridDialog(object? privateOverload)
	{
		InitializeComponent();
	}

	public sealed class Result
	{
		public required IReadOnlySet<ChunkOffset> ExpandedChunks { get; init; }
	}

	public (bool?, Result) ShowDialog(IWindowManager windowManager)
	{
		var vm = DataContext as VM ?? throw new Exception($"Invalid viewmodel type: {DataContext}");
		var windowResult = windowManager.ShowDialog(this);
		return (windowResult, vm.BuildResult());
	}

	public bool? ShowDialog(out Result result)
	{
		var vm = DataContext as VM ?? throw new Exception($"Invalid viewmodel type: {DataContext}");
		var dialogResult = this.ShowDialog();

		if (dialogResult.GetValueOrDefault() == true)
		{
			result = vm.BuildResult();
			return true;
		}
		else
		{
			result = null!;
			return dialogResult;
		}
	}

	public static IDialog<Result> Create(IStage stage, IEnumerable<ChunkOffset> chunkExpansion)
	{
		var vm = new VM();
		foreach (var chunk in stage.ChunksInUse.Concat(chunkExpansion))
		{
			vm[chunk].Status = ChunkStatus.Expanded;
		}
		foreach (var chunk in stage.OriginalChunksInUse)
		{
			vm[chunk].Status = ChunkStatus.Original;
		}
		vm.OnCellCount = vm.AllCells.Where(c => c.Status != ChunkStatus.Off).Count();

		var dialog = new EditChunkGridDialog(null);
		dialog.DataContext = vm;
		return dialog;
	}

	const int i64 = 64; // chunk grid is 64x64

	sealed class VM : ViewModelBase
	{
		public IReadOnlyList<RowVM> Rows { get; }

		public VM()
		{
			Rows = Enumerable.Range(0, i64).Select(z => new RowVM(this, z)).ToList();
		}

		public CellVM this[ChunkOffset offset]
		{
			get => Rows[offset.OffsetZ].Cells[offset.OffsetX];
		}

		public IEnumerable<CellVM> AllCells => Rows.SelectMany(row => row.Cells);

		private int _onCellCount;
		public int OnCellCount
		{
			get => _onCellCount;
			internal set => ChangeProperty(ref _onCellCount, value);
		}

		public Result BuildResult()
		{
			var expansion = AllCells
				.Where(cell => cell.Status == ChunkStatus.Expanded)
				.Select(cell => cell.ChunkOffset)
				.ToHashSet();
			return new Result { ExpandedChunks = expansion };
		}
	}

	sealed class RowVM : ViewModelBase
	{
		public int RowIndex { get; }
		public IReadOnlyList<CellVM> Cells { get; }

		public RowVM(VM vm, int rowIndex)
		{
			this.RowIndex = rowIndex;
			this.Cells = Enumerable.Range(0, i64).Select(x => new CellVM(vm, new ChunkOffset(x, rowIndex))).ToList();
		}
	}

	sealed class CellVM : ViewModelBase
	{
		public VM VM { get; }
		public ChunkOffset ChunkOffset { get; }

		public CellVM(VM vm, ChunkOffset offset)
		{
			this.VM = vm;
			this.ChunkOffset = offset;
		}

		private ChunkStatus _status;
		public ChunkStatus Status
		{
			get => _status;
			set
			{
				if (_status != value)
				{
					ChangeProperty(ref _status, value, nameof(Status), nameof(Color));
					int delta = (value == ChunkStatus.Off) ? -1 : 1;
					VM.OnCellCount += delta;
				}
			}
		}

		public Brush Color => Status switch
		{
			ChunkStatus.Expanded => Brushes.HotPink,
			ChunkStatus.Original => Brushes.Blue,
			_ => Brushes.LightGray,
		};

		public void Expand()
		{
			if (Status == ChunkStatus.Off)
			{
				Status = ChunkStatus.Expanded;
			}
		}

		public void Unexpand()
		{
			if (Status == ChunkStatus.Expanded)
			{
				Status = ChunkStatus.Off;
			}
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
