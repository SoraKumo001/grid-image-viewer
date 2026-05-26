using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using quick_image_viewer.Interfaces;
using System;
using Windows.Foundation;
namespace quick_image_viewer.Views.Controls
{
    public sealed partial class CropOverlay : UserControl
    {
        private readonly IOverlayHost _window;
        private Point _startPoint;
        private bool _isSelecting = false;
        private Rect _selectionRect;

        public CropOverlay(IOverlayHost window)
        {
            this.InitializeComponent();
            _window = window;
            this.SizeChanged += (s, e) => UpdateDimmedPath(_selectionRect.X, _selectionRect.Y, _selectionRect.Width, _selectionRect.Height);

            // Listen for global Enter/Esc keys
            this.KeyDown += CropOverlay_KeyDown;
        }

        private void OverlayRoot_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(OverlayRoot);
            if (point.Properties.IsLeftButtonPressed)
            {
                _isSelecting = true;
                _startPoint = point.Position;
                OverlayRoot.CapturePointer(e.Pointer);

                Canvas.SetLeft(SelectionRect, _startPoint.X);
                Canvas.SetTop(SelectionRect, _startPoint.Y);
                SelectionRect.Width = 0;
                SelectionRect.Height = 0;
                SelectionRect.Visibility = Visibility.Visible;
                e.Handled = true;
            }
        }

        private void OverlayRoot_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isSelecting)
            {
                var point = e.GetCurrentPoint(OverlayRoot);
                double x = Math.Min(point.Position.X, _startPoint.X);
                double y = Math.Min(point.Position.Y, _startPoint.Y);
                double w = Math.Abs(point.Position.X - _startPoint.X);
                double h = Math.Abs(point.Position.Y - _startPoint.Y);

                _selectionRect = new Rect(x, y, w, h);
                Canvas.SetLeft(SelectionRect, x);
                Canvas.SetTop(SelectionRect, y);
                SelectionRect.Width = w;
                SelectionRect.Height = h;

                UpdateDimmedPath(x, y, w, h);
                e.Handled = true;
            }
        }

        private void OverlayRoot_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isSelecting)
            {
                _isSelecting = false;
                OverlayRoot.ReleasePointerCapture(e.Pointer);

                if (SelectionRect.Width < 5 || SelectionRect.Height < 5)
                {
                    SelectionRect.Visibility = Visibility.Collapsed;
                    UpdateDimmedPath(0, 0, 0, 0);
                }
                e.Handled = true;
            }
        }

        private void UpdateDimmedPath(double x, double y, double w, double h)
        {
            var geometry = new GeometryGroup { FillRule = FillRule.EvenOdd };
            geometry.Children.Add(new RectangleGeometry { Rect = new Rect(0, 0, ActualWidth, ActualHeight) });
            geometry.Children.Add(new RectangleGeometry { Rect = new Rect(x, y, w, h) });
            DimmedPath.Data = geometry;
        }

        private void CropOverlay_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ExecuteCrop();
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                Close();
            }
        }

        private void ExecuteCrop()
        {
            if (SelectionRect.Visibility != Visibility.Visible || SelectionRect.Width < 5) return;

            // Use the same math as MenuStateManager.MenuCrop_Click but relative to this overlay
            // Actually, we need to find which image was clicked. 
            // In the original MenuStateManager, it used _window.PagesGrid to determine coordinates.
            // Since this overlay is exactly over the RootGrid (or Row 1), we can transform points.

            _window.MenuStateManager.ExecuteCropWithRect(_selectionRect);
            Close();
        }

        private void Close()
        {
            var parent = this.Parent as Panel;
            parent?.Children.Remove(this);
            _window.IsDialogOpen = false;
        }
    }
}
