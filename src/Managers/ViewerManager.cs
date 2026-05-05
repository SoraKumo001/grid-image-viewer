using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Services;
using quick_image_viewer.ViewModels;
using quick_image_viewer.Views.Controls;
using SkiaSharp.Views.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace quick_image_viewer.Managers
{
    internal class ViewerManager : IViewerManager, IRecipient<NavigationMessage>, IRecipient<FolderNavigationMessage>, IRecipient<ZoomMessage>, IRecipient<ToggleMetadataMessage>
    {
        private readonly IMainView _window;
        private readonly ISettingsManager _settings;
        private readonly IViewerLayoutManager _layoutManager;
        private readonly IViewerImageLoader _imageLoader;
        private readonly IViewerCacheManager _cacheManager;

        // Double-buffered PageRenderers to support Skia crossfade
        private PageRenderer[][] _pagesBuffer = new PageRenderer[][]
        {
            new PageRenderer[] { new PageRenderer(), new PageRenderer(), new PageRenderer(), new PageRenderer() },
            new PageRenderer[] { new PageRenderer(), new PageRenderer(), new PageRenderer(), new PageRenderer() }
        };

        // These arrays will point to the CURRENT buffer's elements for general logic
        private Microsoft.UI.Xaml.FrameworkElement[] _pageGrids;
        private ViewerPageControl[] _pageControls;

        private CancellationTokenSource? _displayCts;
        private int _cachedQuadLayout = 1;
        private bool _lastIsSlideshowRunning = false;
        private int _lastMangaSplitCount = -1;
        private int _lastEffectiveSplitCount = -1;
        private int _lastCachedQuadLayout = -1;
        private ResourceLoader _resourceLoader = new ResourceLoader();

        public ViewerManager(IMainView window, ISettingsManager settings)
        {
            _window = window;
            _settings = settings;
            _layoutManager = new ViewerLayoutManager(settings);
            _imageLoader = new ViewerImageLoader(window, settings);
            _cacheManager = new ViewerCacheManager(window.State, settings);

            // Initialize buffer references
            int idx = _window.ViewerControl.CurrentBufferIndex;
            _pageGrids = _window.ViewerControl.PageControlsBuffer[idx];
            _pageControls = _window.ViewerControl.PageControlsBuffer[idx];

            WeakReferenceMessenger.Default.Register<NavigationMessage>(this);
            WeakReferenceMessenger.Default.Register<FolderNavigationMessage>(this);
            WeakReferenceMessenger.Default.Register<ZoomMessage>(this);
            WeakReferenceMessenger.Default.Register<ToggleMetadataMessage>(this);
        }

        private void UpdateBufferReferences()
        {
            int idx = _window.ViewerControl.CurrentBufferIndex;
            _pageGrids = _window.ViewerControl.PageControlsBuffer[idx];
            _pageControls = _window.ViewerControl.PageControlsBuffer[idx];
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
        public ViewerPageControl[] PageControls => _pageControls;

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

            if (_window.Playlist.Count == 0 || _window.CurrentIndex < 0 || _window.CurrentIndex >= _window.Playlist.Count)
            {
                // Clear display if playlist is empty
                for (int b = 0; b < 2; b++)
                {
                    _window.ViewerControl.PagesGrids[b].Opacity = 0;
                    _window.ViewerControl.PagesGrids[b].Visibility = Visibility.Collapsed;
                }
                _window.UpdatePageIndicator();
                return;
            }

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
            try { _window.AnimationService.StopCrossfade(); } catch { }
            UpdateStretch();

            int splitCount = _settings.MangaSplitCount;
            int remaining = _window.Playlist.Count - _window.CurrentIndex;
            int effectiveSplitCount = Math.Max(1, Math.Min(splitCount, remaining));

            // Recalculate layout for quad mode if needed
            if (splitCount == 4 && _settings.QuadLayoutMode == 0)
            {
                _cachedQuadLayout = _layoutManager.GetEffectiveQuadLayout(_window.CurrentIndex, _window.Playlist, _window.RootGrid.ActualWidth, _window.RootGrid.ActualHeight);
            }
            else if (splitCount == 4)
            {
                _cachedQuadLayout = _settings.QuadLayoutMode;
            }
            else
            {
                _cachedQuadLayout = 1; // Default to horizontal for others
            }

            // 表示内容が変わっていないかチェック（ちらつき防止）
            var currentPaths = _pagesBuffer[_window.ViewerControl.CurrentBufferIndex]
                .Take(effectiveSplitCount)
                .Select(p => p.CurrentFilePath)
                .ToList();

            var nextPaths = new List<string>();
            int maxIndexInView = _window.CurrentIndex;
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
                {
                    nextPaths.Add(_window.Playlist[indexToLoad]);
                    maxIndexInView = Math.Max(maxIndexInView, indexToLoad);
                }
            }
            _window.ViewModel.OverrideDisplayIndex = maxIndexInView + 1;

            bool isSlideshowRunning = _window.SlideshowManager.IsSlideshowRunning;
            bool stateChanged = isSlideshowRunning != _lastIsSlideshowRunning;
            bool splitChanged = splitCount != _lastMangaSplitCount || effectiveSplitCount != _lastEffectiveSplitCount || _cachedQuadLayout != _lastCachedQuadLayout;

            _lastIsSlideshowRunning = isSlideshowRunning;
            _lastMangaSplitCount = splitCount;
            _lastEffectiveSplitCount = effectiveSplitCount;
            _lastCachedQuadLayout = _cachedQuadLayout;

            if (!stateChanged && !splitChanged && currentPaths.SequenceEqual(nextPaths))
            {
                // Ensure current buffer is visible and inactive is hidden
                _window.ViewerControl.CurrentBuffer.Opacity = 1;
                _window.ViewerControl.CurrentBuffer.Visibility = Visibility.Visible;
                _window.ViewerControl.InactiveBuffer.Opacity = 0;
                _window.ViewerControl.InactiveBuffer.Visibility = Visibility.Collapsed;

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

                double windowWidth = _window.Bounds.Width;
                double windowHeight = _window.Bounds.Height;
                _cachedQuadLayout = await Task.Run(() => _layoutManager.GetEffectiveQuadLayout(currentIndex, playlistSnapshot, windowWidth, windowHeight));

                if (isSlideshowRunning)
                {
                    // --- SLIDESHOW MODE: Double-Buffered with Crossfade ---
                    int targetBufferIdx = _window.ViewerControl.InactiveBufferIndex;
                    _layoutManager.UpdateLayoutGrid(
                        _window.ViewerControl.ColsBuffer[targetBufferIdx],
                        _window.ViewerControl.RowsBuffer[targetBufferIdx],
                        _window.ViewerControl.PageControlsBuffer[targetBufferIdx],
                        splitCount,
                        effectiveSplitCount,
                        _cachedQuadLayout,
                        _window.State.IsSlideshowRunning);

                    var targetControls = _window.ViewerControl.PageControlsBuffer[targetBufferIdx];
                    var targetPages = _pagesBuffer[targetBufferIdx];

                    var currentFiles = new List<string>();
                    var loadTasks = new List<Task>();
                    for (int i = 0; i < effectiveSplitCount; i++)
                    {
                        int indexToLoad = -1;
                        if (i == 0) indexToLoad = _window.CurrentIndex;
                        else
                        {
                            if (_settings.SlideshowRandom && _window.SlideshowManager.SlideshowRandomIndices[i] != -1)
                                indexToLoad = _window.SlideshowManager.SlideshowRandomIndices[i];
                            else if (_window.CurrentIndex + i < _window.Playlist.Count)
                                indexToLoad = _window.CurrentIndex + i;
                        }

                        if (indexToLoad != -1)
                        {
                            currentFiles.Add(_window.Playlist[indexToLoad]);
                            loadTasks.Add(_imageLoader.LoadPageIntoBufferAsync(_window.Playlist[indexToLoad], targetControls[i], targetPages[i], i, token, _cacheManager));
                        }
                    }

                    _window.ImageEditService.CleanupSessions(currentFiles);

                    try { await Task.WhenAll(loadTasks); }
                    catch (OperationCanceledException) { return; }

                    for (int i = effectiveSplitCount; i < 4; i++)
                    {
                        targetControls[i].PageImage.Source = null;
                        targetControls[i].PageCanvas.Visibility = Visibility.Collapsed;
                        targetControls[i].LoadingRing.IsActive = false;
                        targetPages[i].Reset();
                    }

                    if (token.IsCancellationRequested) return;

                    var prevBuffer = _window.ViewerControl.CurrentBuffer;
                    var nextBuffer = _window.ViewerControl.InactiveBuffer;

                    if (_settings.SlideshowCrossfade)
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
                }
                else
                {
                    // --- NORMAL MODE: Direct Update for individual items ---
                    int targetBufferIdx = _window.ViewerControl.CurrentBufferIndex;
                    _layoutManager.UpdateLayoutGrid(
                        _window.ViewerControl.ColsBuffer[targetBufferIdx],
                        _window.ViewerControl.RowsBuffer[targetBufferIdx],
                        _window.ViewerControl.PageControlsBuffer[targetBufferIdx],
                        splitCount,
                        effectiveSplitCount,
                        _cachedQuadLayout,
                        _window.State.IsSlideshowRunning);

                    var targetControls = _window.ViewerControl.PageControlsBuffer[targetBufferIdx];
                    var targetPages = _pagesBuffer[targetBufferIdx];

                    // Ensure current buffer is fully visible and inactive is hidden
                    _window.ViewerControl.CurrentBuffer.Opacity = 1;
                    _window.ViewerControl.CurrentBuffer.Visibility = Visibility.Visible;
                    _window.ViewerControl.InactiveBuffer.Opacity = 0;
                    _window.ViewerControl.InactiveBuffer.Visibility = Visibility.Collapsed;

                    var currentFiles = new List<string>();
                    var loadTasks = new List<Task>();
                    for (int i = 0; i < effectiveSplitCount; i++)
                    {
                        int indexToLoad = -1;
                        if (i == 0) indexToLoad = _window.CurrentIndex;
                        else if (_window.CurrentIndex + i < _window.Playlist.Count)
                            indexToLoad = _window.CurrentIndex + i;

                        if (indexToLoad != -1)
                        {
                            string path = _window.Playlist[indexToLoad];
                            currentFiles.Add(path);

                            // Only load if path changed
                            if (targetPages[i].CurrentFilePath != path)
                            {
                                loadTasks.Add(_imageLoader.LoadPageIntoBufferAsync(path, targetControls[i], targetPages[i], i, token, _cacheManager));
                            }
                        }
                    }

                    for (int i = effectiveSplitCount; i < 4; i++)
                    {
                        targetControls[i].PageImage.Source = null;
                        targetControls[i].PageCanvas.Visibility = Visibility.Collapsed;
                        targetControls[i].LoadingRing.IsActive = false;
                        targetPages[i].Reset();
                    }

                    _window.ImageEditService.CleanupSessions(currentFiles);
                    UpdateBufferReferences();
                    _window.MetadataDisplayService.UpdateMetadataPanel();

                    // Do NOT await WhenAll here to allow individual updates.
                    // However, we should await the primary image (index 0) to ensure at least one image is ready if possible,
                    // or just let them all load independently for maximum responsiveness.
                    // User requested "individual updates", so let's fire and forget.
                    _ = Task.Run(async () =>
                    {
                        try { await Task.WhenAll(loadTasks); } catch { }
                        _window.DispatcherQueue.TryEnqueue(() => _window.AnimationService.StartAnimation());
                    });
                }

                _ = _cacheManager.PreloadAroundAsync(_window.CurrentIndex, _window.Playlist.ToList(), _settings.MangaSplitCount);
                _ = _cacheManager.PreloadFoldersAsync(_window.CurrentDirectory);
            }
            finally
            {
            }

            _window.AnimationService.StartAnimation();
            _window.UpdatePageIndicator();
        }





        public void StopAnimation() => _window.AnimationService.StopAnimation();

        public void InvalidatePage(int index) => _pageControls[index].PageCanvas.Invalidate();

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
                    var controls = _window.ViewerControl.PageControlsBuffer[b];
                    for (int i = 0; i < 4; i++)
                    {
                        controls[i].PageImage.Stretch = stretch;
                        controls[i].PageCanvas.Invalidate();
                    }
                }
                for (int b = 0; b < 2; b++)
                {
                    for (int i = 0; i < 4; i++) _pagesBuffer[b][i].StretchMode = stretchMode;
                }
            }
            catch { }
        }

        public void HandleWindowSizeChanged(double width, double height)
        {
            if (_window.Playlist.Count == 0 || _window.IsGridMode) return;
            if (_settings.MangaSplitCount != 4 || _settings.QuadLayoutMode != 0) return;

            // Run on background thread to avoid UI lag for metadata fetching (though sizes are usually cached)
            _ = Task.Run(() =>
            {
                int newLayout = _layoutManager.GetEffectiveQuadLayout(_window.CurrentIndex, _window.Playlist, width, height);
                if (newLayout != _cachedQuadLayout)
                {
                    _cachedQuadLayout = newLayout;
                    _window.DispatcherQueue.TryEnqueue(() =>
                    {
                        int currentBufferIdx = _window.ViewerControl.CurrentBufferIndex;
                        int splitCount = _settings.MangaSplitCount;
                        int remaining = _window.Playlist.Count - _window.CurrentIndex;
                        int effectiveSplitCount = Math.Max(1, Math.Min(splitCount, remaining));

                        _layoutManager.UpdateLayoutGrid(
                            _window.ViewerControl.ColsBuffer[currentBufferIdx],
                            _window.ViewerControl.RowsBuffer[currentBufferIdx],
                            _window.ViewerControl.PageControlsBuffer[currentBufferIdx],
                            splitCount,
                            effectiveSplitCount,
                            _cachedQuadLayout,
                            _window.State.IsSlideshowRunning);
                        for (int i = 0; i < 4; i++) InvalidatePage(i);
                    });
                }
            });
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



        public void Dispose()
        {
            _displayCts?.Cancel();
            _cacheManager.CancelPreloads();
            foreach (var p in _pagesBuffer[0]) p.Reset();
            foreach (var p in _pagesBuffer[1]) p.Reset();
        }
    }
}
