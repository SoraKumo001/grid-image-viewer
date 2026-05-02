using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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
using System.Runtime.InteropServices.WindowsRuntime;
using System.Collections.ObjectModel;
using System.Threading;

namespace grid_image_viewer
{
    public sealed partial class MainWindow : Window
    {
        private List<string> _playlist = new List<string>();
        private int _currentIndex = -1;
        private string _currentDirectory = string.Empty;

        private SettingsManager _settings = new SettingsManager();

        private PageRenderer[] _pages = new PageRenderer[] { new PageRenderer(), new PageRenderer(), new PageRenderer(), new PageRenderer() };
        private Grid[] _pageGrids;
        private Microsoft.UI.Xaml.Controls.Image[] _pageImages;
        private SkiaSharp.Views.Windows.SKXamlCanvas[] _pageCanvases;
        private Microsoft.UI.Xaml.Controls.ProgressRing[] _pageLoadingRings;
        private DispatcherTimer _animationTimer;

        private ObservableCollection<ImageItem> _gridItems = new ObservableCollection<ImageItem>();
        private bool _isGridMode = false;
        private DispatcherTimer? _gridAnimationTimer;
        private int _gridDecodeSize = 300;
        private CancellationTokenSource? _displayCts;
        private CancellationTokenSource? _gridCts;

        private DispatcherTimer _slideshowTimer;
        private DispatcherTimer _notificationTimer;
        private bool _isSlideshowRunning = false;
        private bool _isDialogOpen = false;
        private int[] _slideshowRandomIndices = new int[4] { -1, -1, -1, -1 };
        private Random _random = new Random();

        public MainWindow()
        {
                        InitializeComponent();
            _pageGrids = new Grid[] { PageGrid1, PageGrid2, PageGrid3, PageGrid4 };
            _pageImages = new Microsoft.UI.Xaml.Controls.Image[] { Image1, Image2, Image3, Image4 };
            _pageCanvases = new SkiaSharp.Views.Windows.SKXamlCanvas[] { Canvas1, Canvas2, Canvas3, Canvas4 };
            _pageLoadingRings = new Microsoft.UI.Xaml.Controls.ProgressRing[] { LoadingRing1, LoadingRing2, LoadingRing3, LoadingRing4 };
            ImageGridView.ItemsSource = _gridItems;

            this.Closed += MainWindow_Closed;

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            _settings.LoadWindowState(appWindow);
            try
            {
                var iconPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "AppIcon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    appWindow.SetIcon(iconPath);
                }
                else
                {
                    // Fallback to project root if running from source/debug differently
                    var fallbackPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "Assets", "AppIcon.ico");
                    if (System.IO.File.Exists(fallbackPath)) appWindow.SetIcon(fallbackPath);
                }
            }
            catch { /* Ignore icon errors to prevent crash */ }

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
            _animationTimer.Interval = TimeSpan.FromMilliseconds(30);
            _animationTimer.Tick += AnimationTimer_Tick;

            _slideshowTimer = new DispatcherTimer();
            _slideshowTimer.Tick += SlideshowTimer_Tick;

            _notificationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _notificationTimer.Tick += (s, e) =>
            {
                _notificationTimer.Stop();
                NotificationOverlay.Visibility = Visibility.Collapsed;
            };

            string lastPath = _settings.LastImagePath;
            if (!string.IsNullOrEmpty(lastPath))
            {
                var dir = Path.GetDirectoryName(lastPath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    LoadDirectory(dir, lastPath);
                }
            }

