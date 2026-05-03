using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
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
            try
            {
                _window.MenuCrop.IsEnabled = _hasSelection;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Menu Opening error: {ex.Message}");
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

                await Task.Run(() =>
                {
                    int quality = _settings.JpegQuality;
                    if (_window.ViewerManager.PendingEdits.TryGetValue(sourcePath, out var pendingBmp))
                    {
                        ImageProcessor.SaveBitmap(pendingBmp, destPath, targetExtension, quality);
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

        public void MenuCrop_Click(object sender, RoutedEventArgs e)
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
                if (_window.ViewerManager.PendingEdits.TryGetValue(sourcePath, out var pending))
                {
                    imgW = pending.Width;
                    imgH = pending.Height;
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

                _window.ViewerManager.StopAnimation();
                foreach (var img in _window.ViewerManager.PageImages) img.Source = null;

                // Clear EditedBitmap references in PageRenderers BEFORE disposing the old bitmap.
                // This prevents LoadPageAsync's crossfade prep from accessing a disposed bitmap.
                for (int pi = 0; pi < _window.ViewerManager.Pages.Length; pi++)
                {
                    if (_window.ViewerManager.Pages[pi].CurrentFilePath == sourcePath)
                        _window.ViewerManager.Pages[pi].EditedBitmap = null;
                }

                var newBmp = ImageProcessor.GetCroppedBitmap(sourcePath, cropRect, _window.ViewerManager.PendingEdits.TryGetValue(sourcePath, out var pb) ? pb : null);
                if (newBmp != null)
                {
                    if (_window.ViewerManager.PendingEdits.TryGetValue(sourcePath, out var oldPb)) oldPb.Dispose();
                    _window.ViewerManager.PendingEdits[sourcePath] = newBmp;
                }

                _window.SelectionRectangle.Visibility = Visibility.Collapsed;
                _hasSelection = false;
                _ = _window.UpdateDisplayAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Crop error: {ex.Message}");
                _window.ViewerManager.ShowNotification($"Crop exception: {ex.Message}");
            }
        }

        public void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            var resLoader = ResourceLoader.GetForViewIndependentUse();
            string titleStr = resLoader.GetString("Settings_Title/Text") ?? "Settings";
            string qualityStr = resLoader.GetString("Settings_JpegQuality/Text") ?? "JPEG/WebP Quality";
            string closeStr = resLoader.GetString("ToneAdjustment_Close/Content") ?? "Close";

            var existing = _window.RootGrid.Children.FirstOrDefault(c => c is Grid g && g.Name == "SettingsOverlay");
            if (existing != null) _window.RootGrid.Children.Remove(existing);

            var sliderQuality = new Slider { Minimum = 1, Maximum = 100, Value = _settings.JpegQuality, StepFrequency = 1, Width = 200 };
            var textQuality = new TextBlock { Text = _settings.JpegQuality.ToString(), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Width = 40, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) };
            sliderQuality.ValueChanged += (s, ev) => { textQuality.Text = sliderQuality.Value.ToString("F0"); };

            var overlay = new Grid { Name = "SettingsOverlay", Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0)), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            Grid.SetRowSpan(overlay, 10);

            var panel = new StackPanel
            {
                Spacing = 16,
                Padding = new Thickness(24),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32)),
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 64, 64, 64)),
                BorderThickness = new Thickness(1),
                Width = 320,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RequestedTheme = ElementTheme.Dark
            };

            panel.Children.Add(new TextBlock { Text = titleStr, FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8), Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) });

            var rowQ = new StackPanel { Spacing = 4 };
            rowQ.Children.Add(new TextBlock { Text = qualityStr, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) });
            var gridQ = new Grid(); gridQ.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); gridQ.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            gridQ.Children.Add(sliderQuality); Grid.SetColumn(textQuality, 1); gridQ.Children.Add(textQuality);
            rowQ.Children.Add(gridQ); panel.Children.Add(rowQ);

            var btnClose = new Button { Content = closeStr, Width = 80, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0), Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
            btnClose.Click += (s, ev) =>
            {
                _settings.JpegQuality = (int)sliderQuality.Value;
                _settings.SaveSettings();
                _window.RootGrid.Children.Remove(overlay);
            };
            panel.Children.Add(btnClose);
            overlay.Children.Add(panel);
            _window.RootGrid.Children.Add(overlay);
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
                int origW, origH;
                if (_window.ViewerManager.PendingEdits.TryGetValue(sourcePath, out var pending))
                {
                    origW = pending.Width;
                    origH = pending.Height;
                }
                else
                {
                    (origW, origH) = ImageProcessor.GetImageSize(sourcePath);
                }
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

                    // Clear EditedBitmap references before disposing old bitmap
                    for (int pi = 0; pi < _window.ViewerManager.Pages.Length; pi++)
                    {
                        if (_window.ViewerManager.Pages[pi].CurrentFilePath == sourcePath)
                            _window.ViewerManager.Pages[pi].EditedBitmap = null;
                    }

                    await Task.Run(() =>
                    {
                        var newBmp = ImageProcessor.GetResizedBitmap(sourcePath, newWidth, newHeight, _window.ViewerManager.PendingEdits.TryGetValue(sourcePath, out var pb) ? pb : null);
                        if (newBmp != null)
                        {
                            if (_window.ViewerManager.PendingEdits.TryGetValue(sourcePath, out var oldPb)) oldPb.Dispose();
                            _window.ViewerManager.PendingEdits[sourcePath] = newBmp;
                        }
                    });
                    _ = _window.UpdateDisplayAsync();
                }
            }
            catch { }
        }

        public void MenuTone_Click(object sender, RoutedEventArgs e)
        {
            if (_window.ViewerManager == null) return;

            string sourcePath = _window.CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;

            // Capture base bitmap for non-cumulative adjustment
            SKBitmap? baseBmp = null;
            if (_window.ViewerManager.PendingEdits.TryGetValue(sourcePath, out var currentPb))
            {
                baseBmp = currentPb.Copy();
            }
            else
            {
                try { baseBmp = SKBitmap.Decode(sourcePath); } catch { }
            }

            // Remove any existing tone overlay
            var existing = _window.RootGrid.Children.FirstOrDefault(c => c is Grid g && g.Name == "ToneAdjustmentOverlay");
            if (existing != null) _window.RootGrid.Children.Remove(existing);

            // Create controls programmatically to avoid XAML compiler bugs and forced dimming
            var sliderBrightness = new Slider { Minimum = -255, Maximum = 255, Value = 0, StepFrequency = 1, Width = 200 };
            var textBrightness = new TextBlock { Text = "0", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Width = 40 };
            var sliderContrast = new Slider { Minimum = 0.0, Maximum = 2.0, Value = 1.0, StepFrequency = 0.01, Width = 200 };
            var textContrast = new TextBlock { Text = "1.00", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Width = 40 };
            var sliderSaturation = new Slider { Minimum = 0.0, Maximum = 2.0, Value = 1.0, StepFrequency = 0.01, Width = 200 };
            var textSaturation = new TextBlock { Text = "1.00", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Width = 40 };

            DispatcherTimer updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            updateTimer.Tick += async (s, ev) =>
            {
                updateTimer.Stop();
                float b = (float)sliderBrightness.Value;
                float c = (float)sliderContrast.Value;
                float sVal = (float)sliderSaturation.Value;
                await Task.Run(() =>
                {
                    try
                    {
                        var newBmp = ImageProcessor.ApplyToneAdjustment(sourcePath, b, c, sVal, baseBmp);
                        if (newBmp != null)
                        {
                            _window.DispatcherQueue.TryEnqueue(() =>
                            {
                                if (_window.ViewerManager == null) return;
                                if (_window.ViewerManager.PendingEdits.TryGetValue(sourcePath, out var oldPb)) oldPb.Dispose();
                                _window.ViewerManager.PendingEdits[sourcePath] = newBmp;
                                _window.ViewerManager.StopAnimation();
                                foreach (var img in _window.ViewerManager.PageImages) img.Source = null;
                                for (int pi = 0; pi < _window.ViewerManager.Pages.Length; pi++)
                                {
                                    if (_window.ViewerManager.Pages[pi].CurrentFilePath == sourcePath)
                                        _window.ViewerManager.Pages[pi].EditedBitmap = null;
                                }
                                _ = _window.UpdateDisplayAsync();
                            });
                        }
                    }
                    catch { }
                });
            };

            void OnValueChanged(object s, RangeBaseValueChangedEventArgs ev)
            {
                textBrightness.Text = sliderBrightness.Value.ToString("F0");
                textContrast.Text = sliderContrast.Value.ToString("F2");
                textSaturation.Text = sliderSaturation.Value.ToString("F2");
                updateTimer.Stop(); updateTimer.Start();
            }
            sliderBrightness.ValueChanged += OnValueChanged;
            sliderContrast.ValueChanged += OnValueChanged;
            sliderSaturation.ValueChanged += OnValueChanged;

            // Localization
            var resLoader = ResourceLoader.GetForViewIndependentUse();
            string titleStr = resLoader.GetString("ToneAdjustment_Title/Text");
            string brightnessStr = resLoader.GetString("ToneAdjustment_Brightness/Text");
            string contrastStr = resLoader.GetString("ToneAdjustment_Contrast/Text");
            string saturationStr = resLoader.GetString("ToneAdjustment_Saturation/Text");
            string resetStr = resLoader.GetString("ToneAdjustment_Reset/Content");
            string closeStr = resLoader.GetString("ToneAdjustment_Close/Content");

            // Fallbacks if resources are missing
            if (string.IsNullOrEmpty(titleStr)) titleStr = "Tone Adjustment";
            if (string.IsNullOrEmpty(brightnessStr)) brightnessStr = "Brightness";
            if (string.IsNullOrEmpty(contrastStr)) contrastStr = "Contrast";
            if (string.IsNullOrEmpty(saturationStr)) saturationStr = "Saturation";
            if (string.IsNullOrEmpty(resetStr)) resetStr = "Reset";
            if (string.IsNullOrEmpty(closeStr)) closeStr = "Close";

            var overlay = new Grid
            {
                Name = "ToneAdjustmentOverlay",
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetRowSpan(overlay, 10);

            var panel = new StackPanel
            {
                Spacing = 16,
                Padding = new Thickness(24),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32)),
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 64, 64, 64)),
                BorderThickness = new Thickness(1),
                Width = 320,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RequestedTheme = ElementTheme.Dark
            };

            panel.Children.Add(new TextBlock { Text = titleStr, FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8), Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) });

            var rowB = new StackPanel { Spacing = 4 };
            rowB.Children.Add(new TextBlock { Text = brightnessStr, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) });
            var gridB = new Grid(); gridB.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); gridB.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            gridB.Children.Add(sliderBrightness); Grid.SetColumn(textBrightness, 1); gridB.Children.Add(textBrightness);
            textBrightness.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
            rowB.Children.Add(gridB); panel.Children.Add(rowB);

            var rowC = new StackPanel { Spacing = 4 };
            rowC.Children.Add(new TextBlock { Text = contrastStr, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) });
            var gridC = new Grid(); gridC.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); gridC.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            gridC.Children.Add(sliderContrast); Grid.SetColumn(textContrast, 1); gridC.Children.Add(textContrast);
            textContrast.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
            rowC.Children.Add(gridC); panel.Children.Add(rowC);

            var rowS = new StackPanel { Spacing = 4 };
            rowS.Children.Add(new TextBlock { Text = saturationStr, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) });
            var gridS = new Grid(); gridS.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); gridS.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            gridS.Children.Add(sliderSaturation); Grid.SetColumn(textSaturation, 1); gridS.Children.Add(textSaturation);
            textSaturation.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
            rowS.Children.Add(gridS); panel.Children.Add(rowS);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 16, 0, 0) };
            var btnReset = new Button { Content = resetStr, Width = 80 };
            var btnClose = new Button { Content = closeStr, Width = 80, Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
            buttonPanel.Children.Add(btnReset); buttonPanel.Children.Add(btnClose);
            panel.Children.Add(buttonPanel);

            overlay.Children.Add(panel);
            btnReset.Click += (s, ev) => { sliderBrightness.Value = 0; sliderContrast.Value = 1.0; sliderSaturation.Value = 1.0; };
            btnClose.Click += (s, ev) =>
            {
                _window.RootGrid.Children.Remove(overlay);
                baseBmp?.Dispose();
            };

            _window.RootGrid.Children.Add(overlay);
        }

        public async void MenuFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string filterType)
            {
                string sourcePath = _window.CurrentImagePath;
                if (string.IsNullOrEmpty(sourcePath)) return;

                _window.ViewerManager.StopAnimation();
                foreach (var img in _window.ViewerManager.PageImages) img.Source = null;

                for (int pi = 0; pi < _window.ViewerManager.Pages.Length; pi++)
                {
                    if (_window.ViewerManager.Pages[pi].CurrentFilePath == sourcePath)
                        _window.ViewerManager.Pages[pi].EditedBitmap = null;
                }

                await Task.Run(() =>
                {
                    var newBmp = ImageProcessor.ApplyFilter(sourcePath, filterType, _window.ViewerManager.PendingEdits.TryGetValue(sourcePath, out var pb) ? pb : null);
                    if (newBmp != null)
                    {
                        if (_window.ViewerManager.PendingEdits.TryGetValue(sourcePath, out var oldPb)) oldPb.Dispose();
                        _window.ViewerManager.PendingEdits[sourcePath] = newBmp;
                    }
                });
                _ = _window.UpdateDisplayAsync();
            }
        }

        public async void MenuRotate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string tagStr && float.TryParse(tagStr, out float degrees))
            {
                string sourcePath = _window.CurrentImagePath;
                if (string.IsNullOrEmpty(sourcePath)) return;

                _window.ViewerManager.StopAnimation();
                foreach (var img in _window.ViewerManager.PageImages) img.Source = null;

                for (int pi = 0; pi < _window.ViewerManager.Pages.Length; pi++)
                {
                    if (_window.ViewerManager.Pages[pi].CurrentFilePath == sourcePath)
                        _window.ViewerManager.Pages[pi].EditedBitmap = null;
                }

                await Task.Run(() =>
                {
                    var newBmp = ImageProcessor.GetRotatedBitmap(sourcePath, degrees, _window.ViewerManager.PendingEdits.TryGetValue(sourcePath, out var pb) ? pb : null);
                    if (newBmp != null)
                    {
                        if (_window.ViewerManager.PendingEdits.TryGetValue(sourcePath, out var oldPb)) oldPb.Dispose();
                        _window.ViewerManager.PendingEdits[sourcePath] = newBmp;
                    }
                });
                _ = _window.UpdateDisplayAsync();
            }
        }

        public async void MenuFlip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string flipMode)
            {
                string sourcePath = _window.CurrentImagePath;
                if (string.IsNullOrEmpty(sourcePath)) return;

                bool horizontal = flipMode == "Horz";

                _window.ViewerManager.StopAnimation();
                foreach (var img in _window.ViewerManager.PageImages) img.Source = null;

                for (int pi = 0; pi < _window.ViewerManager.Pages.Length; pi++)
                {
                    if (_window.ViewerManager.Pages[pi].CurrentFilePath == sourcePath)
                        _window.ViewerManager.Pages[pi].EditedBitmap = null;
                }

                await Task.Run(() =>
                {
                    var newBmp = ImageProcessor.GetFlippedBitmap(sourcePath, horizontal, _window.ViewerManager.PendingEdits.TryGetValue(sourcePath, out var pb) ? pb : null);
                    if (newBmp != null)
                    {
                        if (_window.ViewerManager.PendingEdits.TryGetValue(sourcePath, out var oldPb)) oldPb.Dispose();
                        _window.ViewerManager.PendingEdits[sourcePath] = newBmp;
                    }
                });
                _ = _window.UpdateDisplayAsync();
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
            if (sender is MenuFlyoutItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int count))
            {
                _settings.MangaSplitCount = count;
                _settings.SaveMangaMode();
                _ = _window.UpdateDisplayAsync();
            }
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

        private UIElement CreateKeyBindingRow(string header, KeyBindingData binding)
        {
            var stack = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 16, 0) };
            stack.Children.Add(new TextBlock { Text = header, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 170, 170, 170)), Margin = new Thickness(0, 8, 0, 2) });

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Key
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Ctrl
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Shift
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Alt

            var tb = new TextBox { Text = binding.Key.ToString(), IsReadOnly = true, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 12, 0), Background = new SolidColorBrush(Windows.UI.Color.FromArgb(25, 255, 255, 255)) };
            tb.PreviewKeyDown += (s, e) =>
            {
                var key = e.Key;
                if (key != VirtualKey.Control && key != VirtualKey.Shift && key != VirtualKey.Menu)
                {
                    tb.Text = key.ToString();
                    binding.Key = key;
                }
                e.Handled = true;
            };

            var cbCtrl = new CheckBox { Content = "Ctrl", IsChecked = binding.Ctrl, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            cbCtrl.Checked += (s, e) => binding.Ctrl = true;
            cbCtrl.Unchecked += (s, e) => binding.Ctrl = false;

            var cbShift = new CheckBox { Content = "Shift", IsChecked = binding.Shift, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            cbShift.Checked += (s, e) => binding.Shift = true;
            cbShift.Unchecked += (s, e) => binding.Shift = false;

            var cbAlt = new CheckBox { Content = "Alt", IsChecked = binding.Alt, VerticalAlignment = VerticalAlignment.Center };
            cbAlt.Checked += (s, e) => binding.Alt = true;
            cbAlt.Unchecked += (s, e) => binding.Alt = false;

            Grid.SetColumn(tb, 0);
            Grid.SetColumn(cbCtrl, 1);
            Grid.SetColumn(cbShift, 2);
            Grid.SetColumn(cbAlt, 3);

            grid.Children.Add(tb);
            grid.Children.Add(cbCtrl);
            grid.Children.Add(cbShift);
            grid.Children.Add(cbAlt);

            stack.Children.Add(grid);
            return stack;
        }

        public async void MenuKeyBindings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = GetString("KeyBinding_Title"),
                PrimaryButtonText = GetString("KeyBinding_Save"),
                CloseButtonText = GetString("KeyBinding_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = _window.Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark
            };

            var stackPanel = new StackPanel { Spacing = 10, Padding = new Thickness(0, 0, 0, 20), MinWidth = 480 };

            var tempNextImage = _settings.KeyNextImage.Clone();
            var tempPrevImage = _settings.KeyPrevImage.Clone();
            var tempNextFolder = _settings.KeyNextFolder.Clone();
            var tempPrevFolder = _settings.KeyPrevFolder.Clone();
            var tempToggleManga = _settings.KeyToggleManga.Clone();
            var tempExit = _settings.KeyExit.Clone();
            var tempToggleGrid = _settings.KeyToggleGrid.Clone();
            var tempSlideshow = _settings.KeySlideshow.Clone();

            stackPanel.Children.Add(CreateKeyBindingRow(GetString("KeyBinding_NextImage"), tempNextImage));
            stackPanel.Children.Add(CreateKeyBindingRow(GetString("KeyBinding_PrevImage"), tempPrevImage));
            stackPanel.Children.Add(CreateKeyBindingRow(GetString("KeyBinding_NextFolder"), tempNextFolder));
            stackPanel.Children.Add(CreateKeyBindingRow(GetString("KeyBinding_PrevFolder"), tempPrevFolder));
            stackPanel.Children.Add(CreateKeyBindingRow(GetString("KeyBinding_ToggleManga"), tempToggleManga));
            stackPanel.Children.Add(CreateKeyBindingRow(GetString("KeyBinding_ToggleGrid"), tempToggleGrid));
            stackPanel.Children.Add(CreateKeyBindingRow(GetString("KeyBinding_ToggleSlideshow"), tempSlideshow));
            stackPanel.Children.Add(CreateKeyBindingRow(GetString("KeyBinding_Exit"), tempExit));

            dialog.Content = new ScrollViewer
            {
                Content = stackPanel,
                MaxHeight = 500,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

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
        private string GetString(string key)
        {
            try
            {
                // Dots are used for properties in x:Uid, but for simple strings they might be just names.
                // ResourceManager uses / as separator.
                var resourceKey = "Resources/" + key.Replace(".", "/");
                var candidate = _resourceManager.MainResourceMap.GetValue(resourceKey, _resourceContext);
                return candidate?.ValueAsString ?? key;
            }
            catch { return key; }
        }
    }
}
