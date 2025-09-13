using Blocktavius.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Blocktavius.AppDQB2
{
	sealed class TileGridPainterVM : ITileGridPainterVM
	{
		public int TileSize { get; init; } = 8;

		private readonly MutableArray2D<bool> array;
		public int ColumnCount => array.Bounds.Size.X;
		public int RowCount => array.Bounds.Size.Z;

		public TileGridPainterVM(XZ size)
		{
			this.array = new MutableArray2D<bool>(new Core.Rect(XZ.Zero, size), false);
		}

		public bool GetStatus(XZ xz) => array.Sample(xz);

		public void SetStatus(XZ xz, bool on) => array.Put(xz, on);
	}

	/// <summary>
	/// The <see cref="TileGridPainterControl"/> assumes that this viewmodel is immutable with one exception:
	/// the <see cref="SetStatus"/> method may be used by the control to change the on/off status
	/// of each tile based on user input. External code MUST NOT mutate the on/off status.
	///
	/// This near-immutability implies that if a property like <see cref="TileSize"/> or <see cref="ColumnCount"/>
	/// changes, higher-level code must construct a new viewmodel and rebind the control.
	/// </summary>
	interface ITileGridPainterVM
	{
		int TileSize { get; }
		int ColumnCount { get; }
		int RowCount { get; }
		bool GetStatus(XZ xz);
		void SetStatus(XZ xz, bool on);
	}

	public partial class TileGridPainterControl : UserControl
	{
		private static readonly Brush OffBrush = Brushes.LightGray;
		private static readonly Brush OnBrush = Brushes.CornflowerBlue;
		private static readonly Pen BorderPen = new Pen(Brushes.Black, 0.5);

		static TileGridPainterControl()
		{
			OffBrush.Freeze();
			OnBrush.Freeze();
			BorderPen.Freeze();
		}

		private bool GetVM(out ITileGridPainterVM vm)
		{
			vm = (DataContext as ITileGridPainterVM)!;
			return vm != null;
		}

		public TileGridPainterControl()
		{
			this.DataContextChanged += (s, e) =>
			{
				InvalidateArrange();
				InvalidateMeasure();
				InvalidateVisual();
			};
		}

		#region Layout and Rendering

		protected override Size MeasureOverride(Size availableSize)
		{
			if (!GetVM(out var vm))
			{
				return new Size(0, 0);
			}

			return new Size(vm.ColumnCount * vm.TileSize, vm.RowCount * vm.TileSize);
		}

		protected override void OnRender(DrawingContext dc)
		{
			base.OnRender(dc);

			if (!GetVM(out var vm))
			{
				return;
			}

			// Manually draw the background first
			dc.DrawRectangle(Brushes.Pink, null, new System.Windows.Rect(0, 0, ActualWidth, ActualHeight));

			for (int z = 0; z < vm.RowCount; z++)
			{
				for (int x = 0; x < vm.ColumnCount; x++)
				{
					bool status = vm.GetStatus(new XZ(x, z));
					var brush = status ? OnBrush : OffBrush;
					var rect = new System.Windows.Rect(x * vm.TileSize, z * vm.TileSize, vm.TileSize, vm.TileSize);
					dc.DrawRectangle(brush, BorderPen, rect);
				}
			}
		}

		#endregion

		#region Mouse Input

		private bool? _drawState; // The state we are "painting" with (true for On, false for Off)
		private XZ? _lastUpdatedTile;

		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonDown(e);
			if (!GetVM(out var vm)) return;

			if (TryGetTileXZ(e.GetPosition(this), vm, out var xz))
			{
				CaptureMouse();
				bool currentStatus = vm.GetStatus(xz);
				_drawState = !currentStatus;
				vm.SetStatus(xz, _drawState.Value);
				_lastUpdatedTile = xz;
				InvalidateVisual();
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			if (e.LeftButton != MouseButtonState.Pressed || _drawState == null) return;
			if (!GetVM(out var vm)) return;

			if (TryGetTileXZ(e.GetPosition(this), vm, out var xz))
			{
				if (_lastUpdatedTile.HasValue && _lastUpdatedTile.Value.Equals(xz))
				{
					return; // Avoid redundant updates
				}
				if (vm.GetStatus(xz) == _drawState.Value)
				{
					return; // Avoid redundant updates
				}

				vm.SetStatus(xz, _drawState.Value);
				_lastUpdatedTile = xz;
				InvalidateVisual();
			}
		}

		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonUp(e);
			ReleaseMouseCapture();
			_drawState = null;
			_lastUpdatedTile = null;
		}

		private bool TryGetTileXZ(System.Windows.Point pos, ITileGridPainterVM vm, out XZ xz)
		{
			xz = new XZ(0, 0);
			if (pos.X < 0 || pos.X >= ActualWidth || pos.Y < 0 || pos.Y >= ActualHeight)
				return false;

			int tileX = (int)(pos.X / vm.TileSize);
			int tileZ = (int)(pos.Y / vm.TileSize);

			if (tileX >= vm.ColumnCount || tileZ >= vm.RowCount)
				return false;

			xz = new XZ(tileX, tileZ);
			return true;
		}

		#endregion
	}
}
