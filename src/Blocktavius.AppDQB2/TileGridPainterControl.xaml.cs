using Blocktavius.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
				// When the VM changes, we need to discard the old bitmaps.
				_gridBitmap = null;
				_cachedTileSize = -1;
				_onTileBitmap = null;
				_offTileBitmap = null;

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

		// --- NEW BITMAP-BASED RENDERING ---

		private WriteableBitmap? _gridBitmap;
		private RenderTargetBitmap? _onTileBitmap;
		private RenderTargetBitmap? _offTileBitmap;
		private int _cachedTileSize = -1;

		protected override void OnRender(DrawingContext dc)
		{
			base.OnRender(dc);

			if (!GetVM(out var vm))
			{
				// Draw a placeholder if there's no data
				dc.DrawRectangle(Brushes.Pink, null, new System.Windows.Rect(0, 0, ActualWidth, ActualHeight));
				return;
			}

			// Ensure bitmaps are created and up-to-date
			PrepareBitmaps(vm);

			if (_gridBitmap != null)
			{
				dc.DrawImage(_gridBitmap, new System.Windows.Rect(0, 0, ActualWidth, ActualHeight));
			}
		}

		private void PrepareBitmaps(ITileGridPainterVM vm)
		{
			var tileSize = vm.TileSize;
			if (tileSize <= 0) return;

			var pixelWidth = vm.ColumnCount * tileSize;
			var pixelHeight = vm.RowCount * tileSize;
			if (pixelWidth <= 0 || pixelHeight <= 0) return;

			// If tile size changed, we need to regenerate the tile templates
			if (_cachedTileSize != tileSize)
			{
				_cachedTileSize = tileSize;
				_onTileBitmap = CreateTileBitmap(tileSize, true);
				_offTileBitmap = CreateTileBitmap(tileSize, false);
				_gridBitmap = null; // Force full redraw
			}

			// If grid bitmap doesn't exist or has wrong size, create it and draw everything
			if (_gridBitmap == null || _gridBitmap.PixelWidth != pixelWidth || _gridBitmap.PixelHeight != pixelHeight)
			{
				_gridBitmap = new WriteableBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32, null);
				RedrawAllTiles(vm);
			}
		}

		private RenderTargetBitmap CreateTileBitmap(int tileSize, bool on)
		{
			var visual = new DrawingVisual();
			using (var dc = visual.RenderOpen())
			{
				var brush = on ? OnBrush : OffBrush;
				var rect = new System.Windows.Rect(0, 0, tileSize, tileSize);
				dc.DrawRectangle(brush, BorderPen, rect);
			}

			var rtb = new RenderTargetBitmap(tileSize, tileSize, 96, 96, PixelFormats.Pbgra32);
			rtb.Render(visual);
			rtb.Freeze();
			return rtb;
		}

		private void RedrawAllTiles(ITileGridPainterVM vm)
		{
			if (_gridBitmap == null) return;

			for (int z = 0; z < vm.RowCount; z++)
			{
				for (int x = 0; x < vm.ColumnCount; x++)
				{
					bool status = vm.GetStatus(new XZ(x, z));
					UpdateBitmapTile(vm, new XZ(x, z), status, invalidate: false);
				}
			}
		}

		private void UpdateBitmapTile(ITileGridPainterVM vm, XZ xz, bool on, bool invalidate = true)
		{
			// Ensure bitmaps are ready. This might be called from a mouse event before the first render.
			if (_gridBitmap == null || _onTileBitmap == null || _offTileBitmap == null)
			{
				PrepareBitmaps(vm);
				if (_gridBitmap == null || _onTileBitmap == null || _offTileBitmap == null)
				{
					return;
				}
			}

			var sourceBitmap = on ? _onTileBitmap : _offTileBitmap;
			var tileSize = vm.TileSize;
			var destRect = new Int32Rect(xz.X * tileSize, xz.Z * tileSize, tileSize, tileSize);

			// Check if the rectangle is within the bitmap bounds
			if (destRect.X + destRect.Width > _gridBitmap.PixelWidth || destRect.Y + destRect.Height > _gridBitmap.PixelHeight)
			{
				// This can happen if the VM size changes and we haven't fully invalidated yet.
				// A full InvalidateVisual should fix it on the next pass.
				InvalidateVisual();
				return;
			}

			int stride = sourceBitmap.PixelWidth * (sourceBitmap.Format.BitsPerPixel / 8);
			var pixels = new byte[sourceBitmap.PixelHeight * stride];
			sourceBitmap.CopyPixels(pixels, stride, 0);

			_gridBitmap.WritePixels(destRect, pixels, stride, 0);

			if (invalidate)
			{
				InvalidateVisual();
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

				UpdateBitmapTile(vm, xz, _drawState.Value);
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

				UpdateBitmapTile(vm, xz, _drawState.Value);
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
			if (vm.TileSize <= 0) return false; // Avoid division by zero
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
