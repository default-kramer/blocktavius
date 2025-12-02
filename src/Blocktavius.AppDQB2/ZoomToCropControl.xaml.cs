using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Blocktavius.AppDQB2;

public partial class ZoomToCropControl : UserControl
{
	public static readonly DependencyProperty ZoomableContentProperty =
		DependencyProperty.Register("ZoomableContent", typeof(object), typeof(ZoomToCropControl), new PropertyMetadata(null));

	public object ZoomableContent
	{
		get { return GetValue(ZoomableContentProperty); }
		set { SetValue(ZoomableContentProperty, value); }
	}

	private bool isDragging = false;
	private Point selectionStartPoint;
	private double contentWidth;
	private double contentHeight;

	public ZoomToCropControl()
	{
		InitializeComponent();
		this.Loaded += ZoomToCropControl_Loaded;
		this.LayoutUpdated += ZoomToCropControl_LayoutUpdated;
	}

	private void ZoomToCropControl_LayoutUpdated(object? sender, EventArgs e)
	{
		if (contentWidth != measurementViewer.ExtentWidth || contentHeight != measurementViewer.ExtentHeight)
		{
			TrySetContentSize();
		}
	}

	private void ZoomToCropControl_Loaded(object sender, RoutedEventArgs e)
	{
		theBrush.Visual = contentHost;

		Dispatcher.BeginInvoke(new Action(() =>
		{
			TrySetContentSize();
		}), System.Windows.Threading.DispatcherPriority.ContextIdle);
	}

	private void TrySetContentSize()
	{
		contentWidth = measurementViewer.ExtentWidth;
		contentHeight = measurementViewer.ExtentHeight;

		if (contentWidth > 0 && contentHeight > 0)
		{
			ResetZoom();
		}
	}

	private static readonly Rect NoZoom = new(0, 0, 1, 1);

	private void UpdateZoomState()
	{
		bool isZoomed = theBrush.Viewbox != NoZoom;

		resetButton.Visibility = isZoomed ? Visibility.Visible : Visibility.Collapsed;
		zoomMessageTextBlock.Visibility = isZoomed ? Visibility.Collapsed : Visibility.Visible;
		interactionCanvas.IsHitTestVisible = !isZoomed;
	}

	private void ResetZoom()
	{
		if (contentWidth > 0 && contentHeight > 0)
		{
			theBrush.Viewbox = NoZoom;
			viewboxContentGrid.Width = contentWidth;
			viewboxContentGrid.Height = contentHeight;
		}
		UpdateZoomState();
	}

	private void ResetZoom_Click(object sender, RoutedEventArgs e)
	{
		ResetZoom();
	}

	private IEnumerable<Rectangle> SelectionRects => [selectionRectangle, selectionRectangleWhite];

	private void InteractionCanvas_MouseDown(object sender, MouseButtonEventArgs e)
	{
		if (e.LeftButton != MouseButtonState.Pressed || contentWidth <= 0) return;

		isDragging = true;
		selectionStartPoint = e.GetPosition(interactionCanvas);
		foreach (var rect in SelectionRects)
		{
			rect.SetValue(Canvas.LeftProperty, selectionStartPoint.X);
			rect.SetValue(Canvas.TopProperty, selectionStartPoint.Y);
			rect.Width = 0;
			rect.Height = 0;
			rect.Visibility = Visibility.Visible;
		}

		interactionCanvas.CaptureMouse();
	}

	private void InteractionCanvas_MouseMove(object sender, MouseEventArgs e)
	{
		if (isDragging)
		{
			Point currentPoint = e.GetPosition(interactionCanvas);
			var left = Math.Min(currentPoint.X, selectionStartPoint.X);
			var top = Math.Min(currentPoint.Y, selectionStartPoint.Y);
			var width = Math.Abs(currentPoint.X - selectionStartPoint.X);
			var height = Math.Abs(currentPoint.Y - selectionStartPoint.Y);
			foreach (var rect in SelectionRects)
			{
				rect.SetValue(Canvas.LeftProperty, left);
				rect.SetValue(Canvas.TopProperty, top);
				rect.Width = width;
				rect.Height = height;
			}
		}
	}

	private void InteractionCanvas_MouseUp(object sender, MouseButtonEventArgs e)
	{
		if (isDragging)
		{
			isDragging = false;
			interactionCanvas.ReleaseMouseCapture();
			foreach (var rect in SelectionRects)
			{
				rect.Visibility = Visibility.Collapsed;
			}

			Point endPoint = e.GetPosition(interactionCanvas);
			var selectionInView = new Rect(selectionStartPoint, endPoint);

			if (selectionInView.Width > 5 && selectionInView.Height > 5)
			{
				double relativeX = selectionInView.X / interactionCanvas.ActualWidth;
				double relativeY = selectionInView.Y / interactionCanvas.ActualHeight;
				double relativeWidth = selectionInView.Width / interactionCanvas.ActualWidth;
				double relativeHeight = selectionInView.Height / interactionCanvas.ActualHeight;

				Rect currentBrushViewbox = theBrush.Viewbox;
				var newBrushViewbox = new Rect(
					currentBrushViewbox.X + relativeX * currentBrushViewbox.Width,
					currentBrushViewbox.Y + relativeY * currentBrushViewbox.Height,
					currentBrushViewbox.Width * relativeWidth,
					currentBrushViewbox.Height * relativeHeight
					);

				theBrush.Viewbox = newBrushViewbox;

				viewboxContentGrid.Width = selectionInView.Width;
				viewboxContentGrid.Height = selectionInView.Height;

				UpdateZoomState();
			}
		}
	}
}