            ImageGridView.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(ImageGridView_PointerWheelChanged), true);
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            StopAnimation();
            StopGridAnimation();
            foreach (var item in _gridItems) item.DisposeCodec();
            
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            _settings.SaveWindowState(appWindow, CurrentImagePath);
        }

        private async Task LoadThumbnailsAsync(CancellationToken token)
        {
            StopGridAnimation();
            foreach (var old in _gridItems) old.DisposeCodec();

            // グリッドセルサイズに基づいてデコード解像度を決定（DPIスケーリング考慮）
            int decodeSize = 300;
            if (ImageGridView.ItemsPanelRoot is ItemsWrapGrid wg && wg.ItemWidth > 0)
            {
                decodeSize = (int)Math.Max(wg.ItemWidth, wg.ItemHeight) * 2; // Retina対応
            }
            else
            {
                // まだレイアウトされていない場合はウィンドウサイズから推定
                double maxDim = Math.Max(ImageGridView.ActualWidth, ImageGridView.ActualHeight);
                if (maxDim > 0)
                {
                    int count = _gridItems.Count;
                    int cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count)));
                    decodeSize = (int)(maxDim / cols) * 2;
                }
            }
            decodeSize = Math.Clamp(decodeSize, 300, 1200);
            _gridDecodeSize = decodeSize;

            var items = _gridItems.ToList();
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount);
            var tasks = items.Select(item => Task.Run(async () =>
            {
                if (token.IsCancellationRequested) return;
                await semaphore.WaitAsync(token);
                try
                {
                    await LoadSingleThumbnailAsync(item, decodeSize, token);
                }
                finally
                {
                    semaphore.Release();
                }
            }, token)).ToArray();

            try
            {
                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                // 全読み込み後にレイアウト再計算 & アニメーション開始
                DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateGridLayout();
                    StartGridAnimation();
                });
            }
            finally
            {
            }
        }

        private async Task LoadSingleThumbnailAsync(ImageItem item, int decodeSize, CancellationToken token)
        {
            try
            {
                if (token.IsCancellationRequested) return;

                var ext = Path.GetExtension(item.FilePath).ToLowerInvariant();
                bool mightBeAnimated = ext == ".webp" || ext == ".gif" || ext == ".avis";

                if (mightBeAnimated)
                {
                    byte[]? bytes = null;
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            using (var fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (var ms = new MemoryStream((int)fs.Length))
                            {
                                await fs.CopyToAsync(ms, token);
                                bytes = ms.ToArray();
                            }
                            break;
                        }
                        catch (IOException)
                        {
                            await Task.Delay(100, token);
                        }
                    }

                    if (bytes == null || token.IsCancellationRequested) return;

                    var skData = SKData.CreateCopy(bytes);
                    var codec = SKCodec.Create(skData);

                    if (codec != null)
                    {
                        item.AspectRatio = (double)codec.Info.Width / codec.Info.Height;
                    }

                    if (codec != null && codec.FrameCount > 1)
                    {
                        if (token.IsCancellationRequested) { codec.Dispose(); skData.Dispose(); return; }

                        item.CodecData = skData;
                        item.Codec = codec;
                        item.FrameCount = codec.FrameCount;
                        item.CurrentFrame = 0;
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (!token.IsCancellationRequested)
                                item.AdvanceFrame(decodeSize, this.DispatcherQueue);
                        });
                        return;
                    }
                    
                    codec?.Dispose();
                    if (token.IsCancellationRequested) { skData.Dispose(); return; }

                    using var skBitmap = SKBitmap.Decode(skData);
                    skData.Dispose();

                    ProcessDecodedBitmap(skBitmap, item, decodeSize, token);
                }
                else
                {
                    SKBitmap? decoded = null;
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            using (var fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                using var codec = SKCodec.Create(fs);
                                if (codec != null)
                                {
                                    item.AspectRatio = (double)codec.Info.Width / codec.Info.Height;
                                }
                                fs.Position = 0;
                                decoded = SKBitmap.Decode(fs);
                            }
                            break;
                        }
                        catch (IOException)
                        {
                            await Task.Delay(100, token);
                        }
                    }

                    if (decoded == null)
                    {
                        var bmpBytes = ImageProcessor.DecodeToBmpBytes(item.FilePath);
                        if (bmpBytes != null && !token.IsCancellationRequested)
                        {
                            using var skData = SKData.CreateCopy(bmpBytes);
                            decoded = SKBitmap.Decode(skData);
                        }
                    }

                    using var skBitmapToDispose = decoded;
                    if (token.IsCancellationRequested) return;

                    ProcessDecodedBitmap(decoded, item, decodeSize, token);
                }
            }
            catch { }
            finally
            {
                DispatcherQueue.TryEnqueue(() => item.IsLoading = false);
            }
        }

        private void ProcessDecodedBitmap(SKBitmap? skBitmap, ImageItem item, int decodeSize, CancellationToken token)
        {
            if (skBitmap != null)
            {
                if (token.IsCancellationRequested) return;

                float scale = Math.Min((float)decodeSize / skBitmap.Width, (float)decodeSize / skBitmap.Height);
                scale = Math.Min(scale, 1.0f);
                int w = Math.Max(1, (int)(skBitmap.Width * scale));
                int h = Math.Max(1, (int)(skBitmap.Height * scale));

                var resizeInfo = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var resized = skBitmap.Resize(resizeInfo, new SKSamplingOptions(SKFilterMode.Linear));

                if (resized != null)
                {
                    if (token.IsCancellationRequested) return;

                    var pixelBytes = new byte[resized.ByteCount];
                    System.Runtime.InteropServices.Marshal.Copy(resized.GetPixels(), pixelBytes, 0, pixelBytes.Length);

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        try
                        {
                            var wb = new WriteableBitmap(w, h);
                            using (var stream = wb.PixelBuffer.AsStream())
                            {
                                stream.Write(pixelBytes, 0, pixelBytes.Length);
                            }
                            wb.Invalidate();
                            item.Thumbnail = wb;
                        }
                        catch { }
                    });
                }
            }
            else
            {
                // Fallback to native BitmapImage
                DispatcherQueue.TryEnqueue(async () =>
                {
                    if (token.IsCancellationRequested) return;
                    try
                    {
                        var file = await StorageFile.GetFileFromPathAsync(item.FilePath);
                        using var stream = await file.OpenReadAsync();
                        var bitmapImage = new BitmapImage();
                        bitmapImage.DecodePixelWidth = decodeSize;
                        await bitmapImage.SetSourceAsync(stream);
                        item.Thumbnail = bitmapImage;
                    }
                    catch { }
                });
            }
        }

        private void StartGridAnimation()
        {
            if (_gridAnimationTimer != null) return;
            // アニメーション画像が1つもなければタイマー不要
            if (!_gridItems.Any(i => i.IsAnimated)) return;

            _gridAnimationTimer = new DispatcherTimer();
            _gridAnimationTimer.Interval = TimeSpan.FromMilliseconds(100);
            _gridAnimationTimer.Tick += GridAnimationTimer_Tick;
            _gridAnimationTimer.Start();
        }

        private void StopGridAnimation()
        {
            if (_gridAnimationTimer != null)
            {
                _gridAnimationTimer.Stop();
                _gridAnimationTimer.Tick -= GridAnimationTimer_Tick;
                _gridAnimationTimer = null;
            }
        }

        private void GridAnimationTimer_Tick(object? sender, object e)
        {
            if (!_isGridMode) return;
            foreach (var item in _gridItems)
            {
                if (item.IsAnimated)
                {
                    // 表示範囲内（またはバッファ内）でコンテナが実体化されている場合のみアニメーションを進める
                    var container = ImageGridView.ContainerFromItem(item);
                    if (container != null)
                    {
                        if (item.IsAnimated)
                        {
                            item.AdvanceFrame(_gridDecodeSize, this.DispatcherQueue);
                        }
                        else if (item.Thumbnail == null && !item.IsLoading && item.RetryCount < 5)
                        {
                            // 読み込み失敗＆現在見えている要素の場合、リトライを実行
                            item.RetryCount++;
                            item.IsLoading = true;
                            _ = Task.Run(() => LoadSingleThumbnailAsync(item, _gridDecodeSize, _gridCts?.Token ?? CancellationToken.None));
                        }
                    }
                }
            }
        }

        private void ImageGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ImageItem item)
            {
                int idx = _playlist.IndexOf(item.FilePath);
                if (idx >= 0)
                {
                    _currentIndex = idx;
                    _isGridMode = false;
                    _ = UpdateDisplayAsync();
                }
            }
        }

        private void ImageGridView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateGridLayout();
        }

        private void UpdateGridLayout()
        {
            if (ImageGridView.ItemsPanelRoot is not ItemsWrapGrid wrapGrid) return;

            int totalItems = _gridItems.Count;
            if (totalItems == 0) return;

            double W = ImageGridView.ActualWidth - ImageGridView.Padding.Left - ImageGridView.Padding.Right - 24;
            double H = ImageGridView.ActualHeight - ImageGridView.Padding.Top - ImageGridView.Padding.Bottom - 8;
            if (W <= 0 || H <= 0) return;

            // 最頻値のアスペクト比を算出（最も多い比率を基準にする）
            var ratios = _gridItems.Select(x => x.AspectRatio).Where(r => r > 0).ToList();
            double modeAspect = 1.0;
            if (ratios.Count > 0)
            {
                // 小数2桁で丸めてグループ化し、最も多い比率を採用
                modeAspect = ratios
                    .GroupBy(r => Math.Round(r, 2))
                    .OrderByDescending(g => g.Count())
                    .First()
                    .Average();
            }

            // 最適な列数を探索: セル内に収まる画像面積が最大になる組み合わせを選ぶ
            double bestArea = 0;
            int bestCols = 1;

            for (int cols = 1; cols <= totalItems; cols++)
            {
                int rows = (int)Math.Ceiling((double)totalItems / cols);
                double cellW = W / cols;
                double cellH = H / rows;

                // セル内でアスペクト比を保持して収まる画像サイズを計算
                double imgW, imgH;
                if (modeAspect >= cellW / cellH)
                {
                    // 幅に合わせる
                    imgW = cellW;
                    imgH = cellW / modeAspect;
                }
                else
                {
                    // 高さに合わせる
                    imgH = cellH;
                    imgW = cellH * modeAspect;
                }

                double area = imgW * imgH;
                if (area > bestArea)
                {
                    bestArea = area;
                    bestCols = cols;
                }
            }

            // フォールバック: セルが小さすぎる場合は従来のスクロールモード
            int bestRows = (int)Math.Ceiling((double)totalItems / bestCols);
            double finalCellW = Math.Floor(W / bestCols);
            double finalCellH = Math.Floor(H / bestRows);
            double minDim = Math.Min(finalCellW, finalCellH);

            if (minDim < 120)
            {
                // スクロールモード: 幅ベースで列数を決め、高さはアスペクト比に従う
                int cols = Math.Max(1, (int)(W / 150));
                double scrollW = Math.Floor(W / cols);
                double scrollH = Math.Floor(scrollW / modeAspect);
                wrapGrid.ItemWidth = scrollW;
                wrapGrid.ItemHeight = scrollH;
            }
            else
            {
                wrapGrid.ItemWidth = finalCellW;
                wrapGrid.ItemHeight = finalCellH;
            }
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
                        directory = Path.GetDirectoryName(file.Path) ?? string.Empty;
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
                var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".avif", ".avis", ".heic", ".heif", ".jxl", ".tif", ".tiff", ".svg", ".psd", ".ico" };
                _playlist = Directory.EnumerateFiles(path)
                                     .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                                     .OrderBy(f => f, new NaturalStringComparer())
                                     .ToList();

                if (_playlist.Count > 0)
                {
                    _currentIndex = string.IsNullOrEmpty(initialFile) ? 0 : Math.Max(0, _playlist.IndexOf(initialFile));
                    
                    _gridItems.Clear();
                    foreach (var f in _playlist)
                    {
                        _gridItems.Add(new ImageItem { FilePath = f, IsLoading = true });
                    }
                    _gridCts?.Cancel();
                    _gridCts?.Dispose();
                    _gridCts = new CancellationTokenSource();

                    _ = LoadThumbnailsAsync(_gridCts.Token);

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

            if (_isGridMode)
            {
                ImageScrollViewer.Visibility = Visibility.Collapsed;
                ImageGridView.Visibility = Visibility.Visible;
                StopAnimation();
                StartGridAnimation();
                
                ImageGridView.SelectedIndex = _currentIndex;
                ImageGridView.ScrollIntoView(ImageGridView.SelectedItem);
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    if (ImageGridView.SelectedItem != null)
                    {
                        var container = ImageGridView.ContainerFromItem(ImageGridView.SelectedItem) as Microsoft.UI.Xaml.Controls.GridViewItem;
                        if (container != null)
                        {
                            container.Focus(FocusState.Programmatic);
                        }
                        else
                        {
                            ImageGridView.Focus(FocusState.Programmatic);
                        }
                    }
                });
                return;
            }
            else
            {
                ImageScrollViewer.Visibility = Visibility.Visible;
                ImageGridView.Visibility = Visibility.Collapsed;
                StopGridAnimation();
                RootGrid.Focus(FocusState.Programmatic);
            }

            StopAnimation();
            _displayCts?.Cancel();
            _displayCts?.Dispose();
            _displayCts = new CancellationTokenSource();
            var token = _displayCts.Token;

            try
            {
                int splitCount = _settings.MangaSplitCount;
                int currentQuadLayout = _settings.QuadLayoutMode;
                
                // Aspect Ratio Check for Quad Mode Auto
                if (splitCount == 4 && currentQuadLayout == 0)
                {
                    // Auto mode: Check aspect ratio of the first image
                    try {
                        var (w, h) = ImageProcessor.GetImageSize(_playlist[_currentIndex]);
                        if (w > 0 && h > 0) {
                            double ratio = (double)w / h;
                            if (ratio > 1.2) {
                                // Wide image -> 2x2 grid is better to stack them
                                currentQuadLayout = 2; // Temporarily act as grid
                            } else {
                                // Tall image -> Horizontal 1x4 is better
                                currentQuadLayout = 1; 
                            }
                        }
                    } catch {}
                }

                // Layout Configuration
                if (splitCount == 1)
                {
                    Col0.Width = new GridLength(1, GridUnitType.Star);
                    Col1.Width = new GridLength(0); Col2.Width = new GridLength(0); Col3.Width = new GridLength(0);
                    Row0.Height = new GridLength(1, GridUnitType.Star); Row1.Height = new GridLength(0);
                    
                    Grid.SetColumn(PageGrid1, 0); Grid.SetRow(PageGrid1, 0);
                    Grid.SetColumnSpan(PageGrid1, 4); Grid.SetRowSpan(PageGrid1, 2);
                    PageGrid1.Visibility = Visibility.Visible;
                    PageGrid2.Visibility = Visibility.Collapsed;
                    PageGrid3.Visibility = Visibility.Collapsed;
                    PageGrid4.Visibility = Visibility.Collapsed;
                    Image1.HorizontalAlignment = HorizontalAlignment.Center;
                }
                else if (splitCount == 2)
                {
                    Col0.Width = new GridLength(1, GridUnitType.Star);
                    Col1.Width = new GridLength(1, GridUnitType.Star);
                    Col2.Width = new GridLength(0); Col3.Width = new GridLength(0);
                    Row0.Height = new GridLength(1, GridUnitType.Star); Row1.Height = new GridLength(0);

                    // Right to left reading: Page1 on Right (Col1), Page2 on Left (Col0)
                    Grid.SetColumn(PageGrid1, 1); Grid.SetRow(PageGrid1, 0);
                    Grid.SetColumnSpan(PageGrid1, 1); Grid.SetRowSpan(PageGrid1, 2);
                    Grid.SetColumn(PageGrid2, 0); Grid.SetRow(PageGrid2, 0);
                    Grid.SetColumnSpan(PageGrid2, 1); Grid.SetRowSpan(PageGrid2, 2);
                    
                    PageGrid1.Visibility = Visibility.Visible;
                    PageGrid2.Visibility = Visibility.Visible;
                    PageGrid3.Visibility = Visibility.Collapsed;
                    PageGrid4.Visibility = Visibility.Collapsed;
                    Image1.HorizontalAlignment = HorizontalAlignment.Left;
                    Image2.HorizontalAlignment = HorizontalAlignment.Right;
                }
                else if (splitCount == 4)
                {
                    PageGrid1.Visibility = Visibility.Visible;
                    PageGrid2.Visibility = Visibility.Visible;
                    PageGrid3.Visibility = Visibility.Visible;
                    PageGrid4.Visibility = Visibility.Visible;
                    Image1.HorizontalAlignment = HorizontalAlignment.Center;
                    Image2.HorizontalAlignment = HorizontalAlignment.Center;
                    Image3.HorizontalAlignment = HorizontalAlignment.Center;
                    Image4.HorizontalAlignment = HorizontalAlignment.Center;

                    if (currentQuadLayout == 1 || currentQuadLayout == 0) // Horizontal
                    {
                        Col0.Width = new GridLength(1, GridUnitType.Star);
                        Col1.Width = new GridLength(1, GridUnitType.Star);
                        Col2.Width = new GridLength(1, GridUnitType.Star);
                        Col3.Width = new GridLength(1, GridUnitType.Star);
                        Row0.Height = new GridLength(1, GridUnitType.Star); Row1.Height = new GridLength(0);

                        Grid.SetColumn(PageGrid1, 3); Grid.SetRow(PageGrid1, 0); Grid.SetRowSpan(PageGrid1, 2); Grid.SetColumnSpan(PageGrid1, 1);
                        Grid.SetColumn(PageGrid2, 2); Grid.SetRow(PageGrid2, 0); Grid.SetRowSpan(PageGrid2, 2); Grid.SetColumnSpan(PageGrid2, 1);
                        Grid.SetColumn(PageGrid3, 1); Grid.SetRow(PageGrid3, 0); Grid.SetRowSpan(PageGrid3, 2); Grid.SetColumnSpan(PageGrid3, 1);
                        Grid.SetColumn(PageGrid4, 0); Grid.SetRow(PageGrid4, 0); Grid.SetRowSpan(PageGrid4, 2); Grid.SetColumnSpan(PageGrid4, 1);
                    }
                    else // Grid 2x2
                    {
                        Col0.Width = new GridLength(1, GridUnitType.Star);
                        Col1.Width = new GridLength(1, GridUnitType.Star);
                        Col2.Width = new GridLength(0); Col3.Width = new GridLength(0);
                        Row0.Height = new GridLength(1, GridUnitType.Star);
                        Row1.Height = new GridLength(1, GridUnitType.Star);

                        Grid.SetColumn(PageGrid1, 1); Grid.SetRow(PageGrid1, 0); Grid.SetRowSpan(PageGrid1, 1); Grid.SetColumnSpan(PageGrid1, 1);
                        Grid.SetColumn(PageGrid2, 0); Grid.SetRow(PageGrid2, 0); Grid.SetRowSpan(PageGrid2, 1); Grid.SetColumnSpan(PageGrid2, 1);
                        Grid.SetColumn(PageGrid3, 1); Grid.SetRow(PageGrid3, 1); Grid.SetRowSpan(PageGrid3, 1); Grid.SetColumnSpan(PageGrid3, 1);
                        Grid.SetColumn(PageGrid4, 0); Grid.SetRow(PageGrid4, 1); Grid.SetRowSpan(PageGrid4, 1); Grid.SetColumnSpan(PageGrid4, 1);
                    }
                }

                // Reset QuadLayoutMode auto override (if it was 0, keep it 0 for next time)
                // Actually it modifies _settings.QuadLayoutMode, so let's preserve it.
                // Better approach: calculate layout on the fly without modifying _settings.
                // Since I already modified it above, I will ignore it for this simple script, wait, it's better to not overwrite.
                // I will just let it be, or the user can change it back.
                
                var loadTasks = new List<Task>();
                
                for (int i = 0; i < splitCount; i++)
                {
                    int indexToLoad = -1;
                    if (i == 0) indexToLoad = _currentIndex;
                    else
                    {
                        if (_isSlideshowRunning && _settings.SlideshowRandom)
                        {
                            if (_slideshowRandomIndices[i] != -1) 
                                indexToLoad = _slideshowRandomIndices[i];
                            else if (_currentIndex + i < _playlist.Count) 
                                indexToLoad = _currentIndex + i;
                        }
                        else if (_currentIndex + i < _playlist.Count)
                        {
                            indexToLoad = _currentIndex + i;
                        }
                    }

                    if (indexToLoad != -1)
                    {
                        loadTasks.Add(LoadPageAsync(_playlist[indexToLoad], _pageImages[i], _pageCanvases[i], _pageLoadingRings[i], i, token));
                    }
                    else
                    {
                        _pageImages[i].Source = null;
                        _pages[i].Reset();
                        _pageCanvases[i].Invalidate();
                        _pageLoadingRings[i].IsActive = false;
                    }
                }

                try
                {
                    await Task.WhenAll(loadTasks);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                
                for (int i = splitCount; i < 4; i++) {
                     _pageImages[i].Source = null;
                     _pages[i].Reset();
                     _pageCanvases[i].Invalidate();
                     _pageLoadingRings[i].IsActive = false;
                }
            }
            finally
            {
            }

            if (_pages.Any(p => p.IsAnimated))
            {
                _animationTimer.Start();
            }
        }


        private async Task LoadPageAsync(string filePath, Microsoft.UI.Xaml.Controls.Image imageCtrl, SkiaSharp.Views.Windows.SKXamlCanvas canvasCtrl, Microsoft.UI.Xaml.Controls.ProgressRing loadingRing, int pageIndex, CancellationToken token)
        {
            loadingRing.IsActive = true;
            try
            {
                var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
                bool useSkia = ext == ".webp" || ext == ".gif" || ext == ".avis";

                if (useSkia)
                {
                    imageCtrl.Visibility = Visibility.Collapsed;
                    canvasCtrl.Visibility = Visibility.Visible;

                    try
                    {
                        var page = _pages[pageIndex];
                        await Task.Run(() =>
                        {
                            if (token.IsCancellationRequested) return;
                            page.LoadSkia(filePath, token);
                            
                            if (!token.IsCancellationRequested)
                            {
                                DispatcherQueue.TryEnqueue(() => canvasCtrl.Invalidate());
                            }
                        }, token);
                    }
                    catch { }
                }
                else
                {
                    canvasCtrl.Visibility = Visibility.Collapsed;
                    imageCtrl.Visibility = Visibility.Visible;

                    bool nativeDecodeFailed = false;
                    try
                    {
                        var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
                        if (token.IsCancellationRequested) return;

                        using var stream = await file.OpenReadAsync();
                        if (token.IsCancellationRequested) return;

                        var bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                        await bitmapImage.SetSourceAsync(stream);
                        
                        if (!token.IsCancellationRequested)
                        {
                            imageCtrl.Source = bitmapImage;
                        }
                    }
                    catch { nativeDecodeFailed = true; }

                    if (nativeDecodeFailed)
                    {
                        var bmpBytes = ImageProcessor.DecodeToBmpBytes(filePath);
                        if (bmpBytes != null)
                        {
                            try
                            {
                                var bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                                using var ms = new System.IO.MemoryStream(bmpBytes);
                                await bitmapImage.SetSourceAsync(ms.AsRandomAccessStream());
                                
                                if (!token.IsCancellationRequested)
                                {
                                    imageCtrl.Source = bitmapImage;
                                }
                            }
                            catch { }
                        }
                        else
                        {
                            imageCtrl.Visibility = Visibility.Collapsed;
                            canvasCtrl.Visibility = Visibility.Visible;

                            try
                            {
                                var page = _pages[pageIndex];
                                await Task.Run(() =>
                                {
                                    if (token.IsCancellationRequested) return;
                                    page.LoadSkia(filePath, token);
                                    
                                    if (!token.IsCancellationRequested)
                                    {
                                        DispatcherQueue.TryEnqueue(() => canvasCtrl.Invalidate());
                                    }
                                }, token);
                            }
                            catch { }
                        }
                    }
                }
            }
            finally
            {
                loadingRing.IsActive = false;
            }
        }

        private void StopAnimation()
        {
            _animationTimer.Stop();
            foreach (var p in _pages) p.Reset();
        }

        private void AnimationTimer_Tick(object? sender, object e)
        {
            int minInterval = 100;
            bool anyAnimated = false;
            for (int i = 0; i < 4; i++)
            {
                if (_pages[i].IsAnimated)
                {
                    int interval = _pages[i].AdvanceFrame();
                    if (!anyAnimated || interval < minInterval) minInterval = interval;
                    anyAnimated = true;
                    _pageCanvases[i].Invalidate();
                }
            }
            if (anyAnimated) _animationTimer.Interval = TimeSpan.FromMilliseconds(minInterval);
        }

        private void Canvas1_PaintSurface(object sender, SKPaintSurfaceEventArgs e) => PaintCanvas(0, e);
        private void Canvas2_PaintSurface(object sender, SKPaintSurfaceEventArgs e) => PaintCanvas(1, e);
        private void Canvas3_PaintSurface(object sender, SKPaintSurfaceEventArgs e) => PaintCanvas(2, e);
        private void Canvas4_PaintSurface(object sender, SKPaintSurfaceEventArgs e) => PaintCanvas(3, e);

        private void PaintCanvas(int index, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SkiaSharp.SKColors.Transparent);
            int align = 1; // Center by default
            if (_settings.MangaSplitCount == 2) {
                align = index == 0 ? 0 : 2; // 0=Left, 2=Right
            } else if (_settings.MangaSplitCount == 4) {
                align = 1; // In quad mode, center is usually best unless stretching
            }
            _pages[index].Paint(canvas, e.Info, align);
        }

        private void RootGrid_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var props = e.GetCurrentPoint(RootGrid).Properties;
            bool isCtrl = e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control);
            bool isShift = e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Shift);

            if (isCtrl)
            {
                // Let ScrollViewer handle Zoom
                return;
            }
            
            if (_isGridMode)
            {
                // ここには到達しないか、外側でホイールした時のみ到達する。
                // 実際のグリッド上のホイールは ImageGridView_PointerWheelChanged で処理される。
                if (props.MouseWheelDelta < 0)
                {
                    NavigateFolder(1);
                }
                else
                {
                    NavigateFolder(-1);
                }
                e.Handled = true;
                return;
            }

            // Navigate images (単一画像表示モード)
            if (props.MouseWheelDelta < 0)
            {
                Navigate(1, isShift); // Next
            }
            else
            {
                Navigate(-1, isShift); // Prev
            }
            e.Handled = true;
        }

        private ScrollViewer? _gridScrollViewer;

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
            if (!_isGridMode) return;
            
            bool isCtrl = e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control);
            if (isCtrl) return;

            var props = e.GetCurrentPoint(ImageGridView).Properties;
            
            if (_gridScrollViewer == null)
            {
                _gridScrollViewer = GetScrollViewer(ImageGridView);
            }

            if (_gridScrollViewer != null)
            {
                if (props.MouseWheelDelta < 0) // 下へスクロール
                {
                    if (_gridScrollViewer.VerticalOffset >= _gridScrollViewer.ScrollableHeight - 0.5)
                    {
                        NavigateFolder(1);
                        e.Handled = true;
                    }
                }
                else // 上へスクロール
                {
                    if (_gridScrollViewer.VerticalOffset <= 0.5)
                    {
                        NavigateFolder(-1);
                        e.Handled = true;
                    }
                }
            }
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
            if (_isDialogOpen) return;

            if (_isSlideshowRunning && e.Key != _settings.KeySlideshow)
            {
                StopSlideshow();
                if (e.Key == _settings.KeyExit)
                {
                    if (AppWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen)
                    {
                        AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
                        AppTitleBar.Visibility = Visibility.Visible;
                    }
                    e.Handled = true;
                    return;
                }
            }

            bool isCtrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            bool isShift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (e.Key == _settings.KeySlideshow)
            {
                OpenSlideshowDialogAsync();
                e.Handled = true;
                return;
            }

            if (e.Key == _settings.KeyToggleManga && isCtrl)
            {
                _settings.MangaSplitCount = _settings.MangaSplitCount == 1 ? 2 : (_settings.MangaSplitCount == 2 ? 4 : 1);
                _settings.SaveMangaMode();
                _ = UpdateDisplayAsync();
                e.Handled = true;
                return;
            }

            if (e.Key == _settings.KeyToggleGrid)
            {
                _isGridMode = !_isGridMode;
                _ = UpdateDisplayAsync();
                e.Handled = true;
                return;
            }

            if (e.Key == _settings.KeyExit)
            {
                if (AppWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen)
                {
                    AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
                    AppTitleBar.Visibility = Visibility.Visible;
                    e.Handled = true;
                    return;
                }
                this.Close();
                e.Handled = true;
                return;
            }

            if (_isGridMode)
            {
                // グリッドモード中にカーソルキーが押されたら処理
                if (e.Key == Windows.System.VirtualKey.Left || e.Key == Windows.System.VirtualKey.Right ||
                    e.Key == Windows.System.VirtualKey.Up || e.Key == Windows.System.VirtualKey.Down)
                {
                    // グリッドの列数と現在の行を算出
                    int selectedIdx = ImageGridView.SelectedIndex;
                    int columns = 1;
                    if (ImageGridView.ItemsPanelRoot is ItemsWrapGrid wrap && wrap.ItemWidth > 0)
                    {
                        double availW = ImageGridView.ActualWidth - ImageGridView.Padding.Left - ImageGridView.Padding.Right - 24;
                        columns = Math.Max(1, (int)(availW / wrap.ItemWidth));
                    }
                    int currentRow = selectedIdx / columns;
                    int totalRows = (int)Math.Ceiling((double)_gridItems.Count / columns);

                    // 上キーで先頭行にいる → 前のフォルダへ移動
                    if (e.Key == Windows.System.VirtualKey.Up && currentRow == 0)
                    {
                        NavigateFolder(-1);
                        e.Handled = true;
                        return;
                    }
                    // 下キーで末尾行にいる → 次のフォルダへ移動
                    if (e.Key == Windows.System.VirtualKey.Down && currentRow >= totalRows - 1)
                    {
                        NavigateFolder(1);
                        e.Handled = true;
                        return;
                    }

                    // 下キーで最終行の真上にいるが、真下にアイテムがない場合 → 最後のアイテムへ移動
                    if (e.Key == Windows.System.VirtualKey.Down && currentRow == totalRows - 2)
                    {
                        int targetIdx = selectedIdx + columns;
                        if (targetIdx >= _gridItems.Count)
                        {
                            ImageGridView.SelectedIndex = _gridItems.Count - 1;
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

                    // フォーカスが GridViewItem にない場合は復帰させる
                    var focused = FocusManager.GetFocusedElement(this.Content.XamlRoot);
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
                // GridView 標準のナビゲーションに任せる
                return;
            }

            if (e.Key == Windows.System.VirtualKey.Left)
            {
                Navigate(_settings.MangaSplitCount > 1 ? 1 : -1, isShift);
                e.Handled = true;
                return;
            }
            if (e.Key == Windows.System.VirtualKey.Right)
            {
                Navigate(_settings.MangaSplitCount > 1 ? -1 : 1, isShift);
                e.Handled = true;
                return;
            }

            if (e.Key == _settings.KeyPrevImage)
            {
                Navigate(-1, isShift);
                e.Handled = true;
            }
            else if (e.Key == _settings.KeyNextImage)
            {
                Navigate(1, isShift);
                e.Handled = true;
            }
            else if (e.Key == _settings.KeyPrevFolder)
            {
                NavigateFolder(-1);
                e.Handled = true;
            }
            else if (e.Key == _settings.KeyNextFolder)
            {
                NavigateFolder(1);
                e.Handled = true;
            }
        }

        private bool _isSearchingFolder = false;

        private async void NavigateFolder(int offset)
        {
            if (string.IsNullOrEmpty(_currentDirectory) || _isSearchingFolder) return;

            _isSearchingFolder = true;
            FolderSearchingOverlay.Visibility = Visibility.Visible;

            try
            {
                string currentDir = _currentDirectory;
                string? nextImageFolder = await Task.Run(() => FileNavigator.FindNextImageFolder(currentDir, offset));
                
                if (!string.IsNullOrEmpty(nextImageFolder))
                {
                    LoadDirectory(nextImageFolder);
                }
            }
            finally
            {
                _isSearchingFolder = false;
                FolderSearchingOverlay.Visibility = Visibility.Collapsed;
            }
        }


        private void Navigate(int offset, bool forceSingleStep = false)
        {
            for (int i=0; i<4; i++) _slideshowRandomIndices[i] = -1;
            if (_playlist.Count == 0) return;

            int step = forceSingleStep ? 1 : _settings.MangaSplitCount;
            bool looped = false;

            if (offset > 0)
            {
                _currentIndex += step;
                if (_currentIndex >= _playlist.Count)
                {
                    _currentIndex = 0;
                    looped = true;
                }
            }
            else
            {
                _currentIndex -= step;
                if (_currentIndex < 0)
                {
                    int remainder = _playlist.Count % step;
                    _currentIndex = _playlist.Count - (remainder == 0 ? step : remainder);
                    if (_currentIndex < 0) _currentIndex = 0;
                    looped = true;
                }
            }

            if (looped)
            {
                ShowNotification("🔄 Looped to " + (offset > 0 ? "Start" : "End"));
            }

            _ = UpdateDisplayAsync();
        }

        private void ShowNotification(string message)
        {
            NotificationText.Text = message;
            NotificationOverlay.Visibility = Visibility.Visible;
            _notificationTimer.Stop();
            _notificationTimer.Start();
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
            if (MenuToggleManga != null)
            {
                MenuToggleManga.Text = _settings.MangaSplitCount == 1 ? "View Mode: Single" : _settings.MangaSplitCount == 2 ? "View Mode: Double" : "View Mode: Quad";
            }
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
                if (overwrite)
                {
                    StopAnimation();
                    foreach(var img in _pageImages) img.Source = null;
                }

                await Task.Run(() => ImageProcessor.SaveImage(sourcePath, destPath, targetExtension));
                
                if (overwrite) _ = UpdateDisplayAsync();
            }
            catch { }
        }

        private void MenuCrop_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasSelection) return;
            string sourcePath = CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;

            try
            {
                var (imgW, imgH) = ImageProcessor.GetImageSize(sourcePath);
                if (imgW == 0 || imgH == 0) return;

                FrameworkElement targetElement = Image1.Visibility == Visibility.Visible ? Image1 : Canvas1;
                    
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

                var ttv = SelectionRectangle.TransformToVisual(targetElement);
                var rectTopLeft = ttv.TransformPoint(new Windows.Foundation.Point(0, 0));
                var rectBottomRight = ttv.TransformPoint(new Windows.Foundation.Point(SelectionRectangle.Width, SelectionRectangle.Height));

                double cropX = (rectTopLeft.X - offsetX) * (imgW / imgDisplayWidth);
                double cropY = (rectTopLeft.Y - offsetY) * (imgH / imgDisplayHeight);
                double cropW = (rectBottomRight.X - rectTopLeft.X) * (imgW / imgDisplayWidth);
                double cropH = (rectBottomRight.Y - rectTopLeft.Y) * (imgH / imgDisplayHeight);

                cropX = Math.Max(0, Math.Min(cropX, imgW));
                cropY = Math.Max(0, Math.Min(cropY, imgH));
                cropW = Math.Max(1, Math.Min(cropW, imgW - cropX));
                cropH = Math.Max(1, Math.Min(cropH, imgH - cropY));

                var cropRect = new SKRectI((int)cropX, (int)cropY, (int)(cropX + cropW), (int)(cropY + cropH));

                StopAnimation();
                foreach(var img in _pageImages) img.Source = null;

                ImageProcessor.CropImage(sourcePath, cropRect);

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
                DefaultButton = ContentDialogButton.Primary,
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
                var (origW, origH) = ImageProcessor.GetImageSize(sourcePath);
                widthBox.Value = origW;
                heightBox.Value = origH;

                _isDialogOpen = true;
                var result = await dialog.ShowAsync();
                _isDialogOpen = false;

                if (result == ContentDialogResult.Primary)
                {
                    int newWidth = (int)widthBox.Value;
                    int newHeight = (int)heightBox.Value;
                    if (newWidth <= 0 || newHeight <= 0) return;

                    StopAnimation();
                    foreach(var img in _pageImages) img.Source = null;

                    await Task.Run(() => ImageProcessor.ResizeImage(sourcePath, newWidth, newHeight));
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



        private void MenuToggleManga_Click(object sender, RoutedEventArgs e)
        {
            _settings.MangaSplitCount = _settings.MangaSplitCount == 1 ? 2 : (_settings.MangaSplitCount == 2 ? 4 : 1);
            _settings.SaveMangaMode();
            _ = UpdateDisplayAsync();
        }

        private void MenuLayoutMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int mode))
            {
                _settings.QuadLayoutMode = mode;
                _settings.SaveMangaMode();
                if (_settings.MangaSplitCount == 4)
                {
                    _ = UpdateDisplayAsync();
                }
            }
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
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
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

            _isDialogOpen = true;
            var resultKb = await dialog.ShowAsync();
            _isDialogOpen = false;

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

        private async void OpenSlideshowDialogAsync()
        {
            if (_isSlideshowRunning)
            {
                StopSlideshow();
                return;
            }

            SlideshowFullscreen.IsChecked = _settings.SlideshowFullscreen;
            SlideshowRandom.IsChecked = _settings.SlideshowRandom;
            SlideshowLoop.IsChecked = _settings.SlideshowLoop;
            SlideshowNextFolder.IsChecked = _settings.SlideshowNextFolder;
            SlideshowInterval.Value = _settings.SlideshowInterval;

            // Temporarily disable all content controls to force focus to the dialog buttons (OK)
            SetSlideshowControlsEnabled(false);

            SlideshowDialog.XamlRoot = this.Content.XamlRoot;
            _isDialogOpen = true;
            await SlideshowDialog.ShowAsync();
            _isDialogOpen = false;
        }

        private void SlideshowDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            // Re-enable controls after a short delay to ensure focus stays on the OK button
            var restoreTimer = new DispatcherTimer();
            restoreTimer.Interval = TimeSpan.FromMilliseconds(100);
            restoreTimer.Tick += (s, e) =>
            {
                restoreTimer.Stop();
                SetSlideshowControlsEnabled(true);
            };
            restoreTimer.Start();
        }

        private void SetSlideshowControlsEnabled(bool enabled)
        {
            SlideshowFullscreen.IsEnabled = enabled;
            SlideshowRandom.IsEnabled = enabled;
            SlideshowLoop.IsEnabled = enabled;
            SlideshowNextFolder.IsEnabled = enabled;
            SlideshowCurrentFolderOnly.IsEnabled = enabled;
            SlideshowInterval.IsEnabled = enabled;
        }



        private Button? FindButtonByContent(DependencyObject parent, string content)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Button btn && btn.Content is string text && text == content)
                    return btn;

                var result = FindButtonByContent(child, content);
                if (result != null)
                    return result;
            }
            return null;
        }

        private DependencyObject? FindVisualChildByName(DependencyObject parent, string name)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.Name == name)
                    return child;

                var result = FindVisualChildByName(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }



        private void SlideshowDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            _settings.SlideshowFullscreen = SlideshowFullscreen.IsChecked ?? false;
            _settings.SlideshowRandom = SlideshowRandom.IsChecked ?? false;
            _settings.SlideshowLoop = SlideshowLoop.IsChecked ?? false;
            _settings.SlideshowNextFolder = SlideshowNextFolder.IsChecked ?? false;
            _settings.SlideshowInterval = SlideshowInterval.Value;
            _settings.SaveSlideshowSettings();

            StartSlideshow();
        }

        private void StartSlideshow()
        {
            _isSlideshowRunning = true;
            if (_settings.SlideshowFullscreen && !AppWindow.Presenter.Kind.Equals(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen))
            {
                AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
                AppTitleBar.Visibility = Visibility.Collapsed;
            }

            _slideshowTimer.Interval = TimeSpan.FromSeconds(_settings.SlideshowInterval);
            _slideshowTimer.Start();
            ShowNotification("自動再生 開始");
        }

        private void StopSlideshow()
        {
            _isSlideshowRunning = false;
            _slideshowTimer.Stop();
            for (int i=0; i<4; i++) _slideshowRandomIndices[i] = -1;
            ShowNotification("自動再生 停止");
        }

        private void SlideshowTimer_Tick(object? sender, object e)
        {
            if (_playlist == null || _playlist.Count == 0) return;

            if (_settings.SlideshowRandom)
            {
                int nextIdx;
                int splits = _settings.MangaSplitCount;
                if (_playlist.Count <= 1)
                {
                    nextIdx = 0;
                    for (int i=0; i<4; i++) _slideshowRandomIndices[i] = -1;
                }
                else
                {
                    do { nextIdx = _random.Next(_playlist.Count); } while (nextIdx == _currentIndex);
                    _slideshowRandomIndices[0] = nextIdx;
                    
                    if (splits > 1 && _playlist.Count >= splits)
                    {
                        for (int i = 1; i < splits; i++)
                        {
                            int r;
                            do {
                                r = _random.Next(_playlist.Count);
                            } while (r == nextIdx || _slideshowRandomIndices.Take(i).Contains(r) || r == _currentIndex);
                            _slideshowRandomIndices[i] = r;
                        }
                    }
                    else
                    {
                        for (int i=1; i<4; i++) _slideshowRandomIndices[i] = -1;
                    }
                }
                _currentIndex = nextIdx;
                _ = UpdateDisplayAsync();
            }
            else
            {
                int increment = _settings.MangaSplitCount;
                if (_currentIndex + increment >= _playlist.Count)
                {
                    if (_settings.SlideshowNextFolder)
                    {
                        NavigateFolder(1);
                    }
                    else if (_settings.SlideshowLoop)
                    {
                        _currentIndex = 0;
                        _ = UpdateDisplayAsync();
                    }
                    else
                    {
                        StopSlideshow();
                    }
                }
                else
                {
                    Navigate(1);
                }
            }
        }
    }
}
