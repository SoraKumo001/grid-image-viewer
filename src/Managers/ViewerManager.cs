using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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
    public partial class ViewerManager : IViewerManager, IRecipient<ZoomMessage>, IRecipient<ToggleMetadataMessage>, IRecipient<EditActionCompletedMessage>
    {
        private readonly IViewerHost _window;
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
        public bool IsUpdatingDisplay => _window.State.IsDisplayUpdating;
        private int _cachedQuadLayout = 1;
        private bool _isRenderingSubscribed = false;

        private record struct LayoutState(
            bool IsSlideshowRunning,
            int MangaSplitCount,
            int EffectiveSplitCount,
            int CachedQuadLayout,
            bool IsRightToLeft
        );

        private LayoutState _lastLayoutState = new(false, -1, 1, -1, true);
        private Microsoft.UI.Xaml.DispatcherTimer? _resizeTimer;
        private double _lastResizeWidth;
        private double _lastResizeHeight;
        private readonly ResourceLoader _resourceLoader = new();

        public ViewerManager(IViewerHost window, ISettingsManager settings, IViewerCacheManager cacheManager)
        {
            _window = window;
            _settings = settings;
            _layoutManager = new ViewerLayoutManager(settings);
            _imageLoader = new ViewerImageLoader(window, settings);
            _cacheManager = cacheManager;

            // Do NOT initialize buffer references here as UI might not be ready
            // They will be initialized on first use in UpdateDisplayAsync or other methods

            WeakReferenceMessenger.Default.Register<ZoomMessage>(this);
            WeakReferenceMessenger.Default.Register<EditActionCompletedMessage>(this);
            WeakReferenceMessenger.Default.Register<ToggleMetadataMessage>(this);
        }

        private bool _eventsSubscribed = false;
        private bool _needsUpdate = false;
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
                // Reset zoom and scroll offsets (immediate)
                if (_window.ImageScrollViewer != null)
                {
                    _window.ImageScrollViewer.ChangeView(0, 0, 1.0f, true);
                }

                // Reset custom translation on the container
                if (_window.PagesGrid?.RenderTransform is CompositeTransform ct)
                {
                    ct.TranslateX = 0;
                    ct.TranslateY = 0;
                }

                // Also reset transform on the active buffer to ensure full reset
                if (_window.ViewerControl?.CurrentBuffer?.RenderTransform is CompositeTransform bct)
                {
                    bct.TranslateX = 0;
                    bct.TranslateY = 0;
                }
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
            if (_window.State.IsDisplayUpdating)
            {
                _needsUpdate = true;
                return;
            }

            if (!_window.DispatcherQueue.HasThreadAccess)
            {
                _window.DispatcherQueue.TryEnqueue(async () => await UpdateDisplayAsync());
                return;
            }

            _window.State.IsDisplayUpdating = true;
            _needsUpdate = false;
            try
            {
                if (!await EnsureUIReadyAsync()) return;

                if (HandleEmptyPlaylist()) return;

                if (_window.IsGridMode)
                {
                    HandleGridMode();
                    return;
                }

                PrepareViewerMode();

                var layoutInfo = await CalculateLayoutAndIndicesAsync();

                if (TryEarlyReturnIfUnchanged(layoutInfo)) return;

                var loadContext = PrepareAndLoadBuffer(layoutInfo);
                if (loadContext.Token.IsCancellationRequested) return;

                await WaitForLoadingAndSwapBuffersAsync(loadContext);
            }
            finally
            {
                _window.State.IsDisplayUpdating = false;
                if (_needsUpdate)
                {
                    _window.DispatcherQueue.TryEnqueue(async () => await UpdateDisplayAsync());
                }
            }
        }

        private async Task<bool> EnsureUIReadyAsync()
        {
            if (_window.ViewerControl == null)
            {
                // UIがまだ初期化されていない場合は少し待機してリトライを試みる
                await Task.Delay(100);
                if (_window.ViewerControl == null) return false;
            }
            UpdateBufferReferences();
            return true;
        }

        private bool HandleEmptyPlaylist()
        {
            if (_window.Playlist.Count == 0 || _window.CurrentIndex < 0 || _window.CurrentIndex >= _window.Playlist.Count)
            {
                // プレイリストが空の場合、またはインデックスが範囲外の場合は画面をクリアする。
                // ただし、スライドショー停止直後のディレクトリ再読み込み中などは、
                // 完全に真っ暗になるのを防ぐため、以前の表示を維持する場合がある。
                bool isLoading = _window.Playlist.Count == 0;
                if (!isLoading && _window.ViewerControl != null)
                {
                    for (int b = 0; b < 2; b++)
                    {
                        _window.ViewerControl.PagesGrids[b].Opacity = 0;
                        _window.ViewerControl.PagesGrids[b].Visibility = Visibility.Collapsed;
                    }
                }
                _window.UpdatePageIndicator();
                return true;
            }
            return false;
        }

        private void HandleGridMode()
        {
            _window.UpdateGridItems(false);
            _window.ImageScrollViewer.Visibility = Visibility.Collapsed;
            _window.ImageGridView.Visibility = Visibility.Visible;
            _window.AnimationService.StopAnimation();
            _window.GridManager.StartGridAnimation();
            UpdateRenderingSubscription();

            _window.ImageGridView.SelectedIndex = _window.CurrentIndex;
            _window.ImageGridView.ScrollIntoView(_window.ImageGridView.SelectedItem);
            _window.UpdatePageIndicator();
            _window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                if (_window.ImageGridView.SelectedItem != null)
                {
                    if (_window.ImageGridView.ContainerFromItem(_window.ImageGridView.SelectedItem) is Microsoft.UI.Xaml.Controls.GridViewItem container)
                    {
                        container.Focus(FocusState.Programmatic);
                    }
                    else
                    {
                        _window.ImageGridView.Focus(FocusState.Programmatic);
                    }
                }
                else
                {
                    _window.ImageGridView.Focus(FocusState.Programmatic);
                }
            });

            _ = _cacheManager.PreloadFoldersAsync(_window.CurrentDirectory);
        }

        private void PrepareViewerMode()
        {
            _window.ImageScrollViewer.Visibility = Visibility.Visible;
            _window.ImageGridView.Visibility = Visibility.Collapsed;
            _window.GridManager.StopGridAnimation();
            WeakReferenceMessenger.Default.Send(new FocusRequestMessage());

            try { _window.AnimationService.StopAnimation(); }
            catch (Exception ex) { AppLog.Error("ViewerManager", "StopAnimation failed", ex); }

            try { _window.AnimationService.StopCrossfade(); }
            catch (Exception ex) { AppLog.Error("ViewerManager", "StopCrossfade failed", ex); }
            UpdateStretch();
        }

        private record LayoutInfo(
            int SplitCount,
            int EffectiveSplitCount,
            int GridStartIndex,
            int CachedQuadLayout,
            bool IsSlideshowRunning,
            List<string> NextPaths,
            bool StateChanged,
            bool SplitChanged,
            List<string> CurrentPaths
        );

        private async Task<LayoutInfo> CalculateLayoutAndIndicesAsync()
        {
            int splitCount = _settings.MangaSplitCount;
            int gridStartIndex = _window.CurrentIndex;
            int effectiveSplitCount = GetEffectiveSplitCount();

            // Align to end of folder to ensure a full grid if possible (only for normal images)
            if (splitCount > 1 && effectiveSplitCount < splitCount && _window.Playlist.Count >= splitCount && gridStartIndex + splitCount > _window.Playlist.Count)
            {
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

            int cachedQuadLayout = 1;
            if (splitCount == 4)
            {
                if (_settings.QuadLayoutMode == 0)
                {
                    var playlistSnapshot = _window.Playlist.ToList();
                    double windowWidth = _window.Bounds.Width;
                    double windowHeight = _window.Bounds.Height;
                    cachedQuadLayout = await Task.Run(() => _layoutManager.GetEffectiveQuadLayout(gridStartIndex, playlistSnapshot, windowWidth, windowHeight));
                }
                else
                {
                    cachedQuadLayout = _settings.QuadLayoutMode;
                }
            }
            _cachedQuadLayout = cachedQuadLayout;

            var currentPaths = _pagesBuffer[_window.ViewerControl.CurrentBufferIndex]
                .Take(effectiveSplitCount)
                .Select(p => p.CurrentFilePath ?? string.Empty)
                .ToList();

            var nextPaths = new List<string>();
            int maxIndexInView = gridStartIndex;
            bool isSlideshowRunning = _window.SlideshowManager.IsSlideshowRunning;

            for (int i = 0; i < effectiveSplitCount; i++)
            {
                int targetIndex = (isSlideshowRunning && _settings.SlideshowRandom && _window.SlideshowManager.SlideshowRandomIndices[i] != -1)
                    ? _window.SlideshowManager.SlideshowRandomIndices[i]
                    : gridStartIndex + i;

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

            bool stateChanged = isSlideshowRunning != _lastLayoutState.IsSlideshowRunning;
            bool splitChanged = splitCount != _lastLayoutState.MangaSplitCount || effectiveSplitCount != _lastLayoutState.EffectiveSplitCount || cachedQuadLayout != _lastLayoutState.CachedQuadLayout || _settings.IsRightToLeft != _lastLayoutState.IsRightToLeft;

            return new LayoutInfo(splitCount, effectiveSplitCount, gridStartIndex, cachedQuadLayout, isSlideshowRunning, nextPaths, stateChanged, splitChanged, currentPaths);
        }

        private bool TryEarlyReturnIfUnchanged(LayoutInfo info)
        {
            if (!info.StateChanged && !info.SplitChanged && info.CurrentPaths.SequenceEqual(info.NextPaths))
            {
                if (_window.ViewerControl != null)
                {
                    _window.ViewerControl.CurrentBuffer.Opacity = 1;
                    _window.ViewerControl.CurrentBuffer.Visibility = Visibility.Visible;
                    _window.ViewerControl.InactiveBuffer.Opacity = 0;
                    _window.ViewerControl.InactiveBuffer.Visibility = Visibility.Collapsed;
                }

                _window.UpdatePageIndicator();
                _window.MetadataDisplayService.UpdateMetadataPanel();
                _window.AnimationService.StartAnimation();
                return true;
            }
            return false;
        }

        private record BufferLoadContext(
            CancellationToken Token,
            LayoutInfo LayoutInfo,
            int TargetBufferIdx,
            List<Task> LoadTasks,
            List<string> CurrentFiles
        );

        private BufferLoadContext PrepareAndLoadBuffer(LayoutInfo info)
        {
            _displayCts?.Cancel();
            _displayCts?.Dispose();
            _displayCts = new CancellationTokenSource();
            var token = _displayCts.Token;

            int targetBufferIdx = _window.ViewerControl.InactiveBufferIndex;
            var targetGrid = _window.ViewerControl.PagesGrids[targetBufferIdx];
            double[] tempAspects = new double[info.EffectiveSplitCount];
            for (int i = 0; i < info.EffectiveSplitCount; i++) tempAspects[i] = 0.75;

            _layoutManager.UpdateLayoutGrid(
                targetGrid,
                _window.ViewerControl.ColsBuffer[targetBufferIdx],
                _window.ViewerControl.RowsBuffer[targetBufferIdx],
                _window.ViewerControl.PageControlsBuffer[targetBufferIdx],
                tempAspects,
                _window.ViewerControl.ScrollViewer.ViewportWidth,
                _window.ViewerControl.ScrollViewer.ViewportHeight,
                info.SplitCount,
                info.EffectiveSplitCount,
                info.CachedQuadLayout,
                info.IsSlideshowRunning);

            var targetControls = _window.ViewerControl.PageControlsBuffer[targetBufferIdx];
            var targetPages = _pagesBuffer[targetBufferIdx];

            // Important: Keep target buffer HIDDEN during loading to prevent flickering
            _window.ViewerControl.PagesGrids[targetBufferIdx].Opacity = 0;
            _window.ViewerControl.PagesGrids[targetBufferIdx].Visibility = Visibility.Collapsed;

            // Clear ALL controls first to ensure a clean state
            for (int i = 0; i < 4; i++)
            {
                targetControls[i].PageImage.Source = null;
                targetControls[i].PageCanvas.Visibility = Visibility.Collapsed;
                targetControls[i].ResetPlayback();
                targetControls[i].LoadingRing.IsActive = false;
                targetPages[i].Reset();
                targetPages[i].StretchMode = (int)(Microsoft.UI.Xaml.Media.Stretch)_settings.ImageStretchMode;
                targetPages[i].EnablePanAnimation = _settings.EnablePanAnimation;
                targetPages[i].PanAnimationSpeed = _settings.PanAnimationSpeed;
                targetControls[i].Visibility = Visibility.Collapsed;
            }

            var currentFiles = new List<string>();
            var loadTasks = new List<Task>();

            for (int i = 0; i < info.EffectiveSplitCount; i++)
            {
                int indexToLoad = (info.IsSlideshowRunning && _settings.SlideshowRandom && _window.SlideshowManager.SlideshowRandomIndices[i] != -1)
                    ? _window.SlideshowManager.SlideshowRandomIndices[i]
                    : info.GridStartIndex + i;

                if (indexToLoad >= 0 && indexToLoad < _window.Playlist.Count)
                {
                    string path = _window.Playlist[indexToLoad];
                    currentFiles.Add(path);
                    int cellIndex = i;

                    // Show the control container immediately so LoadingRing can be seen if needed
                    targetControls[cellIndex].Visibility = Visibility.Visible;

                    var loadTask = _imageLoader.LoadPageIntoBufferAsync(path, targetControls[cellIndex], targetPages[cellIndex], cellIndex, token, _cacheManager);
                    loadTasks.Add(loadTask);
                }
            }

            _window.ImageEditService.CleanupSessions(currentFiles);
            _window.MetadataDisplayService.UpdateMetadataPanel();
            _window.UpdatePageIndicator();

            return new BufferLoadContext(token, info, targetBufferIdx, loadTasks, currentFiles);
        }

        private async Task WaitForLoadingAndSwapBuffersAsync(BufferLoadContext context)
        {
            var info = context.LayoutInfo;
            var token = context.Token;
            var loadTasks = context.LoadTasks;

            try
            {
                // Wait for either completion or a reasonable timeout for normal navigation
                // Slideshow or structural changes should wait more strictly
                if (info.IsSlideshowRunning || info.StateChanged || info.SplitChanged)
                {
                    await Task.WhenAll(loadTasks);
                    await Task.Delay(50);
                }
                else
                {
                    // 連続移動時は、ダブルバッファを安定させるために全ロード完了を待つ
                    await Task.WhenAll(loadTasks);
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                AppLog.Error("ViewerManager", "Buffer load wait failed", ex);
            }

            // 全タスク待機後にキャンセル状態を再確認
            if (token.IsCancellationRequested) return;

            // Update layout with actual loaded aspect ratios before swapping buffers
            int targetBufferIdx = context.TargetBufferIdx;
            var targetGrid = _window.ViewerControl.PagesGrids[targetBufferIdx];
            double[] loadedAspects = GetAspectRatios(targetBufferIdx, info.EffectiveSplitCount);

            _layoutManager.UpdateLayoutGrid(
                targetGrid,
                _window.ViewerControl.ColsBuffer[targetBufferIdx],
                _window.ViewerControl.RowsBuffer[targetBufferIdx],
                _window.ViewerControl.PageControlsBuffer[targetBufferIdx],
                loadedAspects,
                _window.ViewerControl.ScrollViewer.ViewportWidth,
                _window.ViewerControl.ScrollViewer.ViewportHeight,
                info.SplitCount,
                info.EffectiveSplitCount,
                info.CachedQuadLayout,
                info.IsSlideshowRunning);

            var prevBuffer = _window.ViewerControl.CurrentBuffer;
            var nextBuffer = _window.ViewerControl.InactiveBuffer;
            int prevBufferIdx = _window.ViewerControl.CurrentBufferIndex;

            if (info.IsSlideshowRunning && _settings.SlideshowCrossfade)
            {
                // Prepare next buffer for crossfade
                nextBuffer.Opacity = 0;
                nextBuffer.Visibility = Visibility.Visible;

                _window.ViewerControl.CurrentBufferIndex = context.TargetBufferIdx;
                UpdateBufferReferences();

                _window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    for (int i = 0; i < 4; i++) InvalidatePage(i);
                });

                _window.AnimationService.StartGridCrossfade(nextBuffer, prevBuffer);

                var prevControls = _window.ViewerControl.PageControlsBuffer[prevBufferIdx];
                _ = Task.Run(async () =>
                {
                    double duration = _settings.SlideshowCrossfadeDuration > 0 ? _settings.SlideshowCrossfadeDuration : 0.5;
                    await Task.Delay((int)(duration * 1000) + 100);
                    _window.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        foreach (var pc in prevControls) pc.ResetPlayback();
                        // Ensure previous buffer is fully hidden after crossfade
                        prevBuffer.Visibility = Visibility.Collapsed;
                        prevBuffer.Opacity = 0;
                    });
                });
            }
            else
            {
                // Show the new buffer immediately.
                // Since it is transparent until images load, we will still see the old buffer underneath.
                Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(nextBuffer, 10);
                Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(prevBuffer, 0);
                nextBuffer.Opacity = 1;
                nextBuffer.Visibility = Visibility.Visible;

                _window.ViewerControl.CurrentBufferIndex = context.TargetBufferIdx;
                UpdateBufferReferences();

                // Small additional delay to ensure WinUI has finished rendering the first frame
                // of the newly loaded images before we hide the background.
                await Task.Delay(32);

                if (token.IsCancellationRequested) return;

                // 画面が表示され、レイアウトが更新された後に明示的に再描画を要求する
                for (int i = 0; i < 4; i++)
                {
                    InvalidatePage(i);
                }

                if (token.IsCancellationRequested) return;

                // Now it's safe to hide the old content.
                prevBuffer.Opacity = 0;
                prevBuffer.Visibility = Visibility.Collapsed;

                foreach (var pc in _window.ViewerControl.PageControlsBuffer[prevBufferIdx])
                {
                    pc.ResetPlayback();
                }

                _window.MetadataDisplayService.UpdateMetadataPanel();
            }

            _ = _cacheManager.PreloadAroundAsync(_window.CurrentIndex, _window.Playlist.ToList(), _settings.MangaSplitCount);
            _ = _cacheManager.PreloadFoldersAsync(_window.CurrentDirectory);

            if (loadTasks.Count > 0)
            {
                _ = Task.Run(async () =>
                {
                    try { await Task.WhenAll(loadTasks); }
                    catch (Exception ex) { AppLog.Error("ViewerManager", "Background load completion wait failed", ex); }
                    _window.DispatcherQueue.TryEnqueue(() => _window.AnimationService.StartAnimation());
                });
            }

            _lastLayoutState = new LayoutState(
                info.IsSlideshowRunning,
                info.SplitCount,
                info.EffectiveSplitCount,
                info.CachedQuadLayout,
                _settings.IsRightToLeft
            );

            _window.AnimationService.StartAnimation();
            _window.UpdatePageIndicator();
            WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
            UpdateRenderingSubscription();
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

        public void UpdateStretch(bool resetPosition = false)
        {
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                if (_window.ViewerControl == null) return;

                // Reset translation and zoom only when explicitly requested (e.g. manual stretch change)
                if (resetPosition)
                {
                    if (_window.ViewerControl.RootPagesContainer.RenderTransform is CompositeTransform ct)
                    {
                        ct.TranslateX = 0;
                        ct.TranslateY = 0;
                    }
                    _window.ImageScrollViewer.ChangeView(null, null, 1.0f);
                }

                try
                {
                    var stretch = (Microsoft.UI.Xaml.Media.Stretch)_settings.ImageStretchMode;
                    int stretchMode = (int)stretch;

                    // Update renderer data first to ensure Skia uses the new mode during paint
                    if (_pagesBuffer != null)
                    {
                        for (int b = 0; b < 2; b++)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                if (_pagesBuffer[b][i] != null)
                                {
                                    _pagesBuffer[b][i].StretchMode = stretchMode;
                                    _pagesBuffer[b][i].UseHighQualityScaling = _settings.UseHighQualityScaling;
                                    _pagesBuffer[b][i].EnablePanAnimation = _settings.EnablePanAnimation;
                                    _pagesBuffer[b][i].PanAnimationSpeed = _settings.PanAnimationSpeed;
                                }
                            }
                        }
                    }

                    // Then update UI control properties and invalidate
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
                    UpdateRenderingSubscription();
                }
                catch (Exception ex)
                {
                    AppLog.Error("ViewerManager", "UpdateStretch failed", ex);
                }
            });
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

        private double[] GetAspectRatios(int bufferIdx, int count)
        {
            var aspectRatios = new double[count];
            var pages = _pagesBuffer[bufferIdx];
            var controls = _window.ViewerControl.PageControlsBuffer[bufferIdx];
            for (int i = 0; i < count; i++)
            {
                if (i < controls.Length)
                {
                    var renderer = i < pages.Length ? pages[i] : null;
                    aspectRatios[i] = controls[i].GetContentAspectRatio(renderer);
                }
                else
                {
                    aspectRatios[i] = 0.75;
                }
            }
            return aspectRatios;
        }

        private void ApplyCurrentLayout()
        {
            if (_window.ViewerControl == null || _window.State.IsDisplayUpdating) return;
            int currentBufferIdx = _window.ViewerControl.CurrentBufferIndex;
            int splitCount = _settings.MangaSplitCount;
            int effectiveSplitCount = _lastLayoutState.EffectiveSplitCount;

            var currentGrid = _window.ViewerControl.PagesGrids[currentBufferIdx];
            double[] aspects = GetAspectRatios(currentBufferIdx, effectiveSplitCount);

            _layoutManager.UpdateLayoutGrid(
                currentGrid,
                _window.ViewerControl.ColsBuffer[currentBufferIdx],
                _window.ViewerControl.RowsBuffer[currentBufferIdx],
                _window.ViewerControl.PageControlsBuffer[currentBufferIdx],
                aspects,
                _window.ViewerControl.ScrollViewer.ViewportWidth,
                _window.ViewerControl.ScrollViewer.ViewportHeight,
                splitCount,
                effectiveSplitCount,
                _cachedQuadLayout,
                _window.State.IsSlideshowRunning);

            _window.ViewerControl.CurrentBuffer.UpdateLayout();
            UpdateAllVideoVisualSizes();
            for (int i = 0; i < 4; i++) InvalidatePage(i);

            _window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                UpdateAllVideoVisualSizes();
                for (int i = 0; i < 4; i++) InvalidatePage(i);
            });

            _ = UpdateVideoVisualSizesAfterLayoutDelayAsync();
            UpdateRenderingSubscription();
        }

        private async Task UpdateVideoVisualSizesAfterLayoutDelayAsync()
        {
            await Task.Delay(32);
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                UpdateAllVideoVisualSizes();
                for (int i = 0; i < 4; i++) InvalidatePage(i);
            });
        }

        private void UpdateAllVideoVisualSizes()
        {
            if (_window.ViewerControl == null) return;
            var controls = _window.ViewerControl.PageControlsBuffer[_window.ViewerControl.CurrentBufferIndex];
            foreach (var pc in controls)
            {
                pc.UpdateVideoVisualSize(pc.ActualWidth, pc.ActualHeight);
            }
        }



        public void PaintCanvas(int bufferIndex, int pageIndex, SKPaintGLSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SkiaSharp.SKColors.Transparent);

            int hAlign = 1; // Center
            int vAlign = 1; // Center

            var controls = _window.ViewerControl?.PageControlsBuffer[bufferIndex];
            if (controls != null && pageIndex < controls.Length)
            {
                var ha = controls[pageIndex].PageImage.HorizontalAlignment;
                var va = controls[pageIndex].PageImage.VerticalAlignment;

                if (ha == Microsoft.UI.Xaml.HorizontalAlignment.Left) hAlign = 0;
                else if (ha == Microsoft.UI.Xaml.HorizontalAlignment.Right) hAlign = 2;

                if (va == Microsoft.UI.Xaml.VerticalAlignment.Top) vAlign = 0;
                else if (va == Microsoft.UI.Xaml.VerticalAlignment.Bottom) vAlign = 2;
            }

            var renderer = _pagesBuffer[bufferIndex][pageIndex];
            lock (renderer)
            {
                renderer.Paint(canvas, e.Info, hAlign, vAlign);
            }

            // 動画フレームがあれば重ねて描画
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

        public void ClearPageImageForPath(string path)
        {
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                for (int b = 0; b < 2; b++)
                {
                    for (int i = 0; i < _pagesBuffer[b].Length; i++)
                    {
                        if (_pagesBuffer[b][i].CurrentFilePath == path)
                        {
                            var ctrl = _window.ViewerControl?.PageControlsBuffer[b][i];
                            if (ctrl != null) ctrl.PageImage.Source = null;
                        }
                    }
                }
            });
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

            if (_isRenderingSubscribed)
            {
                Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= OnCompositionRendering;
                _isRenderingSubscribed = false;
            }

            GC.SuppressFinalize(this);
        }

        public void Receive(EditActionCompletedMessage message)
        {
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                StopAnimation();
                bool updated = false;
                for (int b = 0; b < 2; b++)
                {
                    for (int i = 0; i < _pagesBuffer[b].Length; i++)
                    {
                        if (_pagesBuffer[b][i].CurrentFilePath == message.Path)
                        {
                            _pagesBuffer[b][i].EditedBitmap = message.Bitmap;
                            var ctrl = _window.ViewerControl?.PageControlsBuffer[b][i];
                            if (ctrl != null)
                            {
                                ctrl.PageImage.Visibility = Visibility.Collapsed;
                                ctrl.PageCanvas.Visibility = Visibility.Visible;
                                ctrl.PageCanvas.Invalidate();
                            }
                            updated = true;
                        }
                    }
                }
                if (updated)
                {
                    _ = UpdateDisplayAsync();
                }
                else
                {
                    // For debugging: show notification if path not found in buffers
                    // ShowNotification("Debug: Edit path not found in buffers");
                }
            });
        }

        private void UpdateRenderingSubscription()
        {
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                bool needRendering = _settings.EnablePanAnimation
                                     && !_window.IsGridMode
                                     && _settings.ImageStretchMode == 3
                                     && _window.Playlist.Count > 0;

                if (needRendering && !_isRenderingSubscribed)
                {
                    Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += OnCompositionRendering;
                    _isRenderingSubscribed = true;
                }
                else if (!needRendering && _isRenderingSubscribed)
                {
                    Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= OnCompositionRendering;
                    _isRenderingSubscribed = false;
                }
            });
        }

        private void OnCompositionRendering(object? sender, object e)
        {
            if (_window.ViewerControl == null || _window.IsGridMode) return;

            int effectiveCount = GetEffectiveSplitCount();
            for (int i = 0; i < effectiveCount; i++)
            {
                InvalidatePage(i);
                if (i < _pageControls.Length && _pageControls[i] != null && _pageControls[i].IsVideoContent)
                {
                    _pageControls[i].UpdateVideoVisualSize();
                }
            }
        }
    }
}







