using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using quick_image_viewer.ViewModels;
using System;

namespace quick_image_viewer.Views.Controls
{
    public sealed partial class GridImagePanel : UserControl
    {
        public GridView GridView => ImageGridView;
        private ScrollViewer? _gridScrollViewer;

        public GridImagePanel()
        {
            this.InitializeComponent();
            ImageGridView.KeyDown += ImageGridView_KeyDown;
            ImageGridView.PointerWheelChanged += ImageGridView_PointerWheelChanged;
        }

        private void ImageGridView_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Left || e.Key == Windows.System.VirtualKey.Right ||
                e.Key == Windows.System.VirtualKey.Up || e.Key == Windows.System.VirtualKey.Down)
            {
                int selectedIdx = ImageGridView.SelectedIndex;
                int columns = 1;
                if (ImageGridView.ItemsPanelRoot is ItemsWrapGrid wrap && wrap.ItemWidth > 0)
                {
                    double availW = ImageGridView.ActualWidth - ImageGridView.Padding.Left - ImageGridView.Padding.Right - 24;
                    columns = Math.Max(1, (int)(availW / wrap.ItemWidth));
                }
                int currentRow = selectedIdx / columns;
                int totalRows = (int)Math.Ceiling((double)ImageGridView.Items.Count / columns);

                if (e.Key == Windows.System.VirtualKey.Up && currentRow == 0)
                {
                    WeakReferenceMessenger.Default.Send(new FolderNavigationMessage(-1));
                    e.Handled = true;
                    return;
                }
                if (e.Key == Windows.System.VirtualKey.Down && currentRow >= totalRows - 1)
                {
                    WeakReferenceMessenger.Default.Send(new FolderNavigationMessage(1));
                    e.Handled = true;
                    return;
                }

                if (e.Key == Windows.System.VirtualKey.Down && currentRow == totalRows - 2)
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

                var focused = FocusManager.GetFocusedElement(this.XamlRoot);
                if (focused is not GridViewItem)
                {
                    if (ImageGridView.SelectedItem != null)
                    {
                        var container = ImageGridView.ContainerFromItem(ImageGridView.SelectedItem) as GridViewItem;
                        container?.Focus(FocusState.Programmatic);
                    }
                    e.Handled = true;
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

        private void ImageGridView_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var props = e.GetCurrentPoint(this).Properties;
            bool isCtrl = e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control);

            if (isCtrl) return; // Zoom is handled elsewhere

            if (_gridScrollViewer == null)
            {
                _gridScrollViewer = GetScrollViewer(ImageGridView);
            }

            if (_gridScrollViewer != null)
            {
                if (props.MouseWheelDelta < 0) // Scroll down
                {
                    if (_gridScrollViewer.VerticalOffset >= _gridScrollViewer.ScrollableHeight - 0.5)
                    {
                        WeakReferenceMessenger.Default.Send(new FolderNavigationMessage(1));
                        e.Handled = true;
                    }
                }
                else // Scroll up
                {
                    if (_gridScrollViewer.VerticalOffset <= 0.5)
                    {
                        WeakReferenceMessenger.Default.Send(new FolderNavigationMessage(-1));
                        e.Handled = true;
                    }
                }
            }
        }
    }
}