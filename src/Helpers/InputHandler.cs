using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Managers;
using quick_image_viewer.Services;
using quick_image_viewer.ViewModels;
using System;
using System.IO;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
namespace quick_image_viewer.Helpers
{
    public class InputHandler : IInputHandler
    {
        private readonly IMainView _window;
        private readonly ISettingsManager _settings;
        private ScrollViewer? _gridScrollViewer;
        private ResourceLoader _resourceLoader = new ResourceLoader();
        private Windows.Foundation.Point _lastPointerPoint;

        public InputHandler(IMainView window, ISettingsManager settings)
        {
            _window = window;
            _settings = settings;
        }

        public void HandlePointerMoved(object sender, PointerRoutedEventArgs e)
        {
            _lastPointerPoint = e.GetCurrentPoint(_window.PagesGrid).Position;
        }

        private string GetPathAtPointer()
        {
            if (_window.IsGridMode)
            {
                // In grid mode, we use the selected item or fallback to current index
                return _window.CurrentImagePath;
            }

            var pageGrids = _window.GetPageGrids();
            for (int i = 0; i < pageGrids.Count; i++)
            {
                if (pageGrids[i].Visibility != Visibility.Visible) continue;

                // Check if point is inside this page grid
                var ttv = _window.PagesGrid.TransformToVisual(pageGrids[i]);
                try
                {
                    var localPoint = ttv.TransformPoint(_lastPointerPoint);
                    if (localPoint.X >= 0 && localPoint.X <= pageGrids[i].ActualWidth &&
                        localPoint.Y >= 0 && localPoint.Y <= pageGrids[i].ActualHeight)
                    {
                        return _window.ViewerManager.GetPathForPage(i) ?? _window.CurrentImagePath;
                    }
                }
                catch { }
            }

            return _window.CurrentImagePath;
        }

        private bool IsMatch(KeyBindingData binding, VirtualKey key, bool ctrl, bool shift, bool alt)
        {
            return binding.Key == key && binding.Ctrl == ctrl && binding.Shift == shift && binding.Alt == alt;
        }

        public void HandleKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (_window.IsDialogOpen) return;

            bool isCtrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            bool isShift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            bool isAlt = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (isCtrl)
            {
                if (e.Key == VirtualKey.Z)
                {
                    _window.ViewModel.UndoCommand.Execute(GetPathAtPointer());
                    e.Handled = true;
                    return;
                }
                if (e.Key == VirtualKey.Y)
                {
                    _window.ViewModel.RedoCommand.Execute(GetPathAtPointer());
                    e.Handled = true;
                    return;
                }
                if (e.Key == VirtualKey.S)
                {
                    _window.ViewModel.OverwriteCommand.Execute(GetPathAtPointer());
                    e.Handled = true;
                    return;
                }
            }

