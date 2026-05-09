using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using quick_image_viewer.ViewModels;
using System;
using Windows.System;

namespace quick_image_viewer.Views.Controls
{
    public sealed partial class GridImagePanel : UserControl
    {
        public GridView GridView => ImageGridView;
        private ScrollViewer? _gridScrollViewer;

        public void SetLoading(bool isLoading)
        {
            LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SetBackgroundLoading(bool isLoading)
        {
            BackgroundProgressBar.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        }

        public GridImagePanel()
        {
            this.InitializeComponent();
            ImageGridView.KeyDown += ImageGridView_KeyDown;
            // Use AddHandler with handledEventsToo = true to intercept wheel events swallowed by ScrollViewer
            ImageGridView.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(ImageGridView_PointerWheelChanged), true);
        }

        private DateTime _lastBoundaryKeyPress = DateTime.MinValue;
        private VirtualKey _lastBoundaryKey = VirtualKey.None;

        private void ImageGridView_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.PageUp)
            {
                WeakReferenceMessenger.Default.Send(new FolderNavigationMessage(-1));
                e.Handled = true;
                return;
            }
            if (e.Key == VirtualKey.PageDown)
            {
                WeakReferenceMessenger.Default.Send(new FolderNavigationMessage(1));
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Left || e.Key == VirtualKey.Right ||
                e.Key == VirtualKey.Up || e.Key == VirtualKey.Down)
            {
                int selectedIdx = ImageGridView.SelectedIndex;
                if (selectedIdx == -1) return;

                int columns = 1;
                if (ImageGridView.ItemsPanelRoot is ItemsWrapGrid wrap && wrap.ItemWidth > 0)
                {
                    double availW = ImageGridView.ActualWidth - ImageGridView.Padding.Left - ImageGridView.Padding.Right - 24;
                    columns = Math.Max(1, (int)(availW / wrap.ItemWidth));
                }
                int currentRow = selectedIdx / columns;
                int totalRows = (int)Math.Ceiling((double)ImageGridView.Items.Count / columns);

                // Boundary navigation logic
                bool isAtBoundary = false;
                if (e.Key == VirtualKey.Up && currentRow == 0) isAtBoundary = true;
                else if (e.Key == VirtualKey.Down && currentRow >= totalRows - 1) isAtBoundary = true;
                else if (e.Key == VirtualKey.Left && selectedIdx == 0) isAtBoundary = true;
                else if (e.Key == VirtualKey.Right && selectedIdx == ImageGridView.Items.Count - 1) isAtBoundary = true;

                if (isAtBoundary)
                {
                    // Require double-tap or intentional press at boundary to navigate folder
                    var now = DateTime.Now;
                    if (_lastBoundaryKey == e.Key && (now - _lastBoundaryKeyPress).TotalMilliseconds < 500)
                    {
                        int offset = (e.Key == VirtualKey.Up || e.Key == VirtualKey.Left) ? -1 : 1;
                        WeakReferenceMessenger.Default.Send(new FolderNavigationMessage(offset));
                        _lastBoundaryKey = VirtualKey.None;
                    }
                    else
                    {
                        _lastBoundaryKey = e.Key;
                        _lastBoundaryKeyPress = now;

                        // If at top/bottom row but not at first/last item, move to extreme
                        if (e.Key == VirtualKey.Up && selectedIdx > 0) ImageGridView.SelectedIndex = 0;
                        else if (e.Key == VirtualKey.Down && selectedIdx < ImageGridView.Items.Count - 1) ImageGridView.SelectedIndex = ImageGridView.Items.Count - 1;

                        ImageGridView.ScrollIntoView(ImageGridView.SelectedItem);
                    }
                    e.Handled = true;
                    return;
                }

                _lastBoundaryKey = VirtualKey.None;

                // Handle case where Down is pressed on second-to-last row but no item is directly below
                if (e.Key == VirtualKey.Down && currentRow == totalRows - 2)
                {
                    int targetIdx = selectedIdx + columns;
                    if (targetIdx >= ImageGridView.Items.Count)
                    {
                        ImageGridView.SelectedIndex = ImageGridView.Items.Count - 1;
                        ImageGridView.ScrollIntoView(ImageGridView.SelectedItem);

                        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                        {
                            var container = ImageGridView.ContainerFromIndex(ImageGridView.SelectedIndex) as GridViewItem;
                            container?.Focus(FocusState.Programmatic);
                        });
                        e.Handled = true;
                        return;
                    }
                }

                var focused = (this.IsLoaded && this.XamlRoot != null ? FocusManager.GetFocusedElement(this.XamlRoot) : null);
                if (focused is not GridViewItem && !ReferenceEquals(focused, ImageGridView))
                {
                    if (ImageGridView.SelectedItem != null)
                    {
                        var container = ImageGridView.ContainerFromItem(ImageGridView.SelectedItem) as GridViewItem;
                        if (container != null)
                        {
                            container.Focus(FocusState.Programmatic);
                            e.Handled = true;
                        }
                    }
                }
            }
        }

        private ScrollViewer? GetScrollViewer(DependencyObject element)
        {
            if (element is ScrollViewer sv) return sv;
            for (int i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(element, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private int _accumulatedWheelDelta = 0;

        private void ImageGridView_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var props = e.GetCurrentPoint(this).Properties;
            bool isCtrl = e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control);

            if (isCtrl) return;

            if (_gridScrollViewer == null)
            {
                _gridScrollViewer = GetScrollViewer(ImageGridView);
            }

            if (_gridScrollViewer != null)
            {
                int delta = props.MouseWheelDelta;
                int direction = delta < 0 ? 1 : -1; // 1: Down/Next, -1: Up/Prev
                bool atBoundary = false;

                // Check if we are at the top or bottom of the scrollable area
                if (direction > 0 && _gridScrollViewer.VerticalOffset >= _gridScrollViewer.ScrollableHeight - 1.0) atBoundary = true;
                else if (direction < 0 && _gridScrollViewer.VerticalOffset <= 1.0) atBoundary = true;

                if (atBoundary)
                {
                    // Accumulate delta to require intentional movement
                    if (Math.Sign(_accumulatedWheelDelta) != Math.Sign(delta))
                    {
                        _accumulatedWheelDelta = 0;
                    }

                    _accumulatedWheelDelta += delta;

                    // Standard mouse notch is 120. Require 2 notches (240) for folder navigation.
                    // 3 notches (360) was a bit too much for some users.
                    if (Math.Abs(_accumulatedWheelDelta) >= 240)
                    {
                        WeakReferenceMessenger.Default.Send(new FolderNavigationMessage(direction));
                        _accumulatedWheelDelta = 0;
                    }

                    // Always mark as handled when at boundary to prevent scroll jitter 
                    // and allow our accumulation to work even if the event was already handled by the ScrollViewer
                    e.Handled = true;
                }
                else
                {
                    _accumulatedWheelDelta = 0;
                }
            }
        }
    }
}