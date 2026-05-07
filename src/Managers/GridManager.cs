using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Models;
using quick_image_viewer.ViewModels;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
namespace quick_image_viewer.Managers
{
    internal class GridManager : IGridManager
    {
        private readonly IMainView _window;
        private readonly ISettingsManager _settings;

        private ScrollViewer? _gridScrollViewer;
        private DispatcherTimer? _gridAnimationTimer;
        private DispatcherTimer? _resizeDebounceTimer;
        private int _gridDecodeSize = 300;
        private CancellationTokenSource? _gridCts;

        public GridManager(IMainView window, ISettingsManager settings)
        {
            _window = window;
            _settings = settings;

            // UI access must be deferred until InitializeComponent is complete.
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                if (_window.ImageGridView != null)
                {
                    _window.ImageGridView.Loaded += (s, e) => SetupScrollListener();
                    _window.ImageGridView.ContainerContentChanging += GridView_ContainerContentChanging;
                }
            });

            _resizeDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _resizeDebounceTimer.Tick += (s, e) =>
            {
                _resizeDebounceTimer.Stop();
                if (_window != null)
                {
                    UpdateGridLayout();
                }
            };
        }

        private void GridView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue) return;

            var container = args.ItemContainer;
            if (container != null)
            {
                var visual = ElementCompositionPreview.GetElementVisual(container);
                if (visual.ImplicitAnimations == null)
                {
                    var compositor = visual.Compositor;
                    var animationGroup = compositor.CreateImplicitAnimationCollection();

                    // Smooth repositioning when grid layout changes
                    var offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
                    offsetAnimation.Target = "Offset";
                    offsetAnimation.InsertExpressionKeyFrame(1.0f, "this.FinalValue");
                    offsetAnimation.Duration = TimeSpan.FromMilliseconds(400);

                    animationGroup["Offset"] = offsetAnimation;
                    visual.ImplicitAnimations = animationGroup;
                }
            }
        }

        private void SetupScrollListener()
        {
            if (_gridScrollViewer != null) return;
            _gridScrollViewer = FindScrollViewer(_window.ImageGridView);
            if (_gridScrollViewer != null)
            {
                _gridScrollViewer.ViewChanged += (sender, args) => UpdatePageIndicatorFromScroll();
            }
        }

        private void UpdatePageIndicatorFromScroll()
        {
            if (!_window.IsGridMode || _gridScrollViewer == null) return;

            var wrapGrid = _window.ImageGridView.ItemsPanelRoot as ItemsWrapGrid;
            if (wrapGrid == null || wrapGrid.ItemWidth <= 0 || wrapGrid.ItemHeight <= 0) return;

            double viewportWidth = _gridScrollViewer.ViewportWidth;
            double viewportHeight = _gridScrollViewer.ViewportHeight;
            double verticalOffset = _gridScrollViewer.VerticalOffset;

            int columns = (int)Math.Floor(viewportWidth / wrapGrid.ItemWidth);
            if (columns <= 0) columns = 1;

            int firstVisibleRow = (int)Math.Floor(verticalOffset / wrapGrid.ItemHeight);
            int visibleRows = (int)Math.Ceiling(viewportHeight / wrapGrid.ItemHeight);
            int lastVisibleRow = firstVisibleRow + visibleRows;

            int lastVisibleIndex = (lastVisibleRow * columns);
            if (lastVisibleIndex > _window.Playlist.Count) lastVisibleIndex = _window.Playlist.Count;
            if (lastVisibleIndex < 1) lastVisibleIndex = 1;

            _window.ViewModel.OverrideDisplayIndex = lastVisibleIndex;
        }

        private ScrollViewer? FindScrollViewer(DependencyObject parent)
        {
            if (parent is ScrollViewer sv) return sv;
            int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                var result = FindScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        public async Task LoadThumbnailsAsync(CancellationToken token)
        {
            StopGridAnimation();
            foreach (var old in GridItems) old.DisposeCodec();

            // Determine decode resolution based on grid cell size (considering DPI scaling)
            int decodeSize = 300;
            if (_window.ImageGridView.ItemsPanelRoot is ItemsWrapGrid wg && wg.ItemWidth > 0)
            {
                decodeSize = (int)Math.Max(wg.ItemWidth, wg.ItemHeight) * 2; // Support Retina/High DPI
            }
            else
            {
                // Estimate from window size if layout is not yet determined
                double maxDim = Math.Max(_window.ImageGridView.ActualWidth, _window.ImageGridView.ActualHeight);
                if (maxDim > 0)
                {
                    int count = GridItems.Count;
                    int cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count)));
                    decodeSize = (int)(maxDim / cols) * 2;
                }
            }
            decodeSize = Math.Clamp(decodeSize, 300, 1200);
            _gridDecodeSize = decodeSize;

            var items = GridItems.ToList();
            var semaphore = new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount / 2));
            var tasks = items.Select(item => Task.Run(async () =>
            {
                if (token.IsCancellationRequested) return;
                // Add a small initial delay to prioritize main image loading
                await Task.Delay(100, token);
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

                // Recalculate layout and start animation after all items are loaded
                _window.DispatcherQueue.TryEnqueue(() =>
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
                bool mightBeAnimated = ext == ".webp" || ext == ".gif" || ext == ".avis" || ext == ".webm";
                bool isVideo = MediaHelper.IsVideo(item.FilePath);
                bool isArchive = ArchiveManager.IsArchive(item.FilePath);

                if (mightBeAnimated)
                {
                    byte[]? bytes = null;
                    if (ArchiveManager.IsArchivePath(item.FilePath))
                    {
                        var (arc, ent) = ArchiveManager.SplitArchivePath(item.FilePath);
                        bytes = ArchiveManager.GetEntryBytes(arc, ent);
                    }
                    else
                    {
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
                        _window.DispatcherQueue.TryEnqueue(() =>
                        {
                            if (!token.IsCancellationRequested)
                                item.AdvanceFrame(decodeSize, _window.DispatcherQueue);
                        });
                        return;
                    }

                    if (codec != null)
                    {
                        float scale = Math.Min((float)decodeSize / codec.Info.Width, (float)decodeSize / codec.Info.Height);
                        scale = Math.Min(scale, 1.0f);
                        var supportedDim = codec.GetScaledDimensions(scale);
                        var info = new SKImageInfo(supportedDim.Width, supportedDim.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                        var skBitmap = new SKBitmap(info);
                        if (codec.GetPixels(info, skBitmap.GetPixels()) == SKCodecResult.Success)
                        {
                            codec.Dispose();
                            skData.Dispose();
                            using var skBitmapToDispose = skBitmap;
                            ProcessDecodedBitmap(skBitmap, item, decodeSize, token);
                        }
                        else
                        {
                            skBitmap.Dispose();
                            codec.Dispose();
                            skData.Dispose();
                        }
                    }
                    else
                    {
                        skData.Dispose();
                    }
                }
                else if (isVideo)
                {
                    var softwareBitmap = await ImageProcessor.ExtractVideoThumbnailAsync(item.FilePath, (uint)decodeSize);
                    if (softwareBitmap != null)
                    {
                        _window.DispatcherQueue.TryEnqueue(async () =>
                        {
                            if (token.IsCancellationRequested) return;
                            var source = new SoftwareBitmapSource();
                            await source.SetBitmapAsync(softwareBitmap);
                            item.Thumbnail = source;
                        });
                    }
                }
                else if (isArchive)
                {
                    var archiveImages = await Task.Run(() => ArchiveManager.GetArchiveImages(item.FilePath, _settings.EnabledExtensions));
                    if (archiveImages.Count > 0)
                    {
                        SKBitmap? decoded = ImageProcessor.LoadThumbnail(archiveImages[0], decodeSize);
                        if (decoded != null)
                        {
                            using var skBitmapToDispose = decoded;
                            ProcessDecodedBitmap(decoded, item, decodeSize, token);
                        }
                    }
                }
                else
                {
                    SKBitmap? decoded = ImageProcessor.LoadThumbnail(item.FilePath, decodeSize);

                    using var skBitmapToDispose = decoded;
                    if (token.IsCancellationRequested) return;

                    ProcessDecodedBitmap(decoded, item, decodeSize, token);
                }
            }
            catch { }
            finally
            {
                _window.DispatcherQueue.TryEnqueue(() => item.IsLoading = false);
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

                    _window.DispatcherQueue.TryEnqueue(() =>
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
                _window.DispatcherQueue.TryEnqueue(async () =>
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

        public void StartGridAnimation()
        {
            if (_gridAnimationTimer != null) return;
            // No timer needed if there are no animated images
            if (!GridItems.Any(i => i.IsAnimated)) return;

            _gridAnimationTimer = new DispatcherTimer();
            _gridAnimationTimer.Interval = TimeSpan.FromMilliseconds(100);
            _gridAnimationTimer.Tick += GridAnimationTimer_Tick;
            _gridAnimationTimer.Start();
        }

        public void StopGridAnimation()
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
            if (!_window.IsGridMode) return;

            // Optimization: Only iterate over materialized (visible) containers in the GridView.
            // This avoids looping through thousands of items in large folders.
            if (_window.ImageGridView.ItemsPanelRoot is not ItemsWrapGrid wrapGrid) return;

            int count = VisualTreeHelper.GetChildrenCount(wrapGrid);
            for (int i = 0; i < count; i++)
            {
                var container = VisualTreeHelper.GetChild(wrapGrid, i) as Microsoft.UI.Xaml.Controls.Primitives.SelectorItem;
                if (container != null)
                {
                    var item = _window.ImageGridView.ItemFromContainer(container) as ImageItem;
                    if (item != null)
                    {
                        if (item.IsAnimated)
                        {
                            item.AdvanceFrame(_gridDecodeSize, _window.DispatcherQueue);
                        }
                        else if (item.Thumbnail == null && !item.IsLoading && item.RetryCount < 5)
                        {
                            // Retry if loading failed and the element is currently visible
                            item.RetryCount++;
                            item.IsLoading = true;
                            _ = Task.Run(() => LoadSingleThumbnailAsync(item, _gridDecodeSize, _gridCts?.Token ?? CancellationToken.None));
                        }
                    }
                }
            }
        }

        public void ImageGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ImageItem item)
            {
                int idx = _window.Playlist.IndexOf(item.FilePath);
                if (idx >= 0)
                {
                    if (ArchiveManager.IsArchive(item.FilePath) && !ArchiveManager.IsArchivePath(item.FilePath))
                    {
                        WeakReferenceMessenger.Default.Send(new LoadDirectoryMessage(item.FilePath));
                        return;
                    }
                    _window.CurrentIndex = idx;
                    _window.IsGridMode = false;
                    _ = _window.UpdateDisplayAsync();
                }
            }
        }

        public void ImageGridView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _resizeDebounceTimer?.Stop();
            _resizeDebounceTimer?.Start();
        }

        public void UpdateGridLayout()
        {
            if (_window.ImageGridView.ItemsPanelRoot is not ItemsWrapGrid wrapGrid) return;

            int totalItems = GridItems.Count;
            if (totalItems == 0) return;

            double W = _window.ImageGridView.ActualWidth - _window.ImageGridView.Padding.Left - _window.ImageGridView.Padding.Right - 24;
            double H = _window.ImageGridView.ActualHeight - _window.ImageGridView.Padding.Top - _window.ImageGridView.Padding.Bottom - 8;
            if (W <= 0 || H <= 0) return;

            // Calculate the mode of aspect ratios (using the most frequent ratio as the base)
            var ratios = GridItems.Select(x => x.AspectRatio).Where(r => r > 0).ToList();
            double modeAspect = 1.0;
            if (ratios.Count > 0)
            {
                // Group by rounding to 2 decimal places and adopt the most frequent ratio
                modeAspect = ratios
                    .GroupBy(r => Math.Round(r, 2))
                    .OrderByDescending(g => g.Count())
                    .First()
                    .Average();
            }

            // Search for optimal column count: choose the combination that maximizes the image area within the cells
            double bestArea = 0;
            int bestCols = 1;

            // Optimization: Limit the number of column possibilities to check.
            // For large collections, we won't be fitting them all on one screen anyway.
            int maxColsToTest = Math.Min(totalItems, 100);

            for (int cols = 1; cols <= maxColsToTest; cols++)
            {
                int rows = (int)Math.Ceiling((double)totalItems / cols);
                double cellW = W / cols;
                double cellH = H / rows;

                double imgW, imgH;
                if (modeAspect >= cellW / cellH)
                {
                    imgW = cellW;
                    imgH = cellW / modeAspect;
                }
                else
                {
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

            // Fallback: use traditional scroll mode if cells are too small
            int bestRows = (int)Math.Ceiling((double)totalItems / bestCols);
            double finalCellW = Math.Floor(W / bestCols);
            double finalCellH = Math.Floor(H / bestRows);
            double minDim = Math.Min(finalCellW, finalCellH);

            if (minDim < 120)
            {
                // Scroll mode: determine column count based on width, height follows aspect ratio
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

        public ObservableCollection<ImageItem> GridItems { get; private set; } = new ObservableCollection<ImageItem>();
        System.Collections.Generic.IList<ImageItem> IGridManager.GridItems => GridItems;


        public void UpdateGridItems(bool forceFullUpdate)
        {
            if (!_window.IsGridMode && !forceFullUpdate)
            {
                if (GridItems.Count > 1) GridItems.Clear();
                if (GridItems.Count == 0 && _window.ViewModel.CurrentIndex >= 0 && _window.ViewModel.CurrentIndex < _window.ViewModel.Playlist.Count)
                {
                    GridItems.Add(new ImageItem { FilePath = _window.ViewModel.Playlist[_window.ViewModel.CurrentIndex], IsLoading = true });
                }
                return;
            }

            if (GridItems.Count == _window.ViewModel.Playlist.Count && !forceFullUpdate) return;
            if (_window.ViewModel.Playlist.Count > 1000 && !_window.IsGridMode) return;

            var newList = new ObservableCollection<ImageItem>();
            foreach (var f in _window.ViewModel.Playlist) newList.Add(new ImageItem { FilePath = f, IsLoading = true });

            GridItems = newList;
            _window.ImageGridView.ItemsSource = GridItems;
            RefreshThumbnails();
            _window.DispatcherQueue.TryEnqueue(() => UpdatePageIndicatorFromScroll());
        }

        public void RefreshThumbnails()
        {
            _gridCts?.Cancel();
            _gridCts?.Dispose();
            _gridCts = new CancellationTokenSource();
            _ = LoadThumbnailsAsync(_gridCts.Token);
        }

        public void Dispose()
        {
            StopGridAnimation();
            _gridCts?.Cancel();
            _gridCts?.Dispose();
        }
    }
}
