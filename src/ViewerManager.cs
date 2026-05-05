using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using SkiaSharp.Views.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace grid_image_viewer
{
    internal class ViewerManager : IRecipient<NavigationMessage>, IRecipient<FolderNavigationMessage>, IRecipient<ZoomMessage>, IRecipient<ToggleMetadataMessage>
    {
        private readonly MainWindow _window;
        private readonly SettingsManager _settings;

        // Double-buffered PageRenderers to support Skia crossfade
        private PageRenderer[][] _pagesBuffer = new PageRenderer[][]
        {
            new PageRenderer[] { new PageRenderer(), new PageRenderer(), new PageRenderer(), new PageRenderer() },
            new PageRenderer[] { new PageRenderer(), new PageRenderer(), new PageRenderer(), new PageRenderer() }
        };

        // These arrays will point to the CURRENT buffer's elements for general logic
        private Grid[] _pageGrids;
        private Microsoft.UI.Xaml.Controls.Image[] _pageImages;
        private SkiaSharp.Views.Windows.SKXamlCanvas[] _pageCanvases;
        private Microsoft.UI.Xaml.Controls.ProgressRing[] _pageLoadingRings;
        private Border[] _focusBorders;

        private CancellationTokenSource? _displayCts;
        private CancellationTokenSource? _folderPreloadCts;
        private string? _cachedNextFolder;
        private string? _cachedPrevFolder;
        private List<string>? _cachedNextPlaylist;
        private List<string>? _cachedPrevPlaylist;
        private string? _lastPreloadedDirectory;
        private int _cachedQuadLayout = 1;
        private bool _lastIsSlideshowRunning = false;
        private ResourceLoader _resourceLoader = new ResourceLoader();

        private Dictionary<string, byte[]> _imageCache = new Dictionary<string, byte[]>();
        private const int MAX_CACHE_SIZE = 20;

        public ViewerManager(MainWindow window, SettingsManager settings)
        {
            _window = window;
            _settings = settings;

            // Initialize buffer references
            int idx = _window.ViewerControl.CurrentBufferIndex;
            _pageGrids = _window.ViewerControl.PageGridsBuffer[idx];
            _pageImages = _window.ViewerControl.PageImagesBuffer[idx];
            _pageCanvases = _window.ViewerControl.PageCanvasesBuffer[idx];
            _pageLoadingRings = _window.ViewerControl.PageLoadingRingsBuffer[idx];
            _focusBorders = _window.ViewerControl.FocusBordersBuffer[idx];

            WeakReferenceMessenger.Default.Register<NavigationMessage>(this);
            WeakReferenceMessenger.Default.Register<FolderNavigationMessage>(this);
            WeakReferenceMessenger.Default.Register<ZoomMessage>(this);
            WeakReferenceMessenger.Default.Register<ToggleMetadataMessage>(this);
        }

        private void UpdateBufferReferences()
        {
            int idx = _window.ViewerControl.CurrentBufferIndex;
            _pageGrids = _window.ViewerControl.PageGridsBuffer[idx];
            _pageImages = _window.ViewerControl.PageImagesBuffer[idx];
            _pageCanvases = _window.ViewerControl.PageCanvasesBuffer[idx];
            _pageLoadingRings = _window.ViewerControl.PageLoadingRingsBuffer[idx];
            _focusBorders = _window.ViewerControl.FocusBordersBuffer[idx];
        }

        public void Receive(NavigationMessage message) => Navigate(message.Offset, message.ForceSingleStep);
        public void Receive(FolderNavigationMessage message) => NavigateFolder(message.Offset);

        public void Receive(ZoomMessage message)
        {
            if (message.Factor == 0)
            {
                _window.ImageScrollViewer.ChangeView(null, null, 1.0f);
            }
            else
            {
                _window.ImageScrollViewer.ChangeView(null, null, _window.ImageScrollViewer.ZoomFactor * message.Factor);
            }
        }

        public void Receive(ToggleMetadataMessage message) => ToggleMetadataPanel();

        public PageRenderer[] Pages => _pagesBuffer[_window.ViewerControl.CurrentBufferIndex];
        public Microsoft.UI.Xaml.Controls.Image[] PageImages => _pageImages;

        public string? GetPathForPage(int index)
        {
            if (index < 0 || index >= 4) return null;
            return Pages[index].CurrentFilePath;
        }

        public void HandlePointerMoved(PointerRoutedEventArgs e)
        {
            // Logic handled by InputHandler now, but kept for compatibility if needed
        }

        public async Task UpdateDisplayAsync()
        {
            if (!_window.DispatcherQueue.HasThreadAccess)
            {
                _window.DispatcherQueue.TryEnqueue(async () => await UpdateDisplayAsync());
                return;
            }

            if (_window.Playlist.Count == 0 || _window.CurrentIndex < 0 || _window.CurrentIndex >= _window.Playlist.Count) return;

            if (_window.IsGridMode)
            {
                _window.UpdateGridItems(true);
                _window.ImageScrollViewer.Visibility = Visibility.Collapsed;
                _window.ImageGridView.Visibility = Visibility.Visible;
                _window.AnimationService.StopAnimation();
                _window.GridManager.StartGridAnimation();

                _window.ImageGridView.SelectedIndex = _window.CurrentIndex;
                _window.ImageGridView.ScrollIntoView(_window.ImageGridView.SelectedItem);
                _window.UpdatePageIndicator();
                _window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    if (_window.ImageGridView.SelectedItem != null)
                    {
                        var container = _window.ImageGridView.ContainerFromItem(_window.ImageGridView.SelectedItem) as Microsoft.UI.Xaml.Controls.GridViewItem;
                        if (container != null) container.Focus(FocusState.Programmatic);
                        else _window.ImageGridView.Focus(FocusState.Programmatic);
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

            try { _window.AnimationService.StopAnimation(); } catch { }
            UpdateStretch();

            int splitCount = _settings.MangaSplitCount;
            int remaining = _window.Playlist.Count - _window.CurrentIndex;
            int effectiveSplitCount = Math.Max(1, Math.Min(splitCount, remaining));

            // 表示内容が変わっていないかチェック（ちらつき防止）
            var currentPaths = _pagesBuffer[_window.ViewerControl.CurrentBufferIndex]
                .Take(effectiveSplitCount)
                .Select(p => p.CurrentFilePath)
                .ToList();

            var nextPaths = new List<string>();
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
                if (indexToLoad >= 0 && indexToLoad < _window.Playlist.Count)
                    nextPaths.Add(_window.Playlist[indexToLoad]);
            }

            bool isSlideshowRunning = _window.SlideshowManager.IsSlideshowRunning;
            bool stateChanged = isSlideshowRunning != _lastIsSlideshowRunning;
            _lastIsSlideshowRunning = isSlideshowRunning;

            if (!stateChanged && currentPaths.SequenceEqual(nextPaths))
            {
                // バッファ自体は表示されていることを保証
                _window.ViewerControl.CurrentBuffer.Opacity = 1;
                _window.ViewerControl.CurrentBuffer.Visibility = Visibility.Visible;

                _window.UpdatePageIndicator();
                _window.MetadataDisplayService.UpdateMetadataPanel();
                _window.AnimationService.StartAnimation();
                return;
            }

            _displayCts?.Cancel();
            _displayCts?.Dispose();
            _displayCts = new CancellationTokenSource();
            var token = _displayCts.Token;

            try
            {
                int currentIndex = _window.CurrentIndex;
                var playlistSnapshot = _window.Playlist.ToList();

                _cachedQuadLayout = await Task.Run(() => GetEffectiveQuadLayout(currentIndex, playlistSnapshot));

                // Prepare the INACTIVE buffer
                int targetBufferIdx = _window.ViewerControl.InactiveBufferIndex;
                UpdateLayoutGrid(splitCount, effectiveSplitCount, _cachedQuadLayout, targetBufferIdx);

                var targetImages = _window.ViewerControl.PageImagesBuffer[targetBufferIdx];
                var targetCanvases = _window.ViewerControl.PageCanvasesBuffer[targetBufferIdx];
                var targetLoadingRings = _window.ViewerControl.PageLoadingRingsBuffer[targetBufferIdx];
                var targetPages = _pagesBuffer[targetBufferIdx];

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
                        loadTasks.Add(LoadPageIntoBufferAsync(_window.Playlist[indexToLoad], targetImages[i], targetCanvases[i], targetLoadingRings[i], targetPages[i], i, token));
                    }
                }

                _window.ImageEditService.CleanupSessions(currentFiles);

                try { await Task.WhenAll(loadTasks); }
                catch (OperationCanceledException) { return; }

                for (int i = effectiveSplitCount; i < 4; i++)
                {
                    targetImages[i].Source = null;
                    targetCanvases[i].Visibility = Visibility.Collapsed;
                    targetLoadingRings[i].IsActive = false;
                    targetPages[i].Reset();
                }

                if (token.IsCancellationRequested) return;

                // Trigger Crossfade between buffers
                var prevBuffer = _window.ViewerControl.CurrentBuffer;
                var nextBuffer = _window.ViewerControl.InactiveBuffer;

                if (_window.SlideshowManager.IsSlideshowRunning && _settings.SlideshowCrossfade)
                {
                    _window.ViewerControl.CurrentBufferIndex = targetBufferIdx;
                    UpdateBufferReferences();
                    _window.AnimationService.StartGridCrossfade(nextBuffer, prevBuffer);
                }
                else
                {
                    nextBuffer.Opacity = 1;
                    nextBuffer.Visibility = Visibility.Visible;
                    prevBuffer.Opacity = 0;
                    prevBuffer.Visibility = Visibility.Collapsed;
                    _window.ViewerControl.CurrentBufferIndex = targetBufferIdx;
                    UpdateBufferReferences();
                    _window.MetadataDisplayService.UpdateMetadataPanel();
                }

                _ = PreloadAroundAsync();
                _ = PreloadFoldersAsync();
            }
            finally
            {
            }

            _window.AnimationService.StartAnimation();
            _window.UpdatePageIndicator();
        }

        private void UpdateLayoutGrid(int splitCount, int effectiveSplitCount, int currentQuadLayout, int bufferIdx)
        {
            var cols = _window.ViewerControl.ColsBuffer[bufferIdx];
            var rows = _window.ViewerControl.RowsBuffer[bufferIdx];
            var pageGrids = _window.ViewerControl.PageGridsBuffer[bufferIdx];
            var images = _window.ViewerControl.PageImagesBuffer[bufferIdx];

            bool uniformToFill = (_window.SlideshowManager.IsSlideshowRunning && _settings.SlideshowUniformToFill);

            for (int i = 0; i < 4; i++)
            {
                images[i].HorizontalAlignment = HorizontalAlignment.Center;
                images[i].VerticalAlignment = VerticalAlignment.Center;
            }

            if (effectiveSplitCount == 1)
            {
                cols[0].Width = new GridLength(1, GridUnitType.Star);
                cols[1].Width = new GridLength(0); cols[2].Width = new GridLength(0); cols[3].Width = new GridLength(0);
                rows[0].Height = new GridLength(1, GridUnitType.Star); rows[1].Height = new GridLength(0);

                Grid.SetColumn(pageGrids[0], 0); Grid.SetRow(pageGrids[0], 0);
                Grid.SetColumnSpan(pageGrids[0], 4); Grid.SetRowSpan(pageGrids[0], 2);
                pageGrids[0].Visibility = Visibility.Visible;
                pageGrids[1].Visibility = Visibility.Collapsed;
                pageGrids[2].Visibility = Visibility.Collapsed;
                pageGrids[3].Visibility = Visibility.Collapsed;
            }
            else if (effectiveSplitCount == 2)
            {
                cols[0].Width = new GridLength(1, GridUnitType.Star);
                cols[1].Width = new GridLength(1, GridUnitType.Star);
                cols[2].Width = new GridLength(0); cols[3].Width = new GridLength(0);
                rows[0].Height = new GridLength(1, GridUnitType.Star); rows[1].Height = new GridLength(0);

                Grid.SetColumn(pageGrids[0], 1); Grid.SetRow(pageGrids[0], 0);
                Grid.SetColumnSpan(pageGrids[0], 1); Grid.SetRowSpan(pageGrids[0], 2);
                Grid.SetColumn(pageGrids[1], 0); Grid.SetRow(pageGrids[1], 0);
                Grid.SetColumnSpan(pageGrids[1], 1); Grid.SetRowSpan(pageGrids[1], 2);

                pageGrids[0].Visibility = Visibility.Visible;
                pageGrids[1].Visibility = Visibility.Visible;
                pageGrids[2].Visibility = Visibility.Collapsed;
                pageGrids[3].Visibility = Visibility.Collapsed;

                if (!uniformToFill)
                {
                    images[0].HorizontalAlignment = HorizontalAlignment.Left;
                    images[1].HorizontalAlignment = HorizontalAlignment.Right;
                }
            }
            else if (effectiveSplitCount == 3)
            {
                pageGrids[0].Visibility = Visibility.Visible;
                pageGrids[1].Visibility = Visibility.Visible;
                pageGrids[2].Visibility = Visibility.Visible;
                pageGrids[3].Visibility = Visibility.Collapsed;

                if (currentQuadLayout == 2)
                {
                    cols[0].Width = new GridLength(1, GridUnitType.Star);
                    cols[1].Width = new GridLength(1, GridUnitType.Star);
                    cols[2].Width = new GridLength(0); cols[3].Width = new GridLength(0);
                    rows[0].Height = new GridLength(1, GridUnitType.Star);
                    rows[1].Height = new GridLength(1, GridUnitType.Star);

                    Grid.SetColumn(pageGrids[0], 0); Grid.SetRow(pageGrids[0], 0);
                    Grid.SetColumnSpan(pageGrids[0], 2); Grid.SetRowSpan(pageGrids[0], 1);
                    Grid.SetColumn(pageGrids[1], 1); Grid.SetRow(pageGrids[1], 1);
                    Grid.SetColumnSpan(pageGrids[1], 1); Grid.SetRowSpan(pageGrids[1], 1);
                    Grid.SetColumn(pageGrids[2], 0); Grid.SetRow(pageGrids[2], 1);
                    Grid.SetColumnSpan(pageGrids[2], 1); Grid.SetRowSpan(pageGrids[2], 1);

                    if (!uniformToFill)
                    {
                        images[0].VerticalAlignment = VerticalAlignment.Bottom;
                        images[1].HorizontalAlignment = HorizontalAlignment.Left; images[1].VerticalAlignment = VerticalAlignment.Top;
                        images[2].HorizontalAlignment = HorizontalAlignment.Right; images[2].VerticalAlignment = VerticalAlignment.Top;
                    }
                }
                else
                {
                    cols[0].Width = new GridLength(1, GridUnitType.Star);
                    cols[1].Width = new GridLength(1, GridUnitType.Star);
                    cols[2].Width = new GridLength(1, GridUnitType.Star);
                    cols[3].Width = new GridLength(0);
                    rows[0].Height = new GridLength(1, GridUnitType.Star); rows[1].Height = new GridLength(0);

                    Grid.SetColumn(pageGrids[0], 2); Grid.SetRow(pageGrids[0], 0); Grid.SetRowSpan(pageGrids[0], 2); Grid.SetColumnSpan(pageGrids[0], 1);
                    Grid.SetColumn(pageGrids[1], 1); Grid.SetRow(pageGrids[1], 0); Grid.SetRowSpan(pageGrids[1], 2); Grid.SetColumnSpan(pageGrids[1], 1);
                    Grid.SetColumn(pageGrids[2], 0); Grid.SetRow(pageGrids[2], 0); Grid.SetRowSpan(pageGrids[2], 2); Grid.SetColumnSpan(pageGrids[2], 1);
                }
            }
            else
            {
                pageGrids[0].Visibility = Visibility.Visible;
                pageGrids[1].Visibility = Visibility.Visible;
                pageGrids[2].Visibility = Visibility.Visible;
                pageGrids[3].Visibility = Visibility.Visible;

                if (currentQuadLayout == 1 || currentQuadLayout == 0)
                {
                    cols[0].Width = new GridLength(1, GridUnitType.Star);
                    cols[1].Width = new GridLength(1, GridUnitType.Star);
                    cols[2].Width = new GridLength(1, GridUnitType.Star);
                    cols[3].Width = new GridLength(1, GridUnitType.Star);
                    rows[0].Height = new GridLength(1, GridUnitType.Star); rows[1].Height = new GridLength(0);

                    Grid.SetColumn(pageGrids[0], 3); Grid.SetRow(pageGrids[0], 0); Grid.SetRowSpan(pageGrids[0], 2); Grid.SetColumnSpan(pageGrids[0], 1);
                    Grid.SetColumn(pageGrids[1], 2); Grid.SetRow(pageGrids[1], 0); Grid.SetRowSpan(pageGrids[1], 2); Grid.SetColumnSpan(pageGrids[1], 1);
                    Grid.SetColumn(pageGrids[2], 1); Grid.SetRow(pageGrids[2], 0); Grid.SetRowSpan(pageGrids[2], 2); Grid.SetColumnSpan(pageGrids[2], 1);
                    Grid.SetColumn(pageGrids[3], 0); Grid.SetRow(pageGrids[3], 0); Grid.SetRowSpan(pageGrids[3], 2); Grid.SetColumnSpan(pageGrids[3], 1);
                }
                else
                {
                    cols[0].Width = new GridLength(1, GridUnitType.Star);
                    cols[1].Width = new GridLength(1, GridUnitType.Star);
                    cols[2].Width = new GridLength(0); cols[3].Width = new GridLength(0);
                    rows[0].Height = new GridLength(1, GridUnitType.Star);
                    rows[1].Height = new GridLength(1, GridUnitType.Star);

                    Grid.SetColumn(pageGrids[0], 1); Grid.SetRow(pageGrids[0], 0); Grid.SetRowSpan(pageGrids[0], 1); Grid.SetColumnSpan(pageGrids[0], 1);
                    Grid.SetColumn(pageGrids[1], 0); Grid.SetRow(pageGrids[1], 0); Grid.SetRowSpan(pageGrids[1], 1); Grid.SetColumnSpan(pageGrids[1], 1);
                    Grid.SetColumn(pageGrids[2], 1); Grid.SetRow(pageGrids[2], 1); Grid.SetRowSpan(pageGrids[2], 1); Grid.SetColumnSpan(pageGrids[2], 1);
                    Grid.SetColumn(pageGrids[3], 0); Grid.SetRow(pageGrids[3], 1); Grid.SetRowSpan(pageGrids[3], 1); Grid.SetColumnSpan(pageGrids[3], 1);

                    if (!uniformToFill)
                    {
                        images[0].HorizontalAlignment = HorizontalAlignment.Left; images[0].VerticalAlignment = VerticalAlignment.Bottom;
                        images[1].HorizontalAlignment = HorizontalAlignment.Right; images[1].VerticalAlignment = VerticalAlignment.Bottom;
                        images[2].HorizontalAlignment = HorizontalAlignment.Left; images[2].VerticalAlignment = VerticalAlignment.Top;
                        images[3].HorizontalAlignment = HorizontalAlignment.Right; images[3].VerticalAlignment = VerticalAlignment.Top;
                    }
                }
            }
        }

        private async Task LoadPageIntoBufferAsync(string filePath, Microsoft.UI.Xaml.Controls.Image imageCtrl, SkiaSharp.Views.Windows.SKXamlCanvas canvasCtrl, Microsoft.UI.Xaml.Controls.ProgressRing loadingRing, PageRenderer renderer, int pageIndex, CancellationToken token)
        {
            if (ArchiveManager.IsArchive(filePath) && !ArchiveManager.IsArchivePath(filePath))
            {
                _window.DispatcherQueue.TryEnqueue(() => _window.LoadDirectory(filePath));
                return;
            }

            renderer.CurrentFilePath = filePath;
            _window.DispatcherQueue.TryEnqueue(() => { loadingRing.IsActive = !_window.SlideshowManager.IsSlideshowRunning; });

            try
            {
                var session = _window.ImageEditService.GetSession(filePath);
                if (session != null)
                {
                    renderer.Reset();
                    renderer.CurrentFilePath = filePath;
                    renderer.EditedBitmap = session.Current;
                    renderer.FrameCount = 1;

                    imageCtrl.Visibility = Visibility.Collapsed;
                    canvasCtrl.Visibility = Visibility.Visible;
                    canvasCtrl.Invalidate();
                    return;
                }

                var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
                bool useSkia = ext == ".webp" || ext == ".gif" || ext == ".avis";

                if (useSkia)
                {
                    await Task.Run(() =>
                    {
                        var tempRenderer = new PageRenderer();
                        tempRenderer.LoadSkia(filePath, token);
                        if (!token.IsCancellationRequested)
                        {
                            renderer.Reset();
                            renderer.CurrentFilePath = filePath;
                            renderer.Data = tempRenderer.Data;
                            renderer.Codec = tempRenderer.Codec;
                            renderer.Bitmap = tempRenderer.Bitmap;
                            renderer.FrameCount = tempRenderer.FrameCount;
                            renderer.CurrentFrame = tempRenderer.CurrentFrame;
                            renderer.PriorFrame = tempRenderer.PriorFrame;
                            renderer.CurrentFrameDuration = tempRenderer.CurrentFrameDuration;
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
                        catch (Exception)
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

                    renderer.Reset();
                    renderer.CurrentFilePath = filePath;
                    canvasCtrl.Visibility = Visibility.Collapsed;
                    imageCtrl.Visibility = Visibility.Visible;
                    imageCtrl.Source = bitmapImage;
                }
            }
            catch { }
            finally { loadingRing.IsActive = false; }
        }

        public void StopAnimation() => _window.AnimationService.StopAnimation();

        internal void InvalidatePage(int index) => _pageCanvases[index].Invalidate();

        public void UpdateStretch()
        {
            try
            {
                var stretch = (_window.SlideshowManager.IsSlideshowRunning && _settings.SlideshowUniformToFill)
                    ? Microsoft.UI.Xaml.Media.Stretch.UniformToFill
                    : (Microsoft.UI.Xaml.Media.Stretch)_settings.ImageStretchMode;

                int stretchMode = (int)stretch;

                for (int b = 0; b < 2; b++)
                {
                    var images = _window.ViewerControl.PageImagesBuffer[b];
                    var canvases = _window.ViewerControl.PageCanvasesBuffer[b];
                    for (int i = 0; i < 4; i++)
                    {
                        images[i].Stretch = stretch;
                        canvases[i].Invalidate();
                    }
                }
                for (int b = 0; b < 2; b++)
                {
                    for (int i = 0; i < 4; i++) _pagesBuffer[b][i].StretchMode = stretchMode;
                }
            }
            catch { }
        }

        private int GetEffectiveQuadLayout(int currentIndex, List<string> playlist)
        {
            int layout = _settings.QuadLayoutMode;
            if (_settings.MangaSplitCount != 4 || layout != 0) return layout;

            int wideCount = 0;
            int tallCount = 0;
            for (int i = 0; i < 4; i++)
            {
                int indexToLoad = -1;
                if (i == 0) indexToLoad = currentIndex;
                else if (currentIndex + i < playlist.Count) indexToLoad = currentIndex + i;

                if (indexToLoad != -1 && indexToLoad < playlist.Count)
                {
                    try
                    {
                        var (w, h) = ImageProcessor.GetImageSize(playlist[indexToLoad]);
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

        public void PaintCanvas(int bufferIndex, int pageIndex, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SkiaSharp.SKColors.Transparent);

            int hAlign = 1; // Center
            int vAlign = 1; // Center

            int remaining = _window.Playlist.Count - _window.CurrentIndex;
            int splitCount = _settings.MangaSplitCount;
            int effectiveSplitCount = Math.Max(1, Math.Min(splitCount, remaining));

            if (effectiveSplitCount == 2) hAlign = pageIndex == 0 ? 0 : 2;
            else if (effectiveSplitCount == 3)
            {
                if (_cachedQuadLayout == 2)
                {
                    if (pageIndex == 0) { hAlign = 1; vAlign = 2; }
                    else if (pageIndex == 1) { hAlign = 2; vAlign = 0; }
                    else if (pageIndex == 2) { hAlign = 0; vAlign = 0; }
                }
            }
            else if (effectiveSplitCount == 4)
            {
                if (_cachedQuadLayout == 2)
                {
                    if (pageIndex == 0) { hAlign = 0; vAlign = 2; }
                    else if (pageIndex == 1) { hAlign = 2; vAlign = 2; }
                    else if (pageIndex == 2) { hAlign = 0; vAlign = 0; }
                    else if (pageIndex == 3) { hAlign = 2; vAlign = 0; }
                }
            }

            var renderer = _pagesBuffer[bufferIndex][pageIndex];
            lock (renderer)
            {
                renderer.Paint(canvas, e.Info, hAlign, vAlign);
            }
        }

        public void ToggleMetadataPanel(bool cycle = false) => _window.MetadataDisplayService.ToggleMetadataPanel(cycle);
        public void ShowNotification(string message) => _window.NotificationService.Show(message);
        public void Navigate(int offset, bool forceSingleStep) => _window.PlaylistManager.Navigate(offset, forceSingleStep);
        public void NavigateFolder(int offset) => _window.PlaylistManager.NavigateFolder(offset);

        private async Task PreloadAroundAsync()
        {
            var items = _window.Playlist;
            if (items.Count == 0) return;
            int splitCount = _settings.MangaSplitCount;
            var indicesToPreload = new List<int>();
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
                            if (_imageCache.Count >= MAX_CACHE_SIZE) _imageCache.Remove(_imageCache.Keys.First());
                            _imageCache[path] = bytes;
                        }
                    }
                    catch { }
                });
            }
        }

        private async Task PreloadFoldersAsync()
        {
            string currentDir = _window.CurrentDirectory;
            if (string.IsNullOrEmpty(currentDir) || currentDir == _lastPreloadedDirectory) return;
            _folderPreloadCts?.Cancel();
            _folderPreloadCts?.Dispose();
            _folderPreloadCts = new CancellationTokenSource();
            var token = _folderPreloadCts.Token;
            _lastPreloadedDirectory = currentDir;
            try
            {
                _cachedNextFolder = await Task.Run(() => FileNavigator.FindNextImageFolder(currentDir, 1, token), token);
                if (token.IsCancellationRequested) return;
                if (!string.IsNullOrEmpty(_cachedNextFolder)) _cachedNextPlaylist = await Task.Run(() => FolderDiscoveryService.GetInitialPlaylist(_cachedNextFolder), token);
                if (token.IsCancellationRequested) return;
                _cachedPrevFolder = await Task.Run(() => FileNavigator.FindNextImageFolder(currentDir, -1, token), token);
                if (token.IsCancellationRequested) return;
                if (!string.IsNullOrEmpty(_cachedPrevFolder)) _cachedPrevPlaylist = await Task.Run(() => FolderDiscoveryService.GetInitialPlaylist(_cachedPrevFolder), token);
            }
            catch { }
        }

        public void Dispose()
        {
            _displayCts?.Cancel();
            _folderPreloadCts?.Cancel();
            foreach (var p in _pagesBuffer[0]) p.Reset();
            foreach (var p in _pagesBuffer[1]) p.Reset();
        }
    }
}
