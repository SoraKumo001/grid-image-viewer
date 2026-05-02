using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using Windows.Storage;
using Windows.System;

namespace grid_image_viewer
{
    public class EditorManager
    {
        private readonly MainWindow _window;
        private readonly SettingsManager _settings;

        // Selection State
        private Windows.Foundation.Point _selectionStart;
        private bool _isSelecting = false;
        private bool _hasSelection = false;

        public bool HasSelection => _hasSelection;

        public EditorManager(MainWindow window, SettingsManager settings)
        {
            _window = window;
            _settings = settings;
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
            _window.MenuCrop.IsEnabled = _hasSelection;
            if (_window.MenuToggleManga != null)
            {
                _window.MenuToggleManga.Text = _settings.MangaSplitCount == 1 ? "View Mode: Single" : _settings.MangaSplitCount == 2 ? "View Mode: Double" : "View Mode: Quad";
            }
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

                await Task.Run(() => ImageProcessor.SaveImage(sourcePath, destPath, targetExtension));
                
                if (overwrite) _ = _window.UpdateDisplayAsync();
            }
            catch { }
        }

        public void MenuCrop_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasSelection) return;
            string sourcePath = _window.CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;

            try
            {
                var (imgW, imgH) = ImageProcessor.GetImageSize(sourcePath);
                if (imgW == 0 || imgH == 0) return;

                FrameworkElement targetElement = _window.Image1.Visibility == Visibility.Visible ? _window.Image1 : _window.Canvas1;
                    
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

                _window.ViewerManager.StopAnimation();
                foreach (var img in _window.ViewerManager.PageImages) img.Source = null;

                ImageProcessor.CropImage(sourcePath, cropRect);

                _window.SelectionRectangle.Visibility = Visibility.Collapsed;
                _hasSelection = false;
                _ = _window.UpdateDisplayAsync();
            }
            catch { }
        }

        public async void MenuResize_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = _window.CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;

            var dialog = new ContentDialog
            {
                Title = "Resize Image",
                PrimaryButtonText = "Resize",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = _window.Content.XamlRoot
            };

            var stackPanel = new StackPanel { Spacing = 10 };
            var widthBox = new NumberBox { Header = "Width" };
            var heightBox = new NumberBox { Header = "Height" };
            stackPanel.Children.Add(widthBox);
            stackPanel.Children.Add(heightBox);
            dialog.Content = stackPanel;

            try
            {
                var (origW, origH) = ImageProcessor.GetImageSize(sourcePath);
                widthBox.Value = origW;
                heightBox.Value = origH;

                _window.IsDialogOpen = true;
                var result = await dialog.ShowAsync();
                _window.IsDialogOpen = false;

                if (result == ContentDialogResult.Primary)
                {
                    int newWidth = (int)widthBox.Value;
                    int newHeight = (int)heightBox.Value;
                    if (newWidth <= 0 || newHeight <= 0) return;

                    _window.ViewerManager.StopAnimation();
                    foreach (var img in _window.ViewerManager.PageImages) img.Source = null;

                    await Task.Run(() => ImageProcessor.ResizeImage(sourcePath, newWidth, newHeight));
                    _ = _window.UpdateDisplayAsync();
                }
            }
            catch { }
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

        public void MenuToggleManga_Click(object sender, RoutedEventArgs e)
        {
            _settings.MangaSplitCount = _settings.MangaSplitCount == 1 ? 2 : (_settings.MangaSplitCount == 2 ? 4 : 1);
            _settings.SaveMangaMode();
            _ = _window.UpdateDisplayAsync();
        }

        public void MenuLayoutMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int mode))
            {
                _settings.QuadLayoutMode = mode;
                _settings.SaveMangaMode();
                if (_settings.MangaSplitCount == 4)
                {
                    _ = _window.UpdateDisplayAsync();
                }
            }
        }

        private TextBox CreateKeyBindingTextBox(string header, VirtualKey currentKey, Action<VirtualKey> updateAction)
        {
            var tb = new TextBox { Header = header, Text = currentKey.ToString(), IsReadOnly = true };
            tb.PreviewKeyDown += (s, e) =>
            {
                var key = e.Key;
                if (key != VirtualKey.Control && key != VirtualKey.Shift && key != VirtualKey.Menu)
                {
                    tb.Text = key.ToString();
                    updateAction(key);
                }
                e.Handled = true;
            };
            return tb;
        }

        public async void MenuKeyBindings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Key Bindings Settings",
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = _window.Content.XamlRoot
            };

            var stackPanel = new StackPanel { Spacing = 10 };
            
            var tempNextImage = _settings.KeyNextImage;
            var tempPrevImage = _settings.KeyPrevImage;
            var tempNextFolder = _settings.KeyNextFolder;
            var tempPrevFolder = _settings.KeyPrevFolder;
            var tempToggleManga = _settings.KeyToggleManga;
            var tempExit = _settings.KeyExit;
            var tempToggleGrid = _settings.KeyToggleGrid;
            var tempSlideshow = _settings.KeySlideshow;

            stackPanel.Children.Add(CreateKeyBindingTextBox("Next Image", tempNextImage, k => tempNextImage = k));
            stackPanel.Children.Add(CreateKeyBindingTextBox("Previous Image", tempPrevImage, k => tempPrevImage = k));
            stackPanel.Children.Add(CreateKeyBindingTextBox("Next Folder", tempNextFolder, k => tempNextFolder = k));
            stackPanel.Children.Add(CreateKeyBindingTextBox("Previous Folder", tempPrevFolder, k => tempPrevFolder = k));
            stackPanel.Children.Add(CreateKeyBindingTextBox("Toggle Manga Mode (Requires Ctrl)", tempToggleManga, k => tempToggleManga = k));
            stackPanel.Children.Add(CreateKeyBindingTextBox("Toggle Grid Mode", tempToggleGrid, k => tempToggleGrid = k));
            stackPanel.Children.Add(CreateKeyBindingTextBox("Toggle Slideshow", tempSlideshow, k => tempSlideshow = k));
            stackPanel.Children.Add(CreateKeyBindingTextBox("Exit App", tempExit, k => tempExit = k));

            dialog.Content = stackPanel;

            _window.IsDialogOpen = true;
            var resultKb = await dialog.ShowAsync();
            _window.IsDialogOpen = false;

            if (resultKb == ContentDialogResult.Primary)
            {
                _settings.KeyNextImage = tempNextImage;
                _settings.KeyPrevImage = tempPrevImage;
                _settings.KeyNextFolder = tempNextFolder;
                _settings.KeyPrevFolder = tempPrevFolder;
                _settings.KeyToggleManga = tempToggleManga;
                _settings.KeyExit = tempExit;
                _settings.KeyToggleGrid = tempToggleGrid;
                _settings.KeySlideshow = tempSlideshow;
                _settings.SaveKeyBindings();
            }
        }
    }
}
