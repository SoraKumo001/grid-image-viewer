using grid_image_viewer.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;

namespace grid_image_viewer
{
    public class EditorManager
    {
        private readonly MainWindow _window;
        private readonly SettingsManager _settings;
        private Microsoft.Windows.ApplicationModel.Resources.ResourceManager _resourceManager;
        private Microsoft.Windows.ApplicationModel.Resources.ResourceContext _resourceContext;
        private readonly Dictionary<string, string> _stringCache = new Dictionary<string, string>();

        // Selection State
        private Windows.Foundation.Point _selectionStart;
        private bool _isSelecting = false;
        private bool _hasSelection = false;

        public bool HasSelection => _hasSelection;

        public EditorManager(MainWindow window, SettingsManager settings)
        {
            _window = window;
            _settings = settings;
            _resourceManager = new Microsoft.Windows.ApplicationModel.Resources.ResourceManager();
            _resourceContext = _resourceManager.CreateResourceContext();
            PreloadStrings();
        }

        private void PreloadStrings()
        {
            // Currently no dynamic strings need preloading as we moved to sub-menus and x:Uid
        }

        public void PagesGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(_window.PagesGrid);
            if (point.Properties.IsLeftButtonPressed)
            {
                _isSelecting = true;
                _hasSelection = false;
                _selectionStart = point.Position;
                _window.SelectionRectangle.Width = 0;
                _window.SelectionRectangle.Height = 0;
                Canvas.SetLeft(_window.SelectionRectangle, _selectionStart.X);
                Canvas.SetTop(_window.SelectionRectangle, _selectionStart.Y);
                _window.SelectionRectangle.Visibility = Visibility.Visible;
                _window.PagesGrid.CapturePointer(e.Pointer);
                e.Handled = true;
            }
        }

