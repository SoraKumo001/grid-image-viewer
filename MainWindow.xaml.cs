using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using System.Runtime.InteropServices;

namespace grid_image_viewer
{
    public class NaturalStringComparer : IComparer<string>
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int StrCmpLogicalW(string psz1, string psz2);

        public int Compare(string? x, string? y)
        {
            if (x == null || y == null) return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            return StrCmpLogicalW(x, y);
        }
    }

    public sealed partial class MainWindow : Window
    {
        private List<string> _playlist = new List<string>();
        private int _currentIndex = -1;
        private string _currentDirectory = string.Empty;

        // Manga Mode State
        private bool _isMangaMode = false;

        private SKData? _data1;
        private SKCodec? _codec1;
        private SKBitmap? _bitmap1;
        private int _currentFrame1 = 0;
        private int _frameCount1 = 0;

        private SKData? _data2;
        private SKCodec? _codec2;
        private SKBitmap? _bitmap2;
        private int _currentFrame2 = 0;
        private int _frameCount2 = 0;

        private DispatcherTimer _animationTimer;

        // Key Bindings
        private Windows.System.VirtualKey _keyNextImage = Windows.System.VirtualKey.PageDown;
        private Windows.System.VirtualKey _keyPrevImage = Windows.System.VirtualKey.PageUp;
        private Windows.System.VirtualKey _keyNextFolder = Windows.System.VirtualKey.Down;
        private Windows.System.VirtualKey _keyPrevFolder = Windows.System.VirtualKey.Up;
        private Windows.System.VirtualKey _keyToggleManga = Windows.System.VirtualKey.G;

        public MainWindow()
        {
            InitializeComponent();

            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("IsMangaMode", out object obj) && obj is bool isMangaMode)
            {
                _isMangaMode = isMangaMode;
            }
            LoadKeyBindings();
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            if (Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = appWindow.TitleBar;
                
                // Set active window colors
                titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 255, 255, 255);
                titleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(51, 255, 255, 255);
                
                // Set inactive window colors
                titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 128, 128, 128);
                titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            }

            _animationTimer = new DispatcherTimer();
            _animationTimer.Tick += AnimationTimer_Tick;
        }

        private void RootGrid_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void RootGrid_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    var firstItem = items[0];
                    string directory = string.Empty;
                    string targetFile = string.Empty;

                    if (firstItem is StorageFolder folder)
                    {
                        directory = folder.Path;
                    }
                    else if (firstItem is StorageFile file)
                    {
                        directory = Path.GetDirectoryName(file.Path);
                        targetFile = file.Path;
                    }

                    if (!string.IsNullOrEmpty(directory))
                    {
                        LoadDirectory(directory, targetFile);
                        RootGrid.Focus(FocusState.Programmatic);
                    }
                }
            }
        }

        public void LoadDirectory(string path, string initialFile = "")
        {
            _currentDirectory = path;
            try
            {
                var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
                _playlist = Directory.EnumerateFiles(path)
                                     .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                                     .OrderBy(f => f, new NaturalStringComparer())
                                     .ToList();

                if (_playlist.Count > 0)
                {
                    _currentIndex = string.IsNullOrEmpty(initialFile) ? 0 : Math.Max(0, _playlist.IndexOf(initialFile));
                    _ = UpdateDisplayAsync();
                }
            }
            catch (Exception)
            {
                // Ignore access errors
            }
        }

        private async Task UpdateDisplayAsync()
        {
            if (_playlist.Count == 0 || _currentIndex < 0 || _currentIndex >= _playlist.Count) return;

            StopAnimation();

            if (_isMangaMode)
            {
                LeftColumn.Width = new GridLength(1, GridUnitType.Star);
                RightColumn.Width = new GridLength(1, GridUnitType.Star);
                LeftPageGrid.Visibility = Visibility.Visible;
                RightImage.HorizontalAlignment = HorizontalAlignment.Left;
            }
            else
            {
                LeftColumn.Width = new GridLength(0);
                RightColumn.Width = new GridLength(1, GridUnitType.Star);
                LeftPageGrid.Visibility = Visibility.Collapsed;
                RightImage.HorizontalAlignment = HorizontalAlignment.Center;
            }

            // Load Right Page (Index)
            await LoadPageAsync(_playlist[_currentIndex], RightImage, RightSkiaCanvas, true);

            // Load Left Page (Index + 1)
            if (_isMangaMode && _currentIndex + 1 < _playlist.Count)
            {
                await LoadPageAsync(_playlist[_currentIndex + 1], LeftImage, LeftSkiaCanvas, false);
            }
            else
            {
                LeftImage.Source = null;
                _codec2?.Dispose(); _codec2 = null;
                _data2?.Dispose(); _data2 = null;
                _bitmap2?.Dispose(); _bitmap2 = null;
                LeftSkiaCanvas.Invalidate();
            }

            if ((_codec1 != null && _frameCount1 > 1) || (_codec2 != null && _frameCount2 > 1))
            {
                _animationTimer.Start();
            }
        }

        private async Task LoadPageAsync(string filePath, Image imageCtrl, SKXamlCanvas canvasCtrl, bool isRightPage)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            bool useSkia = ext == ".webp" || ext == ".gif";

            if (useSkia)
            {
                imageCtrl.Visibility = Visibility.Collapsed;
                canvasCtrl.Visibility = Visibility.Visible;

                try
                {
                    var data = SKData.Create(filePath);
                    if (data != null)
                    {
                        var codec = SKCodec.Create(data);
                        if (codec != null)
                        {
                            var bitmap = new SKBitmap(codec.Info);
                            int frameCount = codec.FrameCount;

                            if (isRightPage)
                            {
                                _data1 = data; _codec1 = codec; _bitmap1 = bitmap; _frameCount1 = frameCount; _currentFrame1 = 0;
                            }
                            else
                            {
                                _data2 = data; _codec2 = codec; _bitmap2 = bitmap; _frameCount2 = frameCount; _currentFrame2 = 0;
                            }

                            if (frameCount <= 1)
                            {
                                bitmap = SKBitmap.Decode(codec);
                                if (isRightPage) _bitmap1 = bitmap;
                                else _bitmap2 = bitmap;
                            }
                            canvasCtrl.Invalidate();
                        }
                    }
                }
                catch { }
            }
            else
            {
                canvasCtrl.Visibility = Visibility.Collapsed;
                imageCtrl.Visibility = Visibility.Visible;

                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(filePath);
                    using var stream = await file.OpenReadAsync();
                    var bitmapImage = new BitmapImage();
                    await bitmapImage.SetSourceAsync(stream);
                    imageCtrl.Source = bitmapImage;
                }
                catch { }
            }
        }

        private void StopAnimation()
        {
            _animationTimer.Stop();

            _codec1?.Dispose(); _codec1 = null;
            _data1?.Dispose(); _data1 = null;
            _bitmap1?.Dispose(); _bitmap1 = null;
            _frameCount1 = 0;

            _codec2?.Dispose(); _codec2 = null;
            _data2?.Dispose(); _data2 = null;
            _bitmap2?.Dispose(); _bitmap2 = null;
            _frameCount2 = 0;
        }

        private void AnimationTimer_Tick(object? sender, object e)
        {
            bool needsInvalidate1 = false;
            if (_codec1 != null && _frameCount1 > 1)
            {
                _currentFrame1 = (_currentFrame1 + 1) % _frameCount1;
                // Simplified timing: using a fixed 100ms or so by the timer if multiple animations exist
                needsInvalidate1 = true;
            }

            bool needsInvalidate2 = false;
            if (_codec2 != null && _frameCount2 > 1)
            {
                _currentFrame2 = (_currentFrame2 + 1) % _frameCount2;
                needsInvalidate2 = true;
            }

            // Adjust interval to the next frame of codec1, or fixed if multiple
            if (_codec1 != null && _frameCount1 > 1)
            {
                var frameInfo = _codec1.FrameInfo[_currentFrame1];
                _animationTimer.Interval = TimeSpan.FromMilliseconds(frameInfo.Duration > 0 ? frameInfo.Duration : 100);
            }
            else if (_codec2 != null && _frameCount2 > 1)
            {
                var frameInfo = _codec2.FrameInfo[_currentFrame2];
                _animationTimer.Interval = TimeSpan.FromMilliseconds(frameInfo.Duration > 0 ? frameInfo.Duration : 100);
            }

            if (needsInvalidate1) RightSkiaCanvas.Invalidate();
            if (needsInvalidate2) LeftSkiaCanvas.Invalidate();
        }

        private void RightSkiaCanvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            int align = _isMangaMode ? 0 : 1; // 0: Left, 1: Center
            PaintSkiaCanvas(canvas, e.Info, _codec1, ref _bitmap1, _currentFrame1, align);
        }

        private void LeftSkiaCanvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            PaintSkiaCanvas(canvas, e.Info, _codec2, ref _bitmap2, _currentFrame2, 2); // 2: Right
        }

        private void PaintSkiaCanvas(SKCanvas canvas, SKImageInfo info, SKCodec? codec, ref SKBitmap? bitmap, int currentFrame, int horizontalAlignment)
        {
            if (codec != null && codec.FrameCount > 1)
            {
                var imageInfo = new SKImageInfo(codec.Info.Width, codec.Info.Height, codec.Info.ColorType, codec.Info.AlphaType);
                if (bitmap == null || bitmap.Width != imageInfo.Width || bitmap.Height != imageInfo.Height)
                {
                    bitmap?.Dispose();
                    bitmap = new SKBitmap(imageInfo);
                }
                
                var options = new SKCodecOptions { FrameIndex = currentFrame };
                codec.GetPixels(imageInfo, bitmap.GetPixels(), options);
            }

            if (bitmap != null)
            {
                float scale = Math.Min((float)info.Width / bitmap.Width, (float)info.Height / bitmap.Height);
                float x = (info.Width - bitmap.Width * scale) / 2;
                if (horizontalAlignment == 0) x = 0;
                else if (horizontalAlignment == 2) x = info.Width - bitmap.Width * scale;
                float y = (info.Height - bitmap.Height * scale) / 2;

                var destRect = new SKRect(x, y, x + bitmap.Width * scale, y + bitmap.Height * scale);
                canvas.DrawBitmap(bitmap, destRect);
            }
        }

        private void RootGrid_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var props = e.GetCurrentPoint(RootGrid).Properties;
            bool isCtrl = e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control);

            if (isCtrl)
            {
                // Let ScrollViewer handle Zoom
                return;
            }
            
            // Navigate images
            if (props.MouseWheelDelta < 0)
            {
                Navigate(1); // Next
            }
            else
            {
                Navigate(-1); // Prev
            }
            e.Handled = true;
        }

        private void RootGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            if (appWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen)
            {
                appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
                AppTitleBar.Visibility = Visibility.Visible;
            }
            else
            {
                appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
                AppTitleBar.Visibility = Visibility.Collapsed;
            }
        }

        private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            bool isCtrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (e.Key == _keyToggleManga && isCtrl)
            {
                _isMangaMode = !_isMangaMode;
                ApplicationData.Current.LocalSettings.Values["IsMangaMode"] = _isMangaMode;
                _ = UpdateDisplayAsync();
                e.Handled = true;
                return;
            }

            if (e.Key == Windows.System.VirtualKey.Left)
            {
                Navigate(_isMangaMode ? 1 : -1);
                e.Handled = true;
                return;
            }
            if (e.Key == Windows.System.VirtualKey.Right)
            {
                Navigate(_isMangaMode ? -1 : 1);
                e.Handled = true;
                return;
            }

            if (e.Key == _keyPrevImage)
            {
                Navigate(-1);
                e.Handled = true;
            }
            else if (e.Key == _keyNextImage)
            {
                Navigate(1);
                e.Handled = true;
            }
            else if (e.Key == _keyPrevFolder)
            {
                NavigateFolder(-1);
                e.Handled = true;
            }
            else if (e.Key == _keyNextFolder)
            {
                NavigateFolder(1);
                e.Handled = true;
            }
        }

        private void NavigateFolder(int offset)
        {
            if (string.IsNullOrEmpty(_currentDirectory)) return;

            string? nextImageFolder = FindNextImageFolder(_currentDirectory, offset);
            if (!string.IsNullOrEmpty(nextImageFolder))
            {
                LoadDirectory(nextImageFolder);
            }
        }

        private string? FindNextImageFolder(string currentPath, int offset)
        {
            string? node = currentPath;
            var extensions = new HashSet<string>(new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" }, StringComparer.OrdinalIgnoreCase);

            int maxIterations = 1000;
            for (int i = 0; i < maxIterations; i++)
            {
                node = offset == 1 ? GetNextNodeDFS(node) : GetPrevNodeDFS(node);
                if (string.IsNullOrEmpty(node)) break;

                try
                {
                    bool hasImages = Directory.EnumerateFiles(node)
                                              .Any(f => extensions.Contains(Path.GetExtension(f)));
                    if (hasImages)
                    {
                        return node;
                    }
                }
                catch { }
            }
            
            return null;
        }

        private string? GetNextNodeDFS(string current)
        {
            try 
            {
                var dirs = Directory.GetDirectories(current).OrderBy(d => d, new NaturalStringComparer()).ToArray();
                if (dirs.Length > 0) return dirs[0];
            } catch {}

            string node = current;
            while(true)
            {
                var parent = Directory.GetParent(node);
                if (parent == null) return null;

                try 
                {
                    var siblings = parent.GetDirectories().Select(d => d.FullName).OrderBy(d => d, new NaturalStringComparer()).ToList();
                    int idx = siblings.FindIndex(d => string.Equals(d, node, StringComparison.OrdinalIgnoreCase));
                    if (idx != -1 && idx + 1 < siblings.Count)
                    {
                        return siblings[idx + 1];
                    }
                } catch {}
                
                node = parent.FullName;
            }
        }

        private string? GetPrevNodeDFS(string current)
        {
            var parent = Directory.GetParent(current);
            if (parent == null) return null;

            try 
            {
                var siblings = parent.GetDirectories().Select(d => d.FullName).OrderBy(d => d, new NaturalStringComparer()).ToList();
                int idx = siblings.FindIndex(d => string.Equals(d, current, StringComparison.OrdinalIgnoreCase));
                if (idx > 0)
                {
                    string node = siblings[idx - 1];
                    while(true)
                    {
                        try 
                        {
                            var children = Directory.GetDirectories(node).OrderBy(d => d, new NaturalStringComparer()).ToArray();
                            if (children.Length == 0) return node;
                            node = children[children.Length - 1];
                        } catch {
                            return node;
                        }
                    }
                }
                else if (idx == 0)
                {
                    return parent.FullName;
                }
            } catch {}
            
            return parent.FullName;
        }

        private void Navigate(int offset)
        {
            if (_playlist.Count == 0) return;

            int step = _isMangaMode ? 2 : 1;

            if (offset > 0)
            {
                _currentIndex += step;
                if (_currentIndex >= _playlist.Count) _currentIndex = 0;
            }
            else
            {
                _currentIndex -= step;
                if (_currentIndex < 0)
                {
                    int remainder = _playlist.Count % step;
                    _currentIndex = _playlist.Count - (remainder == 0 ? step : remainder);
                    if (_currentIndex < 0) _currentIndex = 0;
                }
            }

            _ = UpdateDisplayAsync();
        }

        private string CurrentImagePath => _playlist != null && _currentIndex >= 0 && _currentIndex < _playlist.Count ? _playlist[_currentIndex] : string.Empty;

        // Selection State
        private Windows.Foundation.Point _selectionStart;
        private bool _isSelecting = false;
        private bool _hasSelection = false;

        private void PagesGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(PagesGrid);
            if (point.Properties.IsLeftButtonPressed)
            {
                _isSelecting = true;
                _hasSelection = false;
                _selectionStart = point.Position;
                SelectionRectangle.Width = 0;
                SelectionRectangle.Height = 0;
                Canvas.SetLeft(SelectionRectangle, _selectionStart.X);
                Canvas.SetTop(SelectionRectangle, _selectionStart.Y);
                SelectionRectangle.Visibility = Visibility.Visible;
                PagesGrid.CapturePointer(e.Pointer);
                e.Handled = true;
            }
        }

        private void PagesGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isSelecting)
            {
                var point = e.GetCurrentPoint(PagesGrid);
                double x = Math.Min(point.Position.X, _selectionStart.X);
                double y = Math.Min(point.Position.Y, _selectionStart.Y);
                double width = Math.Abs(point.Position.X - _selectionStart.X);
                double height = Math.Abs(point.Position.Y - _selectionStart.Y);
                
                Canvas.SetLeft(SelectionRectangle, x);
                Canvas.SetTop(SelectionRectangle, y);
                SelectionRectangle.Width = width;
                SelectionRectangle.Height = height;
                e.Handled = true;
            }
        }

        private void PagesGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isSelecting)
            {
                _isSelecting = false;
                PagesGrid.ReleasePointerCapture(e.Pointer);
                if (SelectionRectangle.Width > 5 && SelectionRectangle.Height > 5)
                {
                    _hasSelection = true;
                }
                else
                {
                    _hasSelection = false;
                    SelectionRectangle.Visibility = Visibility.Collapsed;
                }
                e.Handled = true;
            }
        }

        private void PagesGrid_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
        }

        private void EditMenuFlyout_Opening(object sender, object e)
        {
            MenuCrop.IsEnabled = _hasSelection;
        }

        private async void MenuSaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string ext)
            {
                await SaveImageAsync(ext, false);
            }
        }

        private async void MenuOverwrite_Click(object sender, RoutedEventArgs e)
        {
            await SaveImageAsync(Path.GetExtension(CurrentImagePath), true);
        }

        private async Task SaveImageAsync(string targetExtension, bool overwrite)
        {
            string sourcePath = CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;

            string destPath = overwrite ? sourcePath : Path.ChangeExtension(sourcePath, targetExtension);
            if (!overwrite)
            {
                var picker = new Windows.Storage.Pickers.FileSavePicker();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
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
                byte[] fileBytes = File.ReadAllBytes(sourcePath);
                using var data = SKData.CreateCopy(fileBytes);
                using var codec = SKCodec.Create(data);
                using var bitmap = SKBitmap.Decode(codec);
                if (bitmap != null)
                {
                    using var image = SKImage.FromBitmap(bitmap);
                    using var skData = image.Encode(GetSKEncodedImageFormat(targetExtension), 100);
                    
                    if (overwrite)
                    {
                        StopAnimation();
                        RightImage.Source = null;
                        LeftImage.Source = null;
                    }

                    using var stream = File.Open(destPath, FileMode.Create, FileAccess.Write);
                    skData.SaveTo(stream);
                }
                
                if (overwrite) _ = UpdateDisplayAsync();
            }
            catch { }
        }

        private SKEncodedImageFormat GetSKEncodedImageFormat(string ext)
        {
            return ext.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
                ".png" => SKEncodedImageFormat.Png,
                ".webp" => SKEncodedImageFormat.Webp,
                ".bmp" => SKEncodedImageFormat.Bmp,
                _ => SKEncodedImageFormat.Png
            };
        }

        private void MenuCrop_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasSelection) return;
            string sourcePath = CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;

            try
            {
                byte[] fileBytes = File.ReadAllBytes(sourcePath);
                using var data = SKData.CreateCopy(fileBytes);
                using var codec = SKCodec.Create(data);
                using var bitmap = SKBitmap.Decode(codec);
                if (bitmap != null)
                {
                    FrameworkElement targetElement = RightImage.Visibility == Visibility.Visible ? RightImage : RightSkiaCanvas;
                    
                    double renderRatio = targetElement.ActualWidth / targetElement.ActualHeight;
                    double imageRatio = (double)bitmap.Width / bitmap.Height;

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

                    var ttv = SelectionRectangle.TransformToVisual(targetElement);
                    var rectTopLeft = ttv.TransformPoint(new Windows.Foundation.Point(0, 0));
                    var rectBottomRight = ttv.TransformPoint(new Windows.Foundation.Point(SelectionRectangle.Width, SelectionRectangle.Height));

                    double cropX = (rectTopLeft.X - offsetX) * (bitmap.Width / imgDisplayWidth);
                    double cropY = (rectTopLeft.Y - offsetY) * (bitmap.Height / imgDisplayHeight);
                    double cropW = (rectBottomRight.X - rectTopLeft.X) * (bitmap.Width / imgDisplayWidth);
                    double cropH = (rectBottomRight.Y - rectTopLeft.Y) * (bitmap.Height / imgDisplayHeight);

                    cropX = Math.Max(0, Math.Min(cropX, bitmap.Width));
                    cropY = Math.Max(0, Math.Min(cropY, bitmap.Height));
                    cropW = Math.Max(1, Math.Min(cropW, bitmap.Width - cropX));
                    cropH = Math.Max(1, Math.Min(cropH, bitmap.Height - cropY));

                    var cropRect = new SKRectI((int)cropX, (int)cropY, (int)(cropX + cropW), (int)(cropY + cropH));
                    
                    using var croppedBitmap = new SKBitmap(cropRect.Width, cropRect.Height);
                    bitmap.ExtractSubset(croppedBitmap, cropRect);

                    using var image = SKImage.FromBitmap(croppedBitmap);
                    using var skData = image.Encode(GetSKEncodedImageFormat(Path.GetExtension(sourcePath)), 100);

                    StopAnimation();
                    RightImage.Source = null;
                    LeftImage.Source = null;

                    using var stream = File.Open(sourcePath, FileMode.Create, FileAccess.Write);
                    skData.SaveTo(stream);
                }

                SelectionRectangle.Visibility = Visibility.Collapsed;
                _hasSelection = false;
                _ = UpdateDisplayAsync();
            }
            catch { }
        }

        private async void MenuResize_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;

            var dialog = new ContentDialog
            {
                Title = "Resize Image",
                PrimaryButtonText = "Resize",
                CloseButtonText = "Cancel",
                XamlRoot = this.Content.XamlRoot
            };

            var stackPanel = new StackPanel { Spacing = 10 };
            var widthBox = new NumberBox { Header = "Width" };
            var heightBox = new NumberBox { Header = "Height" };
            stackPanel.Children.Add(widthBox);
            stackPanel.Children.Add(heightBox);
            dialog.Content = stackPanel;

            try
            {
                byte[] fileBytes = File.ReadAllBytes(sourcePath);
                using var data = SKData.CreateCopy(fileBytes);
                using var codec = SKCodec.Create(data);
                if (codec != null)
                {
                    widthBox.Value = codec.Info.Width;
                    heightBox.Value = codec.Info.Height;
                }

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    int newWidth = (int)widthBox.Value;
                    int newHeight = (int)heightBox.Value;
                    if (newWidth <= 0 || newHeight <= 0) return;

                    using var bitmap = SKBitmap.Decode(codec);
                    if (bitmap != null)
                    {
                        using var resizedBitmap = bitmap.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.High);
                        using var image = SKImage.FromBitmap(resizedBitmap);
                        using var skData = image.Encode(GetSKEncodedImageFormat(Path.GetExtension(sourcePath)), 100);

                        StopAnimation();
                        RightImage.Source = null;
                        LeftImage.Source = null;

                        using var stream = File.Open(sourcePath, FileMode.Create, FileAccess.Write);
                        skData.SaveTo(stream);
                    }
                    _ = UpdateDisplayAsync();
                }
            }
            catch { }
        }

        private async void MenuOpenExplorer_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath)) return;

            try
            {
                var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(sourcePath));
                var file = await StorageFile.GetFileFromPathAsync(sourcePath);
                var options = new Windows.System.FolderLauncherOptions();
                options.ItemsToSelect.Add(file);
                await Windows.System.Launcher.LaunchFolderAsync(folder, options);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open explorer: {ex.Message}");
            }
        }

        private void LoadKeyBindings()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            if (settings.TryGetValue("Key_NextImage", out object nextImg)) _keyNextImage = (Windows.System.VirtualKey)(int)nextImg;
            if (settings.TryGetValue("Key_PrevImage", out object prevImg)) _keyPrevImage = (Windows.System.VirtualKey)(int)prevImg;
            if (settings.TryGetValue("Key_NextFolder", out object nextFld)) _keyNextFolder = (Windows.System.VirtualKey)(int)nextFld;
            if (settings.TryGetValue("Key_PrevFolder", out object prevFld)) _keyPrevFolder = (Windows.System.VirtualKey)(int)prevFld;
            if (settings.TryGetValue("Key_ToggleManga", out object tglManga)) _keyToggleManga = (Windows.System.VirtualKey)(int)tglManga;
        }

        private void SaveKeyBindings()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            settings["Key_NextImage"] = (int)_keyNextImage;
            settings["Key_PrevImage"] = (int)_keyPrevImage;
            settings["Key_NextFolder"] = (int)_keyNextFolder;
            settings["Key_PrevFolder"] = (int)_keyPrevFolder;
            settings["Key_ToggleManga"] = (int)_keyToggleManga;
        }

        private TextBox CreateKeyBindingTextBox(string header, Windows.System.VirtualKey currentKey, Action<Windows.System.VirtualKey> updateAction)
        {
            var tb = new TextBox { Header = header, Text = currentKey.ToString(), IsReadOnly = true };
            tb.PreviewKeyDown += (s, e) =>
            {
                var key = e.Key;
                if (key != Windows.System.VirtualKey.Control && key != Windows.System.VirtualKey.Shift && key != Windows.System.VirtualKey.Menu)
                {
                    tb.Text = key.ToString();
                    updateAction(key);
                }
                e.Handled = true;
            };
            return tb;
        }

        private async void MenuKeyBindings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Key Bindings Settings",
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                XamlRoot = this.Content.XamlRoot
            };

            var stackPanel = new StackPanel { Spacing = 10 };
            
            var tempNextImage = _keyNextImage;
            var tempPrevImage = _keyPrevImage;
            var tempNextFolder = _keyNextFolder;
            var tempPrevFolder = _keyPrevFolder;
            var tempToggleManga = _keyToggleManga;

            stackPanel.Children.Add(CreateKeyBindingTextBox("Next Image", tempNextImage, k => tempNextImage = k));
            stackPanel.Children.Add(CreateKeyBindingTextBox("Previous Image", tempPrevImage, k => tempPrevImage = k));
            stackPanel.Children.Add(CreateKeyBindingTextBox("Next Folder", tempNextFolder, k => tempNextFolder = k));
            stackPanel.Children.Add(CreateKeyBindingTextBox("Previous Folder", tempPrevFolder, k => tempPrevFolder = k));
            stackPanel.Children.Add(CreateKeyBindingTextBox("Toggle Manga Mode (Requires Ctrl)", tempToggleManga, k => tempToggleManga = k));

            dialog.Content = stackPanel;

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                _keyNextImage = tempNextImage;
                _keyPrevImage = tempPrevImage;
                _keyNextFolder = tempNextFolder;
                _keyPrevFolder = tempPrevFolder;
                _keyToggleManga = tempToggleManga;
                SaveKeyBindings();
            }
        }
    }
}
