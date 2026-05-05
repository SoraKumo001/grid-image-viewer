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
        private readonly ViewerLayoutManager _layoutManager;
        private readonly ViewerImageLoader _imageLoader;
        private readonly ViewerCacheManager _cacheManager;

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
        private int _cachedQuadLayout = 1;
        private bool _lastIsSlideshowRunning = false;
        private ResourceLoader _resourceLoader = new ResourceLoader();

        public ViewerManager(MainWindow window, SettingsManager settings)
        {
            _window = window;
            _settings = settings;
            _layoutManager = new ViewerLayoutManager(settings);
            _imageLoader = new ViewerImageLoader(window, settings);
            _cacheManager = new ViewerCacheManager(window, settings);

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

                _cachedQuadLayout = await Task.Run(() => _layoutManager.GetEffectiveQuadLayout(currentIndex, playlistSnapshot));

                // Prepare the INACTIVE buffer
                int targetBufferIdx = _window.ViewerControl.InactiveBufferIndex;
                _layoutManager.UpdateLayoutGrid(_window, splitCount, effectiveSplitCount, _cachedQuadLayout, targetBufferIdx);

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
                        loadTasks.Add(_imageLoader.LoadPageIntoBufferAsync(_window.Playlist[indexToLoad], targetImages[i], targetCanvases[i], targetLoadingRings[i], targetPages[i], i, token, _cacheManager));
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