        public void PagesGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isSelecting)
            {
                var point = e.GetCurrentPoint(_window.PagesGrid);
                double x = Math.Min(point.Position.X, _selectionStart.X);
                double y = Math.Min(point.Position.Y, _selectionStart.Y);
                double width = Math.Abs(point.Position.X - _selectionStart.X);
                double height = Math.Abs(point.Position.Y - _selectionStart.Y);

                Canvas.SetLeft(_window.SelectionRectangle, x);
                Canvas.SetTop(_window.SelectionRectangle, y);
                _window.SelectionRectangle.Width = width;
                _window.SelectionRectangle.Height = height;
                e.Handled = true;
            }

            _window.ViewerManager.HandlePointerMoved(e);
        }

        public void PagesGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isSelecting)
            {
                _isSelecting = false;
                _window.PagesGrid.ReleasePointerCapture(e.Pointer);
                if (_window.SelectionRectangle.Width > 5 && _window.SelectionRectangle.Height > 5)
                {
                    _hasSelection = true;
                }
                else
                {
                    _hasSelection = false;
                    _window.SelectionRectangle.Visibility = Visibility.Collapsed;
                }
                e.Handled = true;
            }
        }

        public void EditMenuFlyout_Opening(object sender, object e)
        {
            string path = _window.CurrentImagePath;
            bool hasPath = !string.IsNullOrEmpty(path);
            bool canUndo = false;
            bool canRedo = false;

            if (hasPath && _window.ViewerManager.PendingEdits.TryGetValue(path, out var session))
            {
                canUndo = session.CanUndo;
                canRedo = session.CanRedo;
            }

            _window.MenuUndo.IsEnabled = canUndo;
            _window.MenuRedo.IsEnabled = canRedo;
            _window.MenuSaveAs.IsEnabled = hasPath;
            _window.MenuOverwrite.IsEnabled = hasPath;
            _window.MenuCrop.IsEnabled = hasPath && HasSelection;
            _window.MenuResize.IsEnabled = hasPath;
            _window.MenuRotate.IsEnabled = hasPath;
            _window.MenuFlip.IsEnabled = hasPath;
            _window.MenuTone.IsEnabled = hasPath;
            _window.MenuFilter.IsEnabled = hasPath;

            // Update View Mode checked states
            int splitCount = _settings.MangaSplitCount;
            _window.MenuViewSingle.IsChecked = (splitCount == 1);
            _window.MenuViewDouble.IsChecked = (splitCount == 2);
            _window.MenuViewQuad.IsChecked = (splitCount == 4);

            // Update Quad Layout checked states
            int layoutMode = _settings.QuadLayoutMode;
            _window.MenuLayoutAuto.IsChecked = (layoutMode == 0);
            _window.MenuLayoutHorz.IsChecked = (layoutMode == 1);
            _window.MenuLayoutGrid.IsChecked = (layoutMode == 2);

            // Update Stretch Mode checked states
            int stretchMode = _settings.ImageStretchMode;
            _window.MenuStretchOriginal.IsChecked = (stretchMode == 0);
            _window.MenuStretchContain.IsChecked = (stretchMode == 2);
            _window.MenuStretchCover.IsChecked = (stretchMode == 3);

            // Update Metadata checked state
            _window.MenuMetadata.IsChecked = (_window.MetadataPanel.Visibility == Visibility.Visible);
        }

        public async void MenuSaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string ext)
            {
                await SaveImageAsync(ext, false);
            }
        }

        public async void MenuOverwrite_Click(object sender, RoutedEventArgs e)
        {
            await SaveImageAsync(Path.GetExtension(_window.CurrentImagePath), true);
        }

        public async Task SaveImageAsync(string targetExtension, bool overwrite)
        {
            string sourcePath = _window.CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;

            string destPath = overwrite ? sourcePath : Path.ChangeExtension(sourcePath, targetExtension);
            if (!overwrite)
            {
                var picker = new Windows.Storage.Pickers.FileSavePicker();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                picker.FileTypeChoices.Add(targetExtension.Trim('.').ToUpper(), new List<string>() { targetExtension });
                picker.SuggestedFileName = Path.GetFileNameWithoutExtension(sourcePath);

                var file = await picker.PickSaveFileAsync();
                if (file == null) return;
                destPath = file.Path;
            }

            try
            {
                if (overwrite)
                {
                    _window.ViewerManager.StopAnimation();
                    foreach (var img in _window.ViewerManager.PageImages) img.Source = null;
                }

                await Task.Run(() =>
                {
                    int quality = _settings.JpegQuality;
                    if (_window.ViewerManager.PendingEdits.TryGetValue(sourcePath, out var session) && session.Current != null)
                    {
                        ImageProcessor.SaveBitmap(session.Current, destPath, targetExtension, quality);
                    }
                    else
                    {
                        ImageProcessor.SaveImage(sourcePath, destPath, targetExtension, quality);
                    }
                });

                if (overwrite) _ = _window.UpdateDisplayAsync();
            }
            catch { }
        }

        public async void MenuCrop_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasSelection) return;

            try
            {
                // Find target grid based on selection center
                double cx = Canvas.GetLeft(_window.SelectionRectangle) + _window.SelectionRectangle.Width / 2;
                double cy = Canvas.GetTop(_window.SelectionRectangle) + _window.SelectionRectangle.Height / 2;
                var centerPoint = new Windows.Foundation.Point(cx, cy);

                int targetIdx = 0;
                var pageGrids = new Grid[] { _window.PageGrid1, _window.PageGrid2, _window.PageGrid3, _window.PageGrid4 };
                for (int i = 0; i < 4; i++)
                {
                    if (pageGrids[i].Visibility == Visibility.Visible)
                    {
                        var ttvGrid = _window.OverlayCanvas.TransformToVisual(pageGrids[i]);
                        var p = ttvGrid.TransformPoint(centerPoint);
                        if (p.X >= 0 && p.X <= pageGrids[i].ActualWidth && p.Y >= 0 && p.Y <= pageGrids[i].ActualHeight)
                        {
                            targetIdx = i;
                            break;
                        }
                    }
                }

                string? sourcePath = _window.ViewerManager.Pages[targetIdx].CurrentFilePath;
                if (string.IsNullOrEmpty(sourcePath))
                {
                    _window.ViewerManager.ShowNotification("Crop failed: sourcePath is null");
                    return;
                }

                int imgW, imgH;
                if (_window.ViewerManager.PendingEdits.TryGetValue(sourcePath, out var pending) && pending.Current != null)
                {
                    imgW = pending.Current.Width;
                    imgH = pending.Current.Height;
                }
                else
                {
                    (imgW, imgH) = ImageProcessor.GetImageSize(sourcePath);
                }
                if (imgW == 0 || imgH == 0)
                {
                    _window.ViewerManager.ShowNotification("Crop failed: Could not get image size (possibly unsupported format)");
                    return;
                }

                FrameworkElement targetElement = _window.ViewerManager.PageImages[targetIdx];

                // If image is collapsed, it must be Skia mode, use Canvas instead
                if (targetElement.Visibility != Visibility.Visible)
                {
                    targetElement = targetIdx == 0 ? _window.Canvas1 : (targetIdx == 1 ? _window.Canvas2 : (targetIdx == 2 ? _window.Canvas3 : _window.Canvas4));
                }

                double renderRatio = targetElement.ActualWidth / targetElement.ActualHeight;
                double imageRatio = (double)imgW / imgH;

                double imgDisplayWidth = targetElement.ActualWidth;
                double imgDisplayHeight = targetElement.ActualHeight;
                double offsetX = 0;
                double offsetY = 0;

                if (imageRatio > renderRatio)
                {
                    imgDisplayHeight = targetElement.ActualWidth / imageRatio;
                    offsetY = (targetElement.ActualHeight - imgDisplayHeight) / 2;
                }
                else
                {
                    imgDisplayWidth = targetElement.ActualHeight * imageRatio;
                    offsetX = (targetElement.ActualWidth - imgDisplayWidth) / 2;
                }

                var ttv = _window.SelectionRectangle.TransformToVisual(targetElement);
                var rectTopLeft = ttv.TransformPoint(new Windows.Foundation.Point(0, 0));
                var rectBottomRight = ttv.TransformPoint(new Windows.Foundation.Point(_window.SelectionRectangle.Width, _window.SelectionRectangle.Height));

                double cropX = (rectTopLeft.X - offsetX) * (imgW / imgDisplayWidth);
                double cropY = (rectTopLeft.Y - offsetY) * (imgH / imgDisplayHeight);
                double cropW = (rectBottomRight.X - rectTopLeft.X) * (imgW / imgDisplayWidth);
                double cropH = (rectBottomRight.Y - rectTopLeft.Y) * (imgH / imgDisplayHeight);

                cropX = Math.Max(0, Math.Min(cropX, imgW));
                cropY = Math.Max(0, Math.Min(cropY, imgH));
                cropW = Math.Max(1, Math.Min(cropW, imgW - cropX));
                cropH = Math.Max(1, Math.Min(cropH, imgH - cropY));

                var cropRect = new SKRectI((int)cropX, (int)cropY, (int)(cropX + cropW), (int)(cropY + cropH));

                _window.SelectionRectangle.Visibility = Visibility.Collapsed;
                _hasSelection = false;

                await _window.ImageEditService.ApplyTransformationAsync(sourcePath,
                    (current) => ImageProcessor.GetCroppedBitmap(sourcePath, cropRect, current));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Crop error: {ex.Message}");
                _window.ViewerManager.ShowNotification($"Crop exception: {ex.Message}");
            }
        }

        public void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            var existing = _window.RootGrid.Children.FirstOrDefault(c => c is FrameworkElement fe && fe.Name == "SettingsOverlay");
            if (existing != null) _window.RootGrid.Children.Remove(existing);

            var overlay = new SettingsOverlay(_window, _settings)
            {
                Name = "SettingsOverlay"
            };
            Grid.SetRowSpan(overlay, 2);

            _window.IsDialogOpen = true;
            _window.RootGrid.Children.Add(overlay);
        }

        public void MenuUndo_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = _window.CurrentImagePath;
            if (!string.IsNullOrEmpty(sourcePath))
                _window.ImageEditService.UndoEdit(sourcePath);
        }

        public void MenuRedo_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = _window.CurrentImagePath;
            if (!string.IsNullOrEmpty(sourcePath))
                _window.ImageEditService.RedoEdit(sourcePath);
        }

        public void MenuMetadata_Click(object sender, RoutedEventArgs e)
        {
            _window.ViewerManager.ToggleMetadataPanel(cycle: false);
        }

        public void MenuResize_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = _window.CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;

            int origW, origH;
            var currentBmp = _window.ImageEditService.GetCurrentBitmap(sourcePath);
            if (currentBmp != null)
            {
                origW = currentBmp.Width;
                origH = currentBmp.Height;
            }
            else
            {
                (origW, origH) = ImageProcessor.GetImageSize(sourcePath);
            }

            var existing = _window.RootGrid.Children.FirstOrDefault(c => c is FrameworkElement fe && fe.Name == "ResizeOverlay");
            if (existing != null) _window.RootGrid.Children.Remove(existing);

            var overlay = new ResizeOverlay(_window, sourcePath, origW, origH)
            {
                Name = "ResizeOverlay"
            };
            Grid.SetRowSpan(overlay, 2);

            _window.IsDialogOpen = true;
            _window.RootGrid.Children.Add(overlay);
        }

        public void MenuTone_Click(object sender, RoutedEventArgs e)
        {
            if (_window.ViewerManager == null) return;

            string sourcePath = _window.CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;

            // Capture base bitmap for non-cumulative adjustment
            SKBitmap? baseBmp = _window.ImageEditService.GetCurrentBitmap(sourcePath)?.Copy();
            if (baseBmp == null)
            {
                try { baseBmp = SKBitmap.Decode(sourcePath); } catch { }
            }

            var existing = _window.RootGrid.Children.FirstOrDefault(c => c is FrameworkElement fe && fe.Name == "ToneAdjustmentOverlay");
            if (existing != null) _window.RootGrid.Children.Remove(existing);

            var overlay = new ToneAdjustmentOverlay(_window, sourcePath, baseBmp)
            {
                Name = "ToneAdjustmentOverlay"
            };
            Grid.SetRowSpan(overlay, 2);

            _window.IsDialogOpen = true;
            _window.RootGrid.Children.Add(overlay);
        }

        public async void MenuFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string filterType)
            {
                string sourcePath = _window.CurrentImagePath;
                if (string.IsNullOrEmpty(sourcePath)) return;

                await _window.ImageEditService.FilterAsync(sourcePath, filterType);
            }
        }

        public async void MenuRotate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string tagStr && float.TryParse(tagStr, out float degrees))
            {
                string sourcePath = _window.CurrentImagePath;
                if (string.IsNullOrEmpty(sourcePath)) return;

                await _window.ImageEditService.RotateAsync(sourcePath, degrees);
            }
        }

        public async void MenuFlip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string flipMode)
            {
                string sourcePath = _window.CurrentImagePath;
                if (string.IsNullOrEmpty(sourcePath)) return;

                bool horizontal = flipMode == "Horz";
                await _window.ImageEditService.FlipAsync(sourcePath, horizontal);
            }
        }

        public async void MenuOpenExplorer_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = _window.CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath)) return;

            try
            {
                var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(sourcePath));
                var file = await StorageFile.GetFileFromPathAsync(sourcePath);
                var options = new Windows.System.FolderLauncherOptions();
                options.ItemsToSelect.Add(file);
                await Launcher.LaunchFolderAsync(folder, options);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open explorer: {ex.Message}");
            }
        }

        public void MenuViewMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int count))
            {
                _settings.MangaSplitCount = count;
                _settings.SaveMangaMode();

                // Ensure radio behavior
                _window.MenuViewSingle.IsChecked = (count == 1);
                _window.MenuViewDouble.IsChecked = (count == 2);
                _window.MenuViewQuad.IsChecked = (count == 4);

                _ = _window.UpdateDisplayAsync();
            }
        }

        public void MenuLayoutMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int mode))
            {
                _settings.QuadLayoutMode = mode;
                _settings.SaveMangaMode();

                // Ensure radio behavior
                _window.MenuLayoutAuto.IsChecked = (mode == 0);
                _window.MenuLayoutHorz.IsChecked = (mode == 1);
                _window.MenuLayoutGrid.IsChecked = (mode == 2);

                if (_settings.MangaSplitCount == 4)
                {
                    _ = _window.UpdateDisplayAsync();
                }
            }
        }

        public void MenuStretchMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int mode))
            {
                _settings.ImageStretchMode = mode;
                _settings.SaveSettings();

                // Ensure radio behavior
                _window.MenuStretchOriginal.IsChecked = (mode == 0);
                _window.MenuStretchContain.IsChecked = (mode == 2);
                _window.MenuStretchCover.IsChecked = (mode == 3);

                _window.ViewerManager.UpdateStretch();
            }
        }



        public void MenuKeyBindings_Click(object sender, RoutedEventArgs e)
        {
            var existing = _window.RootGrid.Children.FirstOrDefault(c => c is FrameworkElement fe && fe.Name == "KeyBindingsOverlay");
            if (existing != null) _window.RootGrid.Children.Remove(existing);

            var overlay = new KeyBindingsOverlay(_window, _settings)
            {
                Name = "KeyBindingsOverlay"
            };
            Grid.SetRowSpan(overlay, 2);

            _window.IsDialogOpen = true;
            _window.RootGrid.Children.Add(overlay);
        }
        internal string GetString(string key)
        {
            if (_stringCache.TryGetValue(key, out var cached)) return cached;
            try
            {
                var resourceKey = "Resources/" + key.Replace(".", "/");
                var candidate = _resourceManager.MainResourceMap.GetValue(resourceKey, _resourceContext);
                var val = candidate?.ValueAsString ?? key;
                _stringCache[key] = val;
                return val;
            }
            catch { return key; }
        }
    }
}
