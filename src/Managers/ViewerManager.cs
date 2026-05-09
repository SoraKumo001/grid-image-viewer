using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using quick_image_viewer.Common;
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
    public partial class ViewerManager : IViewerManager, IRecipient<ZoomMessage>, IRecipient<ToggleMetadataMessage>
    {
        private readonly IMainView _window;
        private readonly ISettingsManager _settings;
        private readonly IViewerLayoutManager _layoutManager;
        private readonly IViewerImageLoader _imageLoader;
        private readonly IViewerCacheManager _cacheManager;

        // Double-buffered PageRenderers to support Skia crossfade
        private readonly PageRenderer[][] _pagesBuffer =
        [
            [new(), new(), new(), new()],
            [new(), new(), new(), new()]
        ];

        // These arrays will point to the CURRENT buffer's elements for general logic
        private Microsoft.UI.Xaml.FrameworkElement[] _pageGrids = Array.Empty<Microsoft.UI.Xaml.FrameworkElement>();
        private ViewerPageControl[] _pageControls = Array.Empty<ViewerPageControl>();

        private CancellationTokenSource? _displayCts;
        private int _cachedQuadLayout = 1;
        private bool _lastIsSlideshowRunning = false;
        private int _lastMangaSplitCount = -1;
        private int _lastEffectiveSplitCount = -1;
        private int _lastCachedQuadLayout = -1;
        private Microsoft.UI.Xaml.DispatcherTimer? _resizeTimer;
        private double _lastResizeWidth;
        private double _lastResizeHeight;
        private readonly ResourceLoader _resourceLoader = new();

        public ViewerManager(IMainView window, ISettingsManager settings, IViewerCacheManager cacheManager)
        {
            _window = window;
            _settings = settings;
            _layoutManager = new ViewerLayoutManager(settings);
            _imageLoader = new ViewerImageLoader(window, settings);
            _cacheManager = cacheManager;

            // Do NOT initialize buffer references here as UI might not be ready
            // They will be initialized on first use in UpdateDisplayAsync or other methods

            WeakReferenceMessenger.Default.Register<ZoomMessage>(this);
            WeakReferenceMessenger.Default.Register<ToggleMetadataMessage>(this);
        }

        private bool _eventsSubscribed = false;
        private void UpdateBufferReferences()
        {
            if (_window.ViewerControl == null) return;
            int idx = _window.ViewerControl.CurrentBufferIndex;

            _pageGrids = _window.ViewerControl.PageControlsBuffer[idx];
            _pageControls = _window.ViewerControl.PageControlsBuffer[idx];

            if (!_eventsSubscribed)
            {
                for (int b = 0; b < 2; b++)
                {
                    foreach (var pc in _window.ViewerControl.PageControlsBuffer[b])
                    {
                        pc.VideoSizeChanged += OnVideoSizeChanged;
                    }
                }
                _eventsSubscribed = true;
            }
        }

        private void OnVideoSizeChanged(object? sender, Windows.Foundation.Size e)
        {
            if (_settings.MangaSplitCount == 4 && _settings.QuadLayoutMode == 0)
            {
                // Re-calculate layout if in auto-quad mode
                _window.DispatcherQueue.TryEnqueue(async () => await UpdateDisplayAsync());
            }
        }



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

        public int GetEffectiveSplitCount() => _window.PlaylistManager.GetEffectiveSplitCount();

        public PageRenderer[] Pages => _window.ViewerControl != null ? _pagesBuffer[_window.ViewerControl.CurrentBufferIndex] : _pagesBuffer[0];
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

            if (_window.ViewerControl == null)
            {
                // UIがまだ初期化されていない場合は少し待機してリトライを試みる
                await Task.Delay(100);
                if (_window.ViewerControl == null)
                {
                    return;
                }
            }
            UpdateBufferReferences();


            if (_window.Playlist.Count == 0 || _window.CurrentIndex < 0 || _window.CurrentIndex >= _window.Playlist.Count)
            {
                // プレイリストが空の場合、またはインデックスが範囲外の場合は画面をクリアする。
                // ただし、スライドショー停止直後のディレクトリ再読み込み中などは、
                // 完全に真っ暗になるのを防ぐため、以前の表示を維持する場合がある。
                bool isLoading = _window.Playlist.Count == 0;
                if (!isLoading)
                {
                    for (int b = 0; b < 2; b++)
                    {
                        _window.ViewerControl.PagesGrids[b].Opacity = 0;
                        _window.ViewerControl.PagesGrids[b].Visibility = Visibility.Collapsed;
                    }
                }
                _window.UpdatePageIndicator();
                return;
            }

            if (_window.IsGridMode)
            {
                _window.UpdateGridItems(false);
                _window.ImageScrollViewer.Visibility = Visibility.Collapsed;
                _window.ImageGridView.Visibility = Visibility.Visible;
                _window.AnimationService.StopAnimation();
                _window.GridManager.StartGridAnimation();

                _window.ImageGridView.SelectedIndex = _window.CurrentIndex;
                _window.ImageGridView.ScrollIntoView(_window.ImageGridView.SelectedItem);
                _window.UpdatePageIndicator();
                _window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    // Ensure the GridView itself or an item has focus to enable keyboard navigation
                    if (_window.ImageGridView.SelectedItem != null)
                    {
                        var container = _window.ImageGridView.ContainerFromItem(_window.ImageGridView.SelectedItem) as Microsoft.UI.Xaml.Controls.GridViewItem;
                        if (container != null) container.Focus(FocusState.Programmatic);
                        else _window.ImageGridView.Focus(FocusState.Programmatic);
                    }
                    else
                    {
                        _window.ImageGridView.Focus(FocusState.Programmatic);
                    }
                });

                // Start preloading even in grid mode to make folder navigation snappy
                _ = _cacheManager.PreloadFoldersAsync(_window.CurrentDirectory);
                return;
            }
            else
            {
                _window.ImageScrollViewer.Visibility = Visibility.Visible;
                _window.ImageGridView.Visibility = Visibility.Collapsed;
                _window.GridManager.StopGridAnimation();
                WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
            }

            try { _window.AnimationService.StopAnimation(); } catch { }
            try { _window.AnimationService.StopCrossfade(); } catch { }
            UpdateStretch();

            int splitCount = _settings.MangaSplitCount;
            int gridStartIndex = _window.CurrentIndex;
            int effectiveSplitCount = GetEffectiveSplitCount();

            // Align to end of folder to ensure a full grid if possible (only for normal images)
            if (splitCount > 1 && effectiveSplitCount < splitCount && _window.Playlist.Count >= splitCount && gridStartIndex + splitCount > _window.Playlist.Count)
            {
                // Only align if we are not on an archive and not breaking an archive unit
                int potentialStart = _window.Playlist.Count - splitCount;
                bool containsArchive = false;
                for (int i = 0; i < splitCount; i++)
                {
                    if (ArchiveManager.IsArchive(_window.Playlist[potentialStart + i]) && !ArchiveManager.IsArchivePath(_window.Playlist[potentialStart + i]))
                    {
                        containsArchive = true;
                        break;
                    }
                }

                if (!containsArchive)
                {
                    gridStartIndex = potentialStart;
                    effectiveSplitCount = splitCount;
                }
            }

            // Recalculate layout for quad mode if needed
            if (splitCount == 4 && _settings.QuadLayoutMode == 0)
            {
                _cachedQuadLayout = _layoutManager.GetEffectiveQuadLayout(gridStartIndex, _window.Playlist, _window.RootGrid.ActualWidth, _window.RootGrid.ActualHeight);
            }
            else if (splitCount == 4)
            {
                _cachedQuadLayout = _settings.QuadLayoutMode;
            }
            else
            {
                _cachedQuadLayout = 1; // Default to horizontal for others
            }

            // Check if the content has changed (to prevent flickering)
            var currentPaths = _pagesBuffer[_window.ViewerControl.CurrentBufferIndex]
                .Take(effectiveSplitCount)
                .Select(p => p.CurrentFilePath)
                .ToList();

            var nextPaths = new List<string>();
            int maxIndexInView = gridStartIndex;
            bool isSlideshowRunning = _window.SlideshowManager.IsSlideshowRunning;

            for (int i = 0; i < effectiveSplitCount; i++)
            {
                int targetIndex = -1;
                if (isSlideshowRunning && _settings.SlideshowRandom && _window.SlideshowManager.SlideshowRandomIndices[i] != -1)
                {
                    targetIndex = _window.SlideshowManager.SlideshowRandomIndices[i];
                }
                else
                {
                    targetIndex = gridStartIndex + i;
                }

                if (targetIndex >= 0 && targetIndex < _window.Playlist.Count)
                {
                    nextPaths.Add(_window.Playlist[targetIndex]);
                    if (!isSlideshowRunning || !_settings.SlideshowRandom)
                    {
                        maxIndexInView = targetIndex;
                    }
                }
            }
            _window.ViewModel.OverrideDisplayIndex = maxIndexInView + 1;

            bool stateChanged = isSlideshowRunning != _lastIsSlideshowRunning;
            bool splitChanged = splitCount != _lastMangaSplitCount || effectiveSplitCount != _lastEffectiveSplitCount || _cachedQuadLayout != _lastCachedQuadLayout;

            if (!stateChanged && !splitChanged && currentPaths.SequenceEqual(nextPaths))
            {
                // 内容に変更がない場合は早期復帰する。
                // 確実に現在のバッファが表示されていることを保証する。
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

                // Always use double-buffering for smooth transitions
                int targetBufferIdx = _window.ViewerControl.InactiveBufferIndex;
                _layoutManager.UpdateLayoutGrid(
                    _window.ViewerControl.ColsBuffer[targetBufferIdx],
                    _window.ViewerControl.RowsBuffer[targetBufferIdx],
                    _window.ViewerControl.PageControlsBuffer[targetBufferIdx],
                    splitCount,
                    effectiveSplitCount,
                    _cachedQuadLayout,
                    isSlideshowRunning);

                var targetControls = _window.ViewerControl.PageControlsBuffer[targetBufferIdx];
                var targetPages = _pagesBuffer[targetBufferIdx];

                // Initially hide all controls in the target buffer before loading
                // This allows us to show them one by one as they load.
                for (int i = 0; i < 4; i++) targetControls[i].Visibility = Visibility.Collapsed;

                // Make the target buffer visible but transparent (container-wise)
                _window.ViewerControl.PagesGrids[targetBufferIdx].Opacity = 1;
                _window.ViewerControl.PagesGrids[targetBufferIdx].Visibility = Visibility.Visible;

                var currentFiles = new List<string>();
                var loadTasks = new List<Task>();
                for (int i = 0; i < effectiveSplitCount; i++)
                {
                    int indexToLoad = -1;
                    if (isSlideshowRunning && _settings.SlideshowRandom && _window.SlideshowManager.SlideshowRandomIndices[i] != -1)
                    {
                        indexToLoad = _window.SlideshowManager.SlideshowRandomIndices[i];
                    }
                    else
                    {
                        indexToLoad = gridStartIndex + i;
                    }

                    if (indexToLoad >= 0 && indexToLoad < _window.Playlist.Count)
                    {
                        string path = _window.Playlist[indexToLoad];
                        currentFiles.Add(path);
                        int cellIndex = i; // capture
                        var loadTask = _imageLoader.LoadPageIntoBufferAsync(path, targetControls[cellIndex], targetPages[cellIndex], cellIndex, token, _cacheManager)
                            .ContinueWith(t =>
                            {
                                if (!token.IsCancellationRequested)
                                {
                                    _window.DispatcherQueue.TryEnqueue(() =>
                                    {
                                        targetControls[cellIndex].Visibility = Visibility.Visible;
                                    });
                                }
                            });
                        loadTasks.Add(loadTask);
                    }
                }

                for (int i = effectiveSplitCount; i < 4; i++)
                {
                    targetControls[i].PageImage.Source = null;
                    targetControls[i].PageCanvas.Visibility = Visibility.Collapsed;
                    targetControls[i].ResetPlayback();
                    targetControls[i].LoadingRing.IsActive = false;
                    targetPages[i].Reset();
                }

                _window.ImageEditService.CleanupSessions(currentFiles);

                // Update metadata and indicator immediately for responsiveness
                _window.MetadataDisplayService.UpdateMetadataPanel();
                _window.UpdatePageIndicator();

                // Wait for the content to be ready before switching buffers to avoid flicker.
                // In normal mode, images will appear one by one as they load due to ContinueWith above.
                // We only wait here to decide when to hide the PREVIOUS buffer.
                try
                {
                    // スライドショー中、またはモード切り替え時（スライドショー停止直後など）は、
                    // 表示の乱れを防ぐためすべての画像がロードされるのを待機する。
                    if (isSlideshowRunning || stateChanged || splitChanged)
                    {
                        await Task.WhenAll(loadTasks);
                        // ContinueWith 内での UI スレッドへの Enqueue が完了するのをわずかに待機
                        await Task.Delay(50);
                    }
                    else
                    {
                        // 通常のナビゲーション時は、レスポンスを優先して最大500ms待機とする。
                        await Task.WhenAny(Task.Delay(500), Task.WhenAll(loadTasks));
                    }
                }
                catch (OperationCanceledException) { return; }
                catch (Exception) { }

                if (token.IsCancellationRequested) return;

                var prevBuffer = _window.ViewerControl.CurrentBuffer;
                var nextBuffer = _window.ViewerControl.InactiveBuffer;
                int prevBufferIdx = _window.ViewerControl.CurrentBufferIndex;

                if (isSlideshowRunning && _settings.SlideshowCrossfade)
                {
                    _window.ViewerControl.CurrentBufferIndex = targetBufferIdx;
                    UpdateBufferReferences();
                    _window.AnimationService.StartGridCrossfade(nextBuffer, prevBuffer);

                    var prevControls = _window.ViewerControl.PageControlsBuffer[prevBufferIdx];
                    _ = Task.Run(async () =>
                    {
                        double duration = _settings.SlideshowCrossfadeDuration > 0 ? _settings.SlideshowCrossfadeDuration : 0.5;
                        await Task.Delay((int)(duration * 1000) + 100);
                        _window.DispatcherQueue.TryEnqueue(() =>
                        {
                            foreach (var pc in prevControls) pc.ResetPlayback();
                        });
                    });
                }
                else
                {
                    // Finalize the swap
                    _window.ViewerControl.CurrentBufferIndex = targetBufferIdx;
                    UpdateBufferReferences();

                    // Hide the old buffer
                    prevBuffer.Opacity = 0;
                    prevBuffer.Visibility = Visibility.Collapsed;

                    foreach (var pc in _window.ViewerControl.PageControlsBuffer[prevBufferIdx]) pc.ResetPlayback();

                    _window.MetadataDisplayService.UpdateMetadataPanel();
                }

                _ = _cacheManager.PreloadAroundAsync(_window.CurrentIndex, _window.Playlist.ToList(), _settings.MangaSplitCount);
                _ = _cacheManager.PreloadFoldersAsync(_window.CurrentDirectory);

                // If loading is still ongoing (e.g. after timeout), ensure animation starts when finished
                if (loadTasks.Count > 0)
                {
                    _ = Task.Run(async () =>
                    {
                        try { await Task.WhenAll(loadTasks); } catch { }
                        _window.DispatcherQueue.TryEnqueue(() => _window.AnimationService.StartAnimation());
                    });
                }

                // すべての処理が成功裏に完了（またはタイムアウト等で次のバッファが表示開始）
                // した時点で、最後に表示した状態を記録する。
                _lastIsSlideshowRunning = isSlideshowRunning;
                _lastMangaSplitCount = splitCount;
                _lastEffectiveSplitCount = effectiveSplitCount;
                _lastCachedQuadLayout = _cachedQuadLayout;
            }
            finally
            {
            }

            _window.AnimationService.StartAnimation();
            _window.UpdatePageIndicator();
            WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
        }





        public void StopAnimation() => _window.AnimationService.StopAnimation();

        public void UpdateVolume()
        {
            if (_window.ViewerControl == null) return;
            for (int b = 0; b < 2; b++)
            {
                var controls = _window.ViewerControl.PageControlsBuffer[b];
                for (int i = 0; i < 4; i++)
                {
                    controls[i].UpdateVolume();
                }
            }
        }

        public void PauseAllVideo()
        {
            if (_window.ViewerControl == null) return;
            for (int b = 0; b < 2; b++)
            {
                var controls = _window.ViewerControl.PageControlsBuffer[b];
                for (int i = 0; i < 4; i++)
                {
                    controls[i].PauseVideo();
                }
            }
        }

        public void ResumeAllVideo()
        {
            if (_window.ViewerControl == null) return;
            for (int b = 0; b < 2; b++)
            {
                var controls = _window.ViewerControl.PageControlsBuffer[b];
                for (int i = 0; i < 4; i++)
                {
                    controls[i].ResumeVideo();
                }
            }
        }

        public void InvalidatePage(int index) => _pageControls[index].PageCanvas.Invalidate();

        public void UpdateStretch()
        {
            if (_window.ViewerControl == null) return;
            try
            {
                var stretch = (Microsoft.UI.Xaml.Media.Stretch)_settings.ImageStretchMode;

                int stretchMode = (int)stretch;

                for (int b = 0; b < 2; b++)
                {
                    var controls = _window.ViewerControl.PageControlsBuffer[b];
                    for (int i = 0; i < 4; i++)
                    {
                        controls[i].PageImage.Stretch = stretch;
                        controls[i].PagePlayer.Stretch = stretch;
                        controls[i].UpdateVideoVisualSize();
                        controls[i].PageCanvas.Invalidate();
                    }
                }
                for (int b = 0; b < 2; b++)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        _pagesBuffer[b][i].StretchMode = stretchMode;
                        _pagesBuffer[b][i].UseHighQualityScaling = _settings.UseHighQualityScaling;
                    }
                }
            }
            catch { }
        }

        public void HandleWindowSizeChanged(double width, double height)
        {
            if (_window.Playlist.Count == 0 || _window.IsGridMode) return;

            _lastResizeWidth = width;
            _lastResizeHeight = height;

            if (_resizeTimer == null)
            {
                _resizeTimer = new Microsoft.UI.Xaml.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Constants.LAYOUT_DEBOUNCE_MS) };
                _resizeTimer.Tick += (s, e) =>
                {
                    _resizeTimer.Stop();
                    ProcessDelayedResize(_lastResizeWidth, _lastResizeHeight);
                };
            }

            _resizeTimer.Stop();
            _resizeTimer.Start();
        }

        private void ProcessDelayedResize(double width, double height)
        {
            if (_settings.MangaSplitCount == 4 && _settings.QuadLayoutMode == 0)
            {
                _ = Task.Run(() =>
                {
                    int newLayout = _layoutManager.GetEffectiveQuadLayout(_window.CurrentIndex, _window.Playlist, width, height);
                    if (newLayout != _cachedQuadLayout)
                    {
                        _cachedQuadLayout = newLayout;
                        _window.DispatcherQueue.TryEnqueue(() => ApplyCurrentLayout());
                    }
                    else
                    {
                        _window.DispatcherQueue.TryEnqueue(() => UpdateAllVideoVisualSizes());
                    }
                });
            }
            else
            {
                UpdateAllVideoVisualSizes();
            }
        }

        private void ApplyCurrentLayout()
        {
            if (_window.ViewerControl == null) return;
            int currentBufferIdx = _window.ViewerControl.CurrentBufferIndex;
            int splitCount = _settings.MangaSplitCount;
            int effectiveSplitCount = _lastEffectiveSplitCount;

            _layoutManager.UpdateLayoutGrid(
                _window.ViewerControl.ColsBuffer[currentBufferIdx],
                _window.ViewerControl.RowsBuffer[currentBufferIdx],
                _window.ViewerControl.PageControlsBuffer[currentBufferIdx],
                splitCount,
                effectiveSplitCount,
                _cachedQuadLayout,
                _window.State.IsSlideshowRunning);

            UpdateAllVideoVisualSizes();
            for (int i = 0; i < 4; i++) InvalidatePage(i);
        }

        private void UpdateAllVideoVisualSizes()
        {
            if (_window.ViewerControl == null) return;
            var controls = _window.ViewerControl.PageControlsBuffer[_window.ViewerControl.CurrentBufferIndex];
            foreach (var pc in controls)
            {
                pc.UpdateVideoVisualSize();
            }
        }



        public void PaintCanvas(int bufferIndex, int pageIndex, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SkiaSharp.SKColors.Transparent);

            int hAlign = 1; // Center
            int vAlign = 1; // Center

            int effectiveSplitCount = _lastEffectiveSplitCount > 0 ? _lastEffectiveSplitCount : 1;

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

            // 動画フレームがあれば重ねて描画
            var controls = _window.ViewerControl?.PageControlsBuffer[bufferIndex];
            if (controls != null && pageIndex < controls.Length)
            {
                controls[pageIndex].PaintVideoFrame(canvas, e.Info, hAlign, vAlign);
            }
        }

        public void ToggleMetadataPanel(bool cycle = false) => _window.MetadataDisplayService.ToggleMetadataPanel(cycle);
        public void ShowNotification(string message) => _window.NotificationService.Show(message);
        public void Navigate(int offset, bool forceSingleStep) => _window.PlaylistManager.Navigate(offset, forceSingleStep);
        public void NavigateFolder(int offset) => _window.PlaylistManager.NavigateFolder(offset);

        public void ReplacePath(string oldPath, string newPath)
        {
            for (int b = 0; b < 2; b++)
            {
                foreach (var renderer in _pagesBuffer[b])
                {
                    if (renderer.CurrentFilePath == oldPath)
                    {
                        renderer.CurrentFilePath = newPath;
                    }
                }
            }
        }

        public void Dispose()
        {
            _displayCts?.Cancel();
            _cacheManager.CancelPreloads();
            if (_window.ViewerControl != null)
            {
                foreach (var buffer in _window.ViewerControl.PageControlsBuffer)
                {
                    foreach (var control in buffer) control.ResetPlayback();
                }
            }
            foreach (var p in _pagesBuffer[0]) p.Reset();
            foreach (var p in _pagesBuffer[1]) p.Reset();
            GC.SuppressFinalize(this);
        }
    }
}