            if (IsMatch(_settings.KeyCopyPath, e.Key, isCtrl, isShift, isAlt))
            {
                string path = GetPathAtPointer();
                if (!string.IsNullOrEmpty(path))
                {
                    var dataPackage = new DataPackage();
                    dataPackage.SetText(path);
                    Clipboard.SetContent(dataPackage);
                    _window.ShowNotification(_resourceLoader.GetString("Notification_PathCopied"));
                }
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyZoomIn, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.ZoomInCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (IsMatch(_settings.KeyZoomOut, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.ZoomOutCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (IsMatch(_settings.KeyZoomReset, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.ZoomResetCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (IsMatch(_settings.KeyZoom100, e.Key, isCtrl, isShift, isAlt))
            {
                var path = GetPathAtPointer();
                if (!string.IsNullOrEmpty(path))
                {
                    var (w, h) = _window.ImageEditService.GetImageSize(path);
                    if (w > 0 && _window.ImageScrollViewer.ViewportWidth > 0)
                    {
                        float factor = (float)(w / _window.ImageScrollViewer.ViewportWidth);
                        _window.ImageScrollViewer.ChangeView(null, null, factor);
                    }
                }
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyRotateRight, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.RotateRightCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (IsMatch(_settings.KeyRotateLeft, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.RotateLeftCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (IsMatch(_settings.KeyFlipHorizontal, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.FlipHorzCommand.Execute(GetPathAtPointer());
                e.Handled = true;
                return;
            }
            if (IsMatch(_settings.KeyAddBookmark, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.ToggleBookmarkCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyDeleteFile, e.Key, isCtrl, isShift, isAlt))
            {
                WeakReferenceMessenger.Default.Send(new DeleteFileMessage(GetPathAtPointer()));
                e.Handled = true;
                return;
            }

            if (_window.SlideshowManager.IsSlideshowRunning && !IsMatch(_settings.KeySlideshow, e.Key, isCtrl, isShift, isAlt))
            {
                _window.SlideshowManager.StopSlideshow();
                if (IsMatch(_settings.KeyExit, e.Key, isCtrl, isShift, isAlt))
                {
                    if (_window.AppWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen)
                    {
                        _window.AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
                        _window.AppTitleBar.Visibility = Visibility.Visible;
                    }
                    e.Handled = true;
                    return;
                }
            }

            if (IsMatch(_settings.KeySlideshow, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.OpenSlideshowCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyMetadata, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.ToggleMetadataCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyToggleBookmarks, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.ToggleBookmarkPanelCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyToggleFullscreen, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.ToggleFullscreenCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyToggleStretchMode, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.ToggleStretchModeCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyToggleManga, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.ToggleMangaModeCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyToggleGrid, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.ToggleGridModeCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyExit, e.Key, isCtrl, isShift, isAlt))
            {
                if (_window.AppWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen)
                {
                    _window.AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
                    _window.AppTitleBar.Visibility = Visibility.Visible;
                    e.Handled = true;
                    return;
                }
                _window.Close();
                e.Handled = true;
                return;
            }

            if (_window.IsGridMode)
            {
                // Handle cursor keys during grid mode
                if (e.Key == Windows.System.VirtualKey.Left || e.Key == Windows.System.VirtualKey.Right ||
                    e.Key == Windows.System.VirtualKey.Up || e.Key == Windows.System.VirtualKey.Down)
                {
                    int selectedIdx = _window.ImageGridView.SelectedIndex;
                    int columns = 1;
                    if (_window.ImageGridView.ItemsPanelRoot is ItemsWrapGrid wrap && wrap.ItemWidth > 0)
                    {
                        double availW = _window.ImageGridView.ActualWidth - _window.ImageGridView.Padding.Left - _window.ImageGridView.Padding.Right - 24;
                        columns = Math.Max(1, (int)(availW / wrap.ItemWidth));
                    }
                    int currentRow = selectedIdx / columns;
                    int totalRows = (int)Math.Ceiling((double)_window.GridItems.Count / columns);

                    if (e.Key == Windows.System.VirtualKey.Up && currentRow == 0)
                    {
                        _window.NavigateFolder(-1);
                        e.Handled = true;
                        return;
                    }
                    if (e.Key == Windows.System.VirtualKey.Down && currentRow >= totalRows - 1)
                    {
                        _window.NavigateFolder(1);
                        e.Handled = true;
                        return;
                    }

                    if (e.Key == Windows.System.VirtualKey.Down && currentRow == totalRows - 2)
                    {
                        int targetIdx = selectedIdx + columns;
                        if (targetIdx >= _window.GridItems.Count)
                        {
                            _window.ImageGridView.SelectedIndex = _window.GridItems.Count - 1;
                            _window.ImageGridView.ScrollIntoView(_window.ImageGridView.SelectedItem);

                            _window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                            {
                                var container = _window.ImageGridView.ContainerFromIndex(_window.ImageGridView.SelectedIndex) as GridViewItem;
                                container?.Focus(FocusState.Programmatic);
                            });
                            e.Handled = true;
                            return;
                        }
                    }

                    var focused = FocusManager.GetFocusedElement(_window.Content.XamlRoot);
                    if (focused is not GridViewItem)
                    {
                        if (_window.ImageGridView.SelectedItem != null)
                        {
                            var container = _window.ImageGridView.ContainerFromItem(_window.ImageGridView.SelectedItem) as GridViewItem;
                            container?.Focus(FocusState.Programmatic);
                        }
                        e.Handled = true;
                    }
                }
                return;
            }

            if (e.Key == Windows.System.VirtualKey.Left)
            {
                _window.Navigate(_settings.MangaSplitCount > 1 ? 1 : -1, isShift);
                e.Handled = true;
                return;
            }
            if (e.Key == Windows.System.VirtualKey.Right)
            {
                _window.Navigate(_settings.MangaSplitCount > 1 ? -1 : 1, isShift);
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyPrevImage, e.Key, isCtrl, isShift, isAlt))
            {
                _window.Navigate(-1, isShift);
                e.Handled = true;
            }
            else if (IsMatch(_settings.KeyNextImage, e.Key, isCtrl, isShift, isAlt))
            {
                _window.Navigate(1, isShift);
                e.Handled = true;
            }
            else if (IsMatch(_settings.KeyPrevFolder, e.Key, isCtrl, isShift, isAlt))
            {
                _window.NavigateFolder(-1);
                e.Handled = true;
            }
            else if (IsMatch(_settings.KeyNextFolder, e.Key, isCtrl, isShift, isAlt))
            {
                _window.NavigateFolder(1);
                e.Handled = true;
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

        public void HandlePointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (e.Handled && sender is not GridView) return;

            var props = e.GetCurrentPoint(_window.RootGrid).Properties;
            bool isCtrl = e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control);
            bool isShift = e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Shift);

            if (isCtrl)
            {
                // Let ScrollViewer handle Zoom
                return;
            }

            if (_window.IsGridMode)
            {
                if (_gridScrollViewer == null)
                {
                    _gridScrollViewer = GetScrollViewer(_window.ImageGridView);
                }

                if (_gridScrollViewer != null)
                {
                    if (props.MouseWheelDelta < 0) // Scroll down
                    {
                        if (_gridScrollViewer.VerticalOffset >= _gridScrollViewer.ScrollableHeight - 0.5)
                        {
                            _window.NavigateFolder(1);
                            e.Handled = true;
                        }
                    }
                    else // Scroll up
                    {
                        if (_gridScrollViewer.VerticalOffset <= 0.5)
                        {
                            _window.NavigateFolder(-1);
                            e.Handled = true;
                        }
                    }
                }
                return;
            }

            // Navigate images (Single image display mode)
            if (props.MouseWheelDelta < 0)
            {
                _window.Navigate(1, isShift); // Next
            }
            else
            {
                _window.Navigate(-1, isShift); // Prev
            }
            e.Handled = true;
        }

        public void HandleDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (_window.IsDialogOpen) return;
            _window.ViewModel.ToggleFullscreenCommand.Execute(null);
        }

        public async void HandleDrop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    var item = items[0];
                    if (item is StorageFolder folder)
                    {
                        _window.LoadDirectory(folder.Path, string.Empty);
                    }
                    else if (item is StorageFile file)
                    {
                        if (FolderDiscoveryService.IsSupportedExtension(Path.GetExtension(file.Path), _settings.EnabledExtensions))
                        {
                            string dir = Path.GetDirectoryName(file.Path) ?? string.Empty;
                            _window.LoadDirectory(dir, file.Path);
                        }
                        else if (ArchiveManager.IsArchive(file.Path))
                        {
                            _window.LoadDirectory(file.Path);
                        }
                        else
                        {
                            _window.ShowNotification(_resourceLoader.GetString("Notification_NotAnImage"));
                        }
                    }
                }
            }
        }

        public void HandleDragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

    }
}
