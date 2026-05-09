using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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

        private Windows.Foundation.Point _lastPointerPoint;
        private bool _isPanning = false;
        private Windows.Foundation.Point _startPointerPoint;

        private double _startTranslateX;
        private double _startTranslateY;

        public InputHandler(IMainView window, ISettingsManager settings)
        {
            _window = window;
            _settings = settings;
        }

        public void HandlePointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_window.IsGridMode) return;

            var ptr = e.GetCurrentPoint(_window.RootGrid);
            var keyState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
            bool isCtrl = keyState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (isCtrl && ptr.Properties.IsLeftButtonPressed)
            {
                _isPanning = true;
                _startPointerPoint = ptr.Position;

                var transform = GetTransform(_window.PagesGrid);
                _startTranslateX = transform.TranslateX;
                _startTranslateY = transform.TranslateY;

                ((UIElement)sender).CapturePointer(e.Pointer);
                e.Handled = true;
            }
        }

        private CompositeTransform GetTransform(UIElement element)
        {
            if (element.RenderTransform is CompositeTransform ct) return ct;
            var newCt = new CompositeTransform();
            element.RenderTransform = newCt;
            return newCt;
        }

        public void HandlePointerMoved(object sender, PointerRoutedEventArgs e)
        {
            // Update last point for metadata etc.
            try { _lastPointerPoint = e.GetCurrentPoint(_window.PagesGrid).Position; } catch { }
            _window.MetadataDisplayService.HandlePointerMoved(e);

            if (_isPanning)
            {
                var ptr = e.GetCurrentPoint(_window.RootGrid);
                var currentPoint = ptr.Position;

                double deltaX = currentPoint.X - _startPointerPoint.X;
                double deltaY = currentPoint.Y - _startPointerPoint.Y;

                var transform = GetTransform(_window.PagesGrid);
                float zoom = _window.ImageScrollViewer.ZoomFactor;

                // ビューポート（画面）上での移動量を現在のズーム倍率で割ることで、
                // コンテンツ（画像）座標系での移動量に変換します。
                // これにより、ズーム状態に関わらずマウスの動きに追従して画像が動きます。
                transform.TranslateX = _startTranslateX + (deltaX / zoom);
                transform.TranslateY = _startTranslateY + (deltaY / zoom);

                e.Handled = true;
            }
        }

        public void HandlePointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                ((UIElement)sender).ReleasePointerCapture(e.Pointer);
                e.Handled = true;
            }
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
            if (binding.Ctrl != ctrl || binding.Shift != shift || binding.Alt != alt) return false;
            if (binding.Key == key) return true;

            // Handle Numpad/Number row equivalents for zoom reset
            if (binding.Key == VirtualKey.Number0 && key == VirtualKey.NumberPad0) return true;
            if (binding.Key == VirtualKey.NumberPad0 && key == VirtualKey.Number0) return true;

            // Handle Zoom In equivalents (+ and =)
            if (binding.Key == VirtualKey.Add && (int)key == 187) return true; // 187 is VK_OEM_PLUS
            if ((int)binding.Key == 187 && key == VirtualKey.Add) return true;

            // Handle Zoom Out equivalents (-)
            if (binding.Key == VirtualKey.Subtract && (int)key == 189) return true; // 189 is VK_OEM_MINUS
            if ((int)binding.Key == 189 && key == VirtualKey.Subtract) return true;

            return false;
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
                    _window.ViewModel.Editor.UndoCommand.Execute(GetPathAtPointer());
                    e.Handled = true;
                    return;
                }
                if (e.Key == VirtualKey.Y)
                {
                    _window.ViewModel.Editor.RedoCommand.Execute(GetPathAtPointer());
                    e.Handled = true;
                    return;
                }
                if (e.Key == VirtualKey.S)
                {
                    _window.ViewModel.Editor.OverwriteCommand.Execute(GetPathAtPointer());
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
                    _window.ShowNotification(_settings.GetString("Notification_PathCopied"));
                }
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyZoomIn, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.Viewer.ZoomInCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (IsMatch(_settings.KeyZoomOut, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.Viewer.ZoomOutCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (IsMatch(_settings.KeyZoomReset, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.Viewer.ZoomResetCommand.Execute(null);
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
                _window.ViewModel.Editor.RotateRightCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (IsMatch(_settings.KeyRotateLeft, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.Editor.RotateLeftCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (IsMatch(_settings.KeyFlipHorizontal, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.Editor.FlipHorzCommand.Execute(GetPathAtPointer());
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

            if (IsMatch(_settings.KeyRenameFile, e.Key, isCtrl, isShift, isAlt))
            {
                WeakReferenceMessenger.Default.Send(new RenameFileMessage(GetPathAtPointer()));
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyMoveFile, e.Key, isCtrl, isShift, isAlt))
            {
                WeakReferenceMessenger.Default.Send(new MoveFileMessage(GetPathAtPointer()));
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
                _window.ViewModel.Viewer.ToggleMetadataCommand.Execute(null);
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
                _window.ViewModel.Viewer.ToggleFullscreenCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyToggleStretchMode, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.Viewer.ToggleStretchModeCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyToggleManga, e.Key, isCtrl, isShift, isAlt))
            {
                _window.ViewModel.Viewer.ToggleMangaModeCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyToggleGrid, e.Key, isCtrl, isShift, isAlt))
            {
                // ItemClick in GridView marks Enter as Handled and sets IsGridMode = false.
                // If we don't check e.Handled, we end up toggling it right back to true.
                if (e.Handled && e.Key == VirtualKey.Enter)
                {
                    return;
                }

                // If we are still in grid mode, let the GridView handle the Enter key natively.
                if (_window.IsGridMode && e.Key == VirtualKey.Enter)
                {
                    return;
                }

                _window.ViewModel.Viewer.ToggleGridModeCommand.Execute(null);
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
                if (IsMatch(_settings.KeyPrevImage, e.Key, isCtrl, isShift, isAlt))
                {
                    // In grid mode, we usually want to move item by item
                    WeakReferenceMessenger.Default.Send(new NavigationMessage(-1, true));
                    e.Handled = true;
                    return;
                }
                if (IsMatch(_settings.KeyNextImage, e.Key, isCtrl, isShift, isAlt))
                {
                    WeakReferenceMessenger.Default.Send(new NavigationMessage(1, true));
                    e.Handled = true;
                    return;
                }

                // If navigation key bubbled up to RootGrid, focus might have been lost.
                // Restore focus to grid so native navigation works.
                if (e.Key == VirtualKey.Up || e.Key == VirtualKey.Down ||
                    e.Key == VirtualKey.Left || e.Key == VirtualKey.Right ||
                    e.Key == VirtualKey.PageUp || e.Key == VirtualKey.PageDown ||
                    e.Key == VirtualKey.Home || e.Key == VirtualKey.End)
                {
                    WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
                }

                return; // Keyboard navigation inside grid is handled by GridImagePanel
            }

            if (e.Key == Windows.System.VirtualKey.Left)
            {
                WeakReferenceMessenger.Default.Send(new NavigationMessage(_settings.MangaSplitCount > 1 ? 1 : -1, isShift));
                e.Handled = true;
                return;
            }
            if (e.Key == Windows.System.VirtualKey.Right)
            {
                WeakReferenceMessenger.Default.Send(new NavigationMessage(_settings.MangaSplitCount > 1 ? -1 : 1, isShift));
                e.Handled = true;
                return;
            }

            if (IsMatch(_settings.KeyPrevImage, e.Key, isCtrl, isShift, isAlt))
            {
                WeakReferenceMessenger.Default.Send(new NavigationMessage(-1, isShift));
                e.Handled = true;
            }
            else if (IsMatch(_settings.KeyNextImage, e.Key, isCtrl, isShift, isAlt))
            {
                WeakReferenceMessenger.Default.Send(new NavigationMessage(1, isShift));
                e.Handled = true;
            }
            else if (IsMatch(_settings.KeyPrevFolder, e.Key, isCtrl, isShift, isAlt))
            {
                WeakReferenceMessenger.Default.Send(new FolderNavigationMessage(-1));
                e.Handled = true;
            }
            else if (IsMatch(_settings.KeyNextFolder, e.Key, isCtrl, isShift, isAlt))
            {
                WeakReferenceMessenger.Default.Send(new FolderNavigationMessage(1));
                e.Handled = true;
            }
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
                return; // Scroll events inside grid are handled by GridImagePanel
            }

            // Navigate images (Single image display mode)
            if (props.MouseWheelDelta < 0)
            {
                WeakReferenceMessenger.Default.Send(new NavigationMessage(1, isShift)); // Next
            }
            else
            {
                WeakReferenceMessenger.Default.Send(new NavigationMessage(-1, isShift)); // Prev
            }
            e.Handled = true;
        }

        public void HandleDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (_window.IsDialogOpen) return;
            _window.ViewModel.Viewer.ToggleFullscreenCommand.Execute(null);
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
                        WeakReferenceMessenger.Default.Send(new LoadDirectoryMessage(folder.Path, string.Empty));
                    }
                    else if (item is StorageFile file)
                    {
                        string ext = Path.GetExtension(file.Path).ToLowerInvariant();
                        bool isSupported = FolderDiscoveryService.IsSupportedExtension(ext, _settings.EnabledExtensions);
                        bool isArchive = ArchiveManager.IsArchive(file.Path);

                        if (isSupported)
                        {
                            string dir = Path.GetDirectoryName(file.Path) ?? string.Empty;
                            WeakReferenceMessenger.Default.Send(new LoadDirectoryMessage(dir, file.Path));
                        }
                        else if (isArchive)
                        {
                            WeakReferenceMessenger.Default.Send(new LoadDirectoryMessage(file.Path));
                        }
                        else
                        {
                            _window.ShowNotification(_settings.GetString("Notification_NotAnImage"));
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
