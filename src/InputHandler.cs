using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.IO;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;

namespace grid_image_viewer
{
    public class InputHandler
    {
        private readonly MainWindow _window;
        private readonly SettingsManager _settings;
        private ScrollViewer? _gridScrollViewer;
        private ResourceLoader _resourceLoader = new ResourceLoader();

        public InputHandler(MainWindow window, SettingsManager settings)
        {
            _window = window;
            _settings = settings;
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
                    _window.EditorManager.MenuUndo_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    return;
                }
                if (e.Key == VirtualKey.Y)
                {
                    _window.EditorManager.MenuRedo_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    return;
                }
                if (e.Key == VirtualKey.S)
                {
                    _window.EditorManager.MenuOverwrite_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    return;
                }
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
                _window.SlideshowManager.OpenSlideshowDialogAsync();
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyMetadata, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewerManager.ToggleMetadataPanel();
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyToggleBookmarks, e.Key, isCtrl, isShift, isAlt))
            {
                _window.EditorManager.MenuBookmarksToggle_Click(sender, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyToggleManga, e.Key, isCtrl, isShift, isAlt))
            {
                _settings.MangaSplitCount = _settings.MangaSplitCount == 1 ? 2 : (_settings.MangaSplitCount == 2 ? 4 : 1);
                _settings.SaveMangaMode();
                _ = _window.UpdateDisplayAsync();
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyToggleGrid, e.Key, isCtrl, isShift, isAlt))
            {
                _window.IsGridMode = !_window.IsGridMode;
                _ = _window.UpdateDisplayAsync();
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
                // グリッドモード中にカーソルキーが押されたら処理
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
                    if (props.MouseWheelDelta < 0) // 下へスクロール
                    {
                        if (_gridScrollViewer.VerticalOffset >= _gridScrollViewer.ScrollableHeight - 0.5)
                        {
                            _window.NavigateFolder(1);
                            e.Handled = true;
                        }
                    }
                    else // 上へスクロール
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

            // Navigate images (単一画像表示モード)
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
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            if (appWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen)
            {
                appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
                _window.AppTitleBar.Visibility = Visibility.Visible;
            }
            else
            {
                appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
                _window.AppTitleBar.Visibility = Visibility.Collapsed;
            }
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
                        if (MainWindow.IsSupportedExtension(Path.GetExtension(file.Path)))
                        {
                            string dir = Path.GetDirectoryName(file.Path) ?? string.Empty;
                            _window.LoadDirectory(dir, file.Path);
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
