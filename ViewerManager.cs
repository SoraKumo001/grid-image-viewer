using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace grid_image_viewer
{
    internal class ViewerManager
    {
        private readonly MainWindow _window;
        private readonly SettingsManager _settings;

        private PageRenderer[] _pages = new PageRenderer[] { new PageRenderer(), new PageRenderer(), new PageRenderer(), new PageRenderer() };
        private Grid[] _pageGrids;
        private Grid[] _prevContainers;
        private Grid[] _currentContainers;
        private Microsoft.UI.Xaml.Controls.Image[] _prevImages;
        private Microsoft.UI.Xaml.Controls.Image[] _pageImages;
        private SkiaSharp.Views.Windows.SKXamlCanvas[] _pageCanvases;
        private Microsoft.UI.Xaml.Controls.ProgressRing[] _pageLoadingRings;
        private Border[] _focusBorders;
        private DispatcherTimer _animationTimer;
        private DispatcherTimer _notificationTimer;
        private CancellationTokenSource? _displayCts;
        private int _cachedQuadLayout = 1;
        private int _focusedPageIndex = 0;
        private ResourceLoader _resourceLoader = new ResourceLoader();

        private Dictionary<string, byte[]> _imageCache = new Dictionary<string, byte[]>();
        private const int MAX_CACHE_SIZE = 20;

        public Dictionary<string, EditSession> PendingEdits => _window.ImageEditService.PendingEdits;
        public void AddPendingEdit(string path, SKBitmap bitmap) => _window.ImageEditService.AddPendingEdit(path, bitmap);

        public ViewerManager(MainWindow window, SettingsManager settings)
        {
            _window = window;
            _settings = settings;

            _pageGrids = new Grid[] { _window.PageGrid1, _window.PageGrid2, _window.PageGrid3, _window.PageGrid4 };
            _prevContainers = new Grid[] { _window.PrevContainer1, _window.PrevContainer2, _window.PrevContainer3, _window.PrevContainer4 };
            _currentContainers = new Grid[] { _window.CurrentContainer1, _window.CurrentContainer2, _window.CurrentContainer3, _window.CurrentContainer4 };
            _prevImages = new Microsoft.UI.Xaml.Controls.Image[] { _window.Image1_Prev, _window.Image2_Prev, _window.Image3_Prev, _window.Image4_Prev };
            _pageImages = new Microsoft.UI.Xaml.Controls.Image[] { _window.Image1, _window.Image2, _window.Image3, _window.Image4 };
            _pageCanvases = new SkiaSharp.Views.Windows.SKXamlCanvas[] { _window.Canvas1, _window.Canvas2, _window.Canvas3, _window.Canvas4 };
            _pageLoadingRings = new Microsoft.UI.Xaml.Controls.ProgressRing[] { _window.LoadingRing1, _window.LoadingRing2, _window.LoadingRing3, _window.LoadingRing4 };
            _focusBorders = new Border[] { _window.FocusBorder1, _window.FocusBorder2, _window.FocusBorder3, _window.FocusBorder4 };

            _animationTimer = new DispatcherTimer();
            _animationTimer.Interval = TimeSpan.FromMilliseconds(30);
            _animationTimer.Tick += AnimationTimer_Tick;

            _notificationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _notificationTimer.Tick += (s, e) =>
            {
                _notificationTimer.Stop();
                _window.NotificationOverlay.Visibility = Visibility.Collapsed;
            };
        }

        public PageRenderer[] Pages => _pages;
        public Microsoft.UI.Xaml.Controls.Image[] PageImages => _pageImages;

        public async Task UpdateDisplayAsync()
        {
            _focusedPageIndex = 0;
            if (_window.Playlist.Count == 0 || _window.CurrentIndex < 0 || _window.CurrentIndex >= _window.Playlist.Count) return;

            if (_window.IsGridMode)
            {
                _window.ImageScrollViewer.Visibility = Visibility.Collapsed;
                _window.ImageGridView.Visibility = Visibility.Visible;
                StopAnimation();
                _window.GridManager.StartGridAnimation();

                _window.ImageGridView.SelectedIndex = _window.CurrentIndex;
                _window.ImageGridView.ScrollIntoView(_window.ImageGridView.SelectedItem);
                _window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    if (_window.ImageGridView.SelectedItem != null)
                    {
                        var container = _window.ImageGridView.ContainerFromItem(_window.ImageGridView.SelectedItem) as Microsoft.UI.Xaml.Controls.GridViewItem;
                        if (container != null)
                        {
                            container.Focus(FocusState.Programmatic);
                        }
                        else
                        {
                            _window.ImageGridView.Focus(FocusState.Programmatic);
                        }
                    }
                });
                return;
            }
            else
            {
                _window.ImageScrollViewer.Visibility = Visibility.Visible;
                _window.ImageGridView.Visibility = Visibility.Collapsed;
                _window.GridManager.StopGridAnimation();
                _window.RootGrid.Focus(FocusState.Programmatic);
            }

            StopAnimation();
            UpdateStretch();
            _displayCts?.Cancel();
            _displayCts?.Dispose();
            _displayCts = new CancellationTokenSource();
            var token = _displayCts.Token;

            try
            {
                int splitCount = _settings.MangaSplitCount;
                int remaining = _window.Playlist.Count - _window.CurrentIndex;
                int effectiveSplitCount = Math.Max(1, Math.Min(splitCount, remaining));

                // Layout Configuration
                // Layout Configuration
                _cachedQuadLayout = await Task.Run(() => GetEffectiveQuadLayout());
                UpdateLayoutGrid(splitCount, effectiveSplitCount, _cachedQuadLayout);

                var currentFiles = new List<string>();
                var loadTasks = new List<Task>();
                for (int i = 0; i < effectiveSplitCount; i++)
                {
                    int indexToLoad = -1;
                    if (i == 0) indexToLoad = _window.CurrentIndex;
                    else
                    {
                        if (_window.SlideshowManager.IsSlideshowRunning && _settings.SlideshowRandom)
                        {
                            if (_window.SlideshowManager.SlideshowRandomIndices[i] != -1)
                                indexToLoad = _window.SlideshowManager.SlideshowRandomIndices[i];
                            else if (_window.CurrentIndex + i < _window.Playlist.Count)
                                indexToLoad = _window.CurrentIndex + i;
                        }
                        else if (_window.CurrentIndex + i < _window.Playlist.Count)
                        {
                            indexToLoad = _window.CurrentIndex + i;
                        }
                    }

                    if (indexToLoad != -1)
                    {
                        currentFiles.Add(_window.Playlist[indexToLoad]);
                        loadTasks.Add(LoadPageAsync(_window.Playlist[indexToLoad], _pageImages[i], _pageCanvases[i], _pageLoadingRings[i], i, token));
                    }
                }

                var keysToRemove = PendingEdits.Keys.Where(k => !currentFiles.Contains(k)).ToList();
                foreach (var k in keysToRemove)
                {
                    PendingEdits[k].Dispose();
                    PendingEdits.Remove(k);
                }

                try
                {
                    await Task.WhenAll(loadTasks);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                for (int i = effectiveSplitCount; i < 4; i++)
                {
                    _pageImages[i].Source = null;
                    _pages[i].Reset();
                    _pageCanvases[i].Invalidate();
                    _pageLoadingRings[i].IsActive = false;
                }

                _ = PreloadAroundAsync();
            }
            finally
            {
            }

            if (_pages.Any(p => p.IsAnimated))
            {
                int minInterval = 1000;
                for (int i = 0; i < 4; i++)
                {
                    if (_pages[i].IsAnimated && _pages[i].CurrentFrameDuration < minInterval)
                        minInterval = _pages[i].CurrentFrameDuration;
                }
                _animationTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(10, minInterval));
                _animationTimer.Start();
            }
        }

        private void UpdateLayoutGrid(int splitCount, int effectiveSplitCount, int currentQuadLayout)
        {
            bool uniformToFill = (_window.SlideshowManager.IsSlideshowRunning && _settings.SlideshowUniformToFill);

            // Reset alignments
            for (int i = 0; i < 4; i++)
            {
                _pageImages[i].HorizontalAlignment = HorizontalAlignment.Center;
                _pageImages[i].VerticalAlignment = VerticalAlignment.Center;
                _prevImages[i].HorizontalAlignment = HorizontalAlignment.Center;
                _prevImages[i].VerticalAlignment = VerticalAlignment.Center;
            }

            if (effectiveSplitCount == 1)
            {
                _window.Col0.Width = new GridLength(1, GridUnitType.Star);
                _window.Col1.Width = new GridLength(0); _window.Col2.Width = new GridLength(0); _window.Col3.Width = new GridLength(0);
                _window.Row0.Height = new GridLength(1, GridUnitType.Star); _window.Row1.Height = new GridLength(0);

                Grid.SetColumn(_window.PageGrid1, 0); Grid.SetRow(_window.PageGrid1, 0);
                Grid.SetColumnSpan(_window.PageGrid1, 4); Grid.SetRowSpan(_window.PageGrid1, 2);
                _window.PageGrid1.Visibility = Visibility.Visible;
                _window.PageGrid2.Visibility = Visibility.Collapsed;
                _window.PageGrid3.Visibility = Visibility.Collapsed;
                _window.PageGrid4.Visibility = Visibility.Collapsed;
                _window.Image1.HorizontalAlignment = HorizontalAlignment.Center;
                _window.Image1_Prev.HorizontalAlignment = HorizontalAlignment.Center;
            }
            else if (effectiveSplitCount == 2)
            {
                _window.Col0.Width = new GridLength(1, GridUnitType.Star);
                _window.Col1.Width = new GridLength(1, GridUnitType.Star);
                _window.Col2.Width = new GridLength(0); _window.Col3.Width = new GridLength(0);
                _window.Row0.Height = new GridLength(1, GridUnitType.Star); _window.Row1.Height = new GridLength(0);

                Grid.SetColumn(_window.PageGrid1, 1); Grid.SetRow(_window.PageGrid1, 0);
                Grid.SetColumnSpan(_window.PageGrid1, 1); Grid.SetRowSpan(_window.PageGrid1, 2);
                Grid.SetColumn(_window.PageGrid2, 0); Grid.SetRow(_window.PageGrid2, 0);
                Grid.SetColumnSpan(_window.PageGrid2, 1); Grid.SetRowSpan(_window.PageGrid2, 2);

                _window.PageGrid1.Visibility = Visibility.Visible;
                _window.PageGrid2.Visibility = Visibility.Visible;
                _window.PageGrid3.Visibility = Visibility.Collapsed;
                _window.PageGrid4.Visibility = Visibility.Collapsed;

                if (!uniformToFill)
                {
                    _window.Image1.HorizontalAlignment = HorizontalAlignment.Left;
                    _window.Image1_Prev.HorizontalAlignment = HorizontalAlignment.Left;
                    _window.Image2.HorizontalAlignment = HorizontalAlignment.Right;
                    _window.Image2_Prev.HorizontalAlignment = HorizontalAlignment.Right;
                }
            }
            else if (effectiveSplitCount == 3)
            {
                _window.PageGrid1.Visibility = Visibility.Visible;
                _window.PageGrid2.Visibility = Visibility.Visible;
                _window.PageGrid3.Visibility = Visibility.Visible;
                _window.PageGrid4.Visibility = Visibility.Collapsed;

                if (currentQuadLayout == 2) // Grid mode (1 top, 2 bottom)
                {
                    _window.Col0.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col1.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col2.Width = new GridLength(0); _window.Col3.Width = new GridLength(0);
                    _window.Row0.Height = new GridLength(1, GridUnitType.Star);
                    _window.Row1.Height = new GridLength(1, GridUnitType.Star);

                    Grid.SetColumn(_window.PageGrid1, 0); Grid.SetRow(_window.PageGrid1, 0);
                    Grid.SetColumnSpan(_window.PageGrid1, 2); Grid.SetRowSpan(_window.PageGrid1, 1);
                    Grid.SetColumn(_window.PageGrid2, 1); Grid.SetRow(_window.PageGrid2, 1);
                    Grid.SetColumnSpan(_window.PageGrid2, 1); Grid.SetRowSpan(_window.PageGrid2, 1);
                    Grid.SetColumn(_window.PageGrid3, 0); Grid.SetRow(_window.PageGrid3, 1);
                    Grid.SetColumnSpan(_window.PageGrid3, 1); Grid.SetRowSpan(_window.PageGrid3, 1);

                    if (!uniformToFill)
                    {
                        _window.Image1.VerticalAlignment = VerticalAlignment.Bottom;
                        _window.Image1_Prev.VerticalAlignment = VerticalAlignment.Bottom;

                        _window.Image2.HorizontalAlignment = HorizontalAlignment.Left;
                        _window.Image2.VerticalAlignment = VerticalAlignment.Top;
                        _window.Image2_Prev.HorizontalAlignment = HorizontalAlignment.Left;
                        _window.Image2_Prev.VerticalAlignment = VerticalAlignment.Top;

                        _window.Image3.HorizontalAlignment = HorizontalAlignment.Right;
                        _window.Image3.VerticalAlignment = VerticalAlignment.Top;
                        _window.Image3_Prev.HorizontalAlignment = HorizontalAlignment.Right;
                        _window.Image3_Prev.VerticalAlignment = VerticalAlignment.Top;
                    }
                }
                else // Horizontal 1x3
                {
                    _window.Col0.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col1.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col2.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col3.Width = new GridLength(0);
                    _window.Row0.Height = new GridLength(1, GridUnitType.Star); _window.Row1.Height = new GridLength(0);

                    Grid.SetColumn(_window.PageGrid1, 2); Grid.SetRow(_window.PageGrid1, 0); Grid.SetRowSpan(_window.PageGrid1, 2); Grid.SetColumnSpan(_window.PageGrid1, 1);
                    Grid.SetColumn(_window.PageGrid2, 1); Grid.SetRow(_window.PageGrid2, 0); Grid.SetRowSpan(_window.PageGrid2, 2); Grid.SetColumnSpan(_window.PageGrid2, 1);
                    Grid.SetColumn(_window.PageGrid3, 0); Grid.SetRow(_window.PageGrid3, 0); Grid.SetRowSpan(_window.PageGrid3, 2); Grid.SetColumnSpan(_window.PageGrid3, 1);
                }
            }
            else // 4
            {
                _window.PageGrid1.Visibility = Visibility.Visible;
                _window.PageGrid2.Visibility = Visibility.Visible;
                _window.PageGrid3.Visibility = Visibility.Visible;
                _window.PageGrid4.Visibility = Visibility.Visible;

                if (currentQuadLayout == 1 || currentQuadLayout == 0) // Horizontal 1x4
                {

                    _window.Col0.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col1.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col2.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col3.Width = new GridLength(1, GridUnitType.Star);
                    _window.Row0.Height = new GridLength(1, GridUnitType.Star); _window.Row1.Height = new GridLength(0);

                    Grid.SetColumn(_window.PageGrid1, 3); Grid.SetRow(_window.PageGrid1, 0); Grid.SetRowSpan(_window.PageGrid1, 2); Grid.SetColumnSpan(_window.PageGrid1, 1);
                    Grid.SetColumn(_window.PageGrid2, 2); Grid.SetRow(_window.PageGrid2, 0); Grid.SetRowSpan(_window.PageGrid2, 2); Grid.SetColumnSpan(_window.PageGrid2, 1);
                    Grid.SetColumn(_window.PageGrid3, 1); Grid.SetRow(_window.PageGrid3, 0); Grid.SetRowSpan(_window.PageGrid3, 2); Grid.SetColumnSpan(_window.PageGrid3, 1);
                    Grid.SetColumn(_window.PageGrid4, 0); Grid.SetRow(_window.PageGrid4, 0); Grid.SetRowSpan(_window.PageGrid4, 2); Grid.SetColumnSpan(_window.PageGrid4, 1);
                }
                else // Grid 2x2
                {
                    _window.Col0.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col1.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col2.Width = new GridLength(0); _window.Col3.Width = new GridLength(0);
                    _window.Row0.Height = new GridLength(1, GridUnitType.Star);
                    _window.Row1.Height = new GridLength(1, GridUnitType.Star);

                    Grid.SetColumn(_window.PageGrid1, 1); Grid.SetRow(_window.PageGrid1, 0); Grid.SetRowSpan(_window.PageGrid1, 1); Grid.SetColumnSpan(_window.PageGrid1, 1);
                    Grid.SetColumn(_window.PageGrid2, 0); Grid.SetRow(_window.PageGrid2, 0); Grid.SetRowSpan(_window.PageGrid2, 1); Grid.SetColumnSpan(_window.PageGrid2, 1);
                    Grid.SetColumn(_window.PageGrid3, 1); Grid.SetRow(_window.PageGrid3, 1); Grid.SetRowSpan(_window.PageGrid3, 1); Grid.SetColumnSpan(_window.PageGrid3, 1);
                    Grid.SetColumn(_window.PageGrid4, 0); Grid.SetRow(_window.PageGrid4, 1); Grid.SetRowSpan(_window.PageGrid4, 1); Grid.SetColumnSpan(_window.PageGrid4, 1);

                    if (!uniformToFill)
                    {
                        _window.Image1.HorizontalAlignment = HorizontalAlignment.Left;
                        _window.Image1_Prev.HorizontalAlignment = HorizontalAlignment.Left;
                        _window.Image2.HorizontalAlignment = HorizontalAlignment.Right;
                        _window.Image2_Prev.HorizontalAlignment = HorizontalAlignment.Right;
                        _window.Image3.HorizontalAlignment = HorizontalAlignment.Left;
                        _window.Image3_Prev.HorizontalAlignment = HorizontalAlignment.Left;
                        _window.Image4.HorizontalAlignment = HorizontalAlignment.Right;
                        _window.Image4_Prev.HorizontalAlignment = HorizontalAlignment.Right;

                        _window.Image1.VerticalAlignment = VerticalAlignment.Bottom;
                        _window.Image1_Prev.VerticalAlignment = VerticalAlignment.Bottom;
                        _window.Image2.VerticalAlignment = VerticalAlignment.Bottom;
                        _window.Image2_Prev.VerticalAlignment = VerticalAlignment.Bottom;
                        _window.Image3.VerticalAlignment = VerticalAlignment.Top;
                        _window.Image3_Prev.VerticalAlignment = VerticalAlignment.Top;
                        _window.Image4.VerticalAlignment = VerticalAlignment.Top;
                        _window.Image4_Prev.VerticalAlignment = VerticalAlignment.Top;
                    }
                }
            }
        }

        private async Task PreloadAroundAsync()
        {
            var items = _window.Playlist;
            if (items.Count == 0) return;

            var indicesToPreload = new List<int>();
            int splitCount = _settings.MangaSplitCount;

            for (int i = 1; i <= splitCount; i++)
            {
                int prevIdx = (_window.CurrentIndex - i) % items.Count;
                if (prevIdx < 0) prevIdx += items.Count;
                indicesToPreload.Add(prevIdx);
            }

            for (int i = 0; i < 2 * splitCount; i++)
            {
                int nextIdx = (_window.CurrentIndex + splitCount + i) % items.Count;
                if (nextIdx < 0) nextIdx += items.Count;
                indicesToPreload.Add(nextIdx);
            }

            foreach (var idx in indicesToPreload)
            {
                var path = items[idx];
                lock (_imageCache) { if (_imageCache.ContainsKey(path)) continue; }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var bytes = await System.IO.File.ReadAllBytesAsync(path);
                        lock (_imageCache)
                        {
                            if (_imageCache.Count >= MAX_CACHE_SIZE)
                            {
                                var firstKey = _imageCache.Keys.First();
                                _imageCache.Remove(firstKey);
                            }
                            _imageCache[path] = bytes;
                        }
                    }
                    catch { }
                });
            }
        }

        private async Task LoadPageAsync(string filePath, Microsoft.UI.Xaml.Controls.Image imageCtrl, SkiaSharp.Views.Windows.SKXamlCanvas canvasCtrl, Microsoft.UI.Xaml.Controls.ProgressRing loadingRing, int pageIndex, CancellationToken token)
        {
            // Prepare previous image for crossfade or to prevent blackout
            if (imageCtrl.Visibility == Visibility.Visible && imageCtrl.Source != null)
            {
                _prevImages[pageIndex].Source = imageCtrl.Source;
            }
            else if (canvasCtrl.Visibility == Visibility.Visible)
            {
                try
                {
                    var bitmap = _pages[pageIndex].EditedBitmap ?? _pages[pageIndex].Bitmap;
                    if (bitmap != null)
                    {
                        using var snapshot = SKImage.FromBitmap(bitmap);
                        if (snapshot != null)
                        {
                            var bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                            using var ms = new System.IO.MemoryStream();
                            await Task.Run(() =>
                            {
                                using var data = snapshot.Encode(SKEncodedImageFormat.Bmp, 100);
                                if (data != null) data.SaveTo(ms);
                            });
                            ms.Position = 0;
                            await bitmapImage.SetSourceAsync(ms.AsRandomAccessStream());
                            _prevImages[pageIndex].Source = bitmapImage;
                        }
                    }
                }
                catch
                {
                    // Bitmap may have been disposed by an edit operation - safe to skip crossfade
                    _prevImages[pageIndex].Source = null;
                }
            }

            // Show previous image in the background container
            _prevContainers[pageIndex].Opacity = 1;

            // NOTE: We don't hide CurrentContainer here to prevent blackout.
            // It will be hidden/faded only when the new content is ready.

            _pages[pageIndex].CurrentFilePath = filePath;
            loadingRing.IsActive = !_window.SlideshowManager.IsSlideshowRunning;

            try
            {
                if (PendingEdits.TryGetValue(filePath, out var session))
                {
                    _pages[pageIndex].Reset();
                    _pages[pageIndex].CurrentFilePath = filePath;
                    _pages[pageIndex].EditedBitmap = session.Current;
                    _pages[pageIndex].FrameCount = 1;

                    imageCtrl.Visibility = Visibility.Collapsed;
                    canvasCtrl.Visibility = Visibility.Visible;
                    canvasCtrl.Invalidate();

                    if (_window.SlideshowManager.IsSlideshowRunning && _settings.SlideshowCrossfade)
                    {
                        _currentContainers[pageIndex].Opacity = 0;
                        StartCrossfade(pageIndex);
                    }
                    else
                    {
                        _currentContainers[pageIndex].Opacity = 1;
                        _prevContainers[pageIndex].Opacity = 0;
                        if (pageIndex == 0) UpdateMetadataPanel();
                    }
                    return;
                }

                var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
                bool useSkia = ext == ".webp" || ext == ".gif" || ext == ".avis";

                if (useSkia)
                {
                    // Load Skia content in background
                    await Task.Run(() =>
                    {
                        var tempRenderer = new PageRenderer();
                        tempRenderer.LoadSkia(filePath, token);
                        if (!token.IsCancellationRequested)
                        {
                            _pages[pageIndex].Reset();
                            _pages[pageIndex].CurrentFilePath = filePath;
                            _pages[pageIndex].Data = tempRenderer.Data;
                            _pages[pageIndex].Codec = tempRenderer.Codec;
                            _pages[pageIndex].Bitmap = tempRenderer.Bitmap;
                            _pages[pageIndex].FrameCount = tempRenderer.FrameCount;
                            _pages[pageIndex].CurrentFrame = tempRenderer.CurrentFrame;
                            _pages[pageIndex].PriorFrame = tempRenderer.PriorFrame;
                            _pages[pageIndex].CurrentFrameDuration = tempRenderer.CurrentFrameDuration;
                        }
                    });

                    if (token.IsCancellationRequested) return;

                    imageCtrl.Visibility = Visibility.Collapsed;
                    canvasCtrl.Visibility = Visibility.Visible;
                    canvasCtrl.Invalidate();
                }
                else
                {
                    byte[]? cachedBytes = null;
                    lock (_imageCache) { _imageCache.TryGetValue(filePath, out cachedBytes); }

                    Microsoft.UI.Xaml.Media.Imaging.BitmapImage? bitmapImage = null;

                    if (cachedBytes != null)
                    {
                        using var ms = new System.IO.MemoryStream(cachedBytes);
                        bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                        await bitmapImage.SetSourceAsync(ms.AsRandomAccessStream());
                    }
                    else
                    {
                        try
                        {
                            using var stream = System.IO.File.OpenRead(filePath);
                            bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                            await bitmapImage.SetSourceAsync(stream.AsRandomAccessStream());
                        }
                        catch
                        {
                            var bmpBytes = await Task.Run(() => ImageProcessor.DecodeToBmpBytes(filePath));
                            if (bmpBytes != null)
                            {
                                bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                                using var ms = new System.IO.MemoryStream(bmpBytes);
                                await bitmapImage.SetSourceAsync(ms.AsRandomAccessStream());
                            }
                        }
                    }

                    if (token.IsCancellationRequested) return;

                    _pages[pageIndex].Reset();
                    _pages[pageIndex].CurrentFilePath = filePath;
                    canvasCtrl.Visibility = Visibility.Collapsed;
                    imageCtrl.Visibility = Visibility.Visible;
                    imageCtrl.Source = bitmapImage;
                }

                if (_window.SlideshowManager.IsSlideshowRunning && _settings.SlideshowCrossfade)
                {
                    // For crossfade, we need to start from Opacity 0
                    _currentContainers[pageIndex].Opacity = 0;
                    StartCrossfade(pageIndex);
                }
                else
                {
                    _currentContainers[pageIndex].Opacity = 1;
                    _prevContainers[pageIndex].Opacity = 0;
                    if (pageIndex == 0) UpdateMetadataPanel();
                }
            }
            catch { }
            finally
            {
                loadingRing.IsActive = false;
            }
        }

        public void StopAnimation()
        {
            _animationTimer.Stop();
        }

        public void UpdateStretch()
        {
            var stretch = (_window.SlideshowManager.IsSlideshowRunning && _settings.SlideshowUniformToFill)
                ? Microsoft.UI.Xaml.Media.Stretch.UniformToFill
                : (Microsoft.UI.Xaml.Media.Stretch)_settings.ImageStretchMode;

            int stretchMode = (int)stretch;

            for (int i = 0; i < 4; i++)
            {
                _pageImages[i].Stretch = stretch;
                _prevImages[i].Stretch = stretch;
                _pages[i].StretchMode = stretchMode;
                _pageCanvases[i].Invalidate();
            }
        }

        private void AnimationTimer_Tick(object? sender, object e)
        {
            _animationTimer.Stop();

            int minInterval = 1000;
            bool anyAnimated = false;

            for (int i = 0; i < 4; i++)
            {
                if (_pages[i].IsAnimated)
                {
                    int interval = _pages[i].AdvanceFrame();
                    if (interval < minInterval) minInterval = interval;
                    anyAnimated = true;
                    _pageCanvases[i].Invalidate();
                }
            }

            if (anyAnimated)
            {
                // Ensure interval is at least 10ms to prevent CPU saturation
                _animationTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(10, minInterval));
                _animationTimer.Start();
            }
        }

        private int GetEffectiveQuadLayout()
        {
            int layout = _settings.QuadLayoutMode;
            if (_settings.MangaSplitCount != 4 || layout != 0) return layout;

            int wideCount = 0;
            int tallCount = 0;
            for (int i = 0; i < 4; i++)
            {
                int indexToLoad = -1;
                if (i == 0) indexToLoad = _window.CurrentIndex;
                else
                {
                    if (_window.SlideshowManager.IsSlideshowRunning && _settings.SlideshowRandom)
                    {
                        if (_window.SlideshowManager.SlideshowRandomIndices[i] != -1)
                            indexToLoad = _window.SlideshowManager.SlideshowRandomIndices[i];
                        else if (_window.CurrentIndex + i < _window.Playlist.Count)
                            indexToLoad = _window.CurrentIndex + i;
                    }
                    else if (_window.CurrentIndex + i < _window.Playlist.Count)
                    {
                        indexToLoad = _window.CurrentIndex + i;
                    }
                }

                if (indexToLoad != -1 && indexToLoad < _window.Playlist.Count)
                {
                    try
                    {
                        var path = _window.Playlist[indexToLoad];
                        int w, h;
                        if (PendingEdits.TryGetValue(path, out var session))
                        {
                            w = session.Current?.Width ?? 0;
                            h = session.Current?.Height ?? 0;
                        }
                        else
                        {
                            (w, h) = ImageProcessor.GetImageSize(path);
                        }

                        if (w > 0 && h > 0)
                        {
                            if ((double)w / h > 1.2) wideCount++;
                            else tallCount++;
                        }
                    }
                    catch { }
                }
            }
            if (wideCount == 0 && tallCount == 0) return 1;
            return wideCount >= tallCount ? 2 : 1;
        }

        public void PaintCanvas(int index, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SkiaSharp.SKColors.Transparent);

            int hAlign = 1; // Center
            int vAlign = 1; // Center

            int remaining = _window.Playlist.Count - _window.CurrentIndex;
            int splitCount = _settings.MangaSplitCount;
            int effectiveSplitCount = Math.Max(1, Math.Min(splitCount, remaining));

            if (effectiveSplitCount == 2)
            {
                hAlign = index == 0 ? 0 : 2; // Index 0 (Right) -> Left-aligned, Index 1 (Left) -> Right-aligned
            }
            else if (effectiveSplitCount == 3)
            {
                if (_cachedQuadLayout == 2) // Grid mode (1 top, 2 bottom)
                {
                    if (index == 0) { hAlign = 1; vAlign = 2; } // Top center (Bottom-aligned)
                    else if (index == 1) { hAlign = 2; vAlign = 0; } // Bottom right (Right/Top-aligned)
                    else if (index == 2) { hAlign = 0; vAlign = 0; } // Bottom left (Left/Top-aligned)
                }
            }
            else if (effectiveSplitCount == 4)
            {
                if (_cachedQuadLayout == 2) // Grid 2x2
                {
                    hAlign = (index == 0 || index == 2) ? 0 : 2;
                    vAlign = (index == 0 || index == 1) ? 2 : 0;
                }
            }

            _pages[index].Paint(canvas, e.Info, hAlign, vAlign);
        }

        public async void NavigateFolder(int offset)
        {
            if (string.IsNullOrEmpty(_window.CurrentDirectory) || _window.IsSearchingFolder) return;

            _window.IsSearchingFolder = true;
            _window.FolderSearchingOverlay.Visibility = Visibility.Visible;

            try
            {
                string currentDir = _window.CurrentDirectory;
                string? nextImageFolder = await Task.Run(() => FileNavigator.FindNextImageFolder(currentDir, offset));

                if (!string.IsNullOrEmpty(nextImageFolder))
                {
                    bool includeSiblings = _window.SlideshowManager.IsSlideshowRunning && _settings.SlideshowIncludeSiblings;
                    _window.LoadDirectory(nextImageFolder, includeSiblings: includeSiblings);
                }
            }
            finally
            {
                _window.IsSearchingFolder = false;
                _window.FolderSearchingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        public void Navigate(int offset, bool forceSingleStep = false)
        {
            for (int i = 0; i < 4; i++) _window.SlideshowManager.SlideshowRandomIndices[i] = -1;
            if (_window.Playlist.Count == 0) return;

            int step = forceSingleStep ? 1 : _settings.MangaSplitCount;
            bool looped = false;

            int newIndex = _window.CurrentIndex;
            if (offset > 0)
            {
                newIndex += step;
                if (newIndex >= _window.Playlist.Count)
                {
                    newIndex = 0;
                    looped = true;
                }
            }
            else
            {
                newIndex -= step;
                if (newIndex < 0)
                {
                    int remainder = _window.Playlist.Count % step;
                    newIndex = _window.Playlist.Count - (remainder == 0 ? step : remainder);
                    if (newIndex < 0) newIndex = 0;
                    looped = true;
                }
            }
            _window.CurrentIndex = newIndex;

            if (looped)
            {
                string resKey = offset > 0 ? "Notification_LoopedStart" : "Notification_LoopedEnd";
                ShowNotification(_resourceLoader.GetString(resKey));
            }

            _ = UpdateDisplayAsync();
        }

        public void ShowNotification(string message)
        {
            _window.NotificationText.Text = message;
            _window.NotificationOverlay.Visibility = Visibility.Visible;
            _notificationTimer.Stop();
            _notificationTimer.Start();
        }

        public void ToggleMetadataPanel(bool cycle = true)
        {
            if (_window.MetadataPanel.Visibility == Visibility.Visible)
            {
                int total = 0;
                int splitCount = _settings.MangaSplitCount;
                for (int i = 0; i < splitCount; i++)
                {
                    if (_window.CurrentIndex + i < _window.Playlist.Count) total++;
                }

                if (total > 1 && cycle)
                {
                    _focusedPageIndex++;
                    if (_focusedPageIndex >= total)
                    {
                        _focusedPageIndex = 0;
                        _window.MetadataPanel.Visibility = Visibility.Collapsed;
                        UpdateFocusBorders();
                    }
                    else
                    {
                        UpdateMetadataPanel();
                    }
                }
                else
                {
                    _window.MetadataPanel.Visibility = Visibility.Collapsed;
                    UpdateFocusBorders();
                }
            }
            else
            {
                _focusedPageIndex = 0;
                _window.MetadataPanel.Visibility = Visibility.Visible;
                UpdateMetadataPanel();
            }
        }

        private void UpdateFocusBorders()
        {
            bool panelVisible = _window.MetadataPanel.Visibility == Visibility.Visible;
            for (int i = 0; i < 4; i++)
            {
                _focusBorders[i].Visibility = (panelVisible && i == _focusedPageIndex) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public void HandlePointerMoved(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_window.MetadataPanel.Visibility != Visibility.Visible || _window.Playlist.Count == 0) return;

            var point = e.GetCurrentPoint(_window.PagesGrid).Position;
            int splitCount = _settings.MangaSplitCount;

            int hoveredIndex = -1;
            for (int i = 0; i < splitCount; i++)
            {
                if (_window.CurrentIndex + i >= _window.Playlist.Count) break;

                var grid = _pageGrids[i];
                if (grid.Visibility == Visibility.Visible)
                {
                    try
                    {
                        var transform = grid.TransformToVisual(_window.PagesGrid);
                        var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, grid.ActualWidth, grid.ActualHeight));

                        if (bounds.Contains(point))
                        {
                            hoveredIndex = i;
                            break;
                        }
                    }
                    catch { }
                }
            }

            if (hoveredIndex != -1 && hoveredIndex != _focusedPageIndex)
            {
                _focusedPageIndex = hoveredIndex;
                UpdateMetadataPanel();
            }
        }

        public void UpdateMetadataPanel()
        {
            if (_window.MetadataPanel.Visibility != Visibility.Visible || _window.Playlist.Count == 0)
            {
                UpdateFocusBorders();
                return;
            }

            int index = _window.CurrentIndex + _focusedPageIndex;
            if (index < 0 || index >= _window.Playlist.Count)
            {
                _window.MetadataPanel.Visibility = Visibility.Collapsed;
                UpdateFocusBorders();
                return;
            }

            UpdateFocusBorders();

            int total = 0;
            int splitCount = _settings.MangaSplitCount;
            for (int i = 0; i < splitCount; i++)
            {
                if (_window.CurrentIndex + i < _window.Playlist.Count) total++;
            }

            string title = _resourceLoader.GetString("Metadata_Title");
            if (string.IsNullOrEmpty(title)) title = "IMAGE INFORMATION";

            if (total > 1)
            {
                _window.TxtMetaTitle.Text = $"{title} ({_focusedPageIndex + 1}/{total})";
            }
            else
            {
                _window.TxtMetaTitle.Text = title;
            }

            string filePath = _window.Playlist[index];
            var meta = MetadataService.GetMetadata(filePath);

            _window.TxtMetaFileName.Text = meta.FileName;
            _window.TxtMetaDimensions.Text = meta.Dimensions;
            _window.TxtMetaFileSize.Text = meta.FileSize;

            if (meta.HasExif)
            {
                _window.ExifDivider.Visibility = Visibility.Visible;
                _window.ExifGrid.Visibility = Visibility.Visible;
                _window.TxtMetaCamera.Text = $"{meta.Make} {meta.Model}".Trim();
                _window.TxtMetaLens.Text = meta.LensModel ?? "-";
                _window.TxtMetaSettings.Text = $"{meta.FNumber}  {meta.ExposureTime}  ISO {meta.Iso}  {meta.FocalLength}".Trim();
                _window.TxtMetaDate.Text = meta.DateTaken ?? "-";
            }
            else
            {
                _window.ExifDivider.Visibility = Visibility.Collapsed;
                _window.ExifGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void StartCrossfade(int pageIndex)
        {
            var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();

            var animIn = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(_settings.SlideshowCrossfadeDuration),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animIn, _currentContainers[pageIndex]);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animIn, "Opacity");
            sb.Children.Add(animIn);

            var animOut = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(_settings.SlideshowCrossfadeDuration),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animOut, _prevContainers[pageIndex]);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animOut, "Opacity");
            sb.Children.Add(animOut);

            sb.Completed += (s, e) =>
            {
                _prevContainers[pageIndex].Opacity = 0;
            };

            sb.Begin();

            // 画像が切り替わったのでメタデータも更新（パネルが開いている場合のみ）
            if (pageIndex == 0) UpdateMetadataPanel();
        }



        public void Dispose()
        {
            StopAnimation();
            _displayCts?.Cancel();
            _displayCts?.Dispose();
        }
    }
}
