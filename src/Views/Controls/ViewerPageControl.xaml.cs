using CommunityToolkit.Mvvm.Messaging;
using FFmpegInteropX;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using quick_image_viewer.Common;
using quick_image_viewer.ViewModels;
using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Playback;

namespace quick_image_viewer.Views.Controls
{
    public sealed partial class ViewerPageControl : UserControl
    {
        public event System.EventHandler<Windows.Foundation.Size>? VideoSizeChanged;
        public Grid RootGrid => InternalRootGrid;
        public Image PageImage => InternalPageImage;
        public SkiaSharp.Views.Windows.SKXamlCanvas PageCanvas => InternalPageCanvas;

        private MediaPlayerElement? _internalMediaPlayer;
        public MediaPlayerElement PagePlayer => _internalMediaPlayer ?? throw new InvalidOperationException("MediaPlayerElement not initialized");

        public ProgressRing LoadingRing => InternalLoadingRing;
        public Border FocusBorder => InternalFocusBorder;

        private readonly Microsoft.UI.Xaml.DispatcherTimer _hideTimer;
        private readonly Microsoft.UI.Xaml.DispatcherTimer _sliderUpdateTimer;
        private readonly Microsoft.UI.Xaml.DispatcherTimer _resizeDebounceTimer;
        private double _pendingWidth;
        private double _pendingHeight;
        private bool _isDraggingSlider = false;
        private readonly Interfaces.ISettingsManager _settings;
        private readonly System.Threading.SemaphoreSlim _loadingSemaphore = new(1, 1);
        private static readonly System.Threading.SemaphoreSlim _globalVideoInitSemaphore = new(1, 1);
        private Microsoft.UI.Xaml.Media.Imaging.SoftwareBitmapSource? _currentSoftwareSource;
        private System.Threading.CancellationTokenSource? _activeLoadCts;

        public static System.Threading.SemaphoreSlim GetGlobalInitSemaphore() => _globalVideoInitSemaphore;

        public void RecreateMediaPlayerElement()
        {
            if (_internalMediaPlayer != null)
            {
                try
                {
                    var oldPlayer = _internalMediaPlayer.MediaPlayer;
                    if (oldPlayer != null)
                    {
                        oldPlayer.Pause();
                        oldPlayer.Source = null;
                    }
                    _internalMediaPlayer.Source = null;
                }
                catch { }
                MediaPlayerContainer.Children.Remove(_internalMediaPlayer);
                _internalMediaPlayer = null;
            }

            _internalMediaPlayer = new MediaPlayerElement
            {
                AutoPlay = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                AreTransportControlsEnabled = false,
                IsHitTestVisible = false,
                IsTabStop = false,
                AllowFocusOnInteraction = false,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                FocusVisualPrimaryThickness = new Thickness(0),
                FocusVisualSecondaryThickness = new Thickness(0),
                Visibility = Visibility.Collapsed
            };

            MediaPlayerContainer.Children.Add(_internalMediaPlayer);
        }

        public Windows.Media.Playback.MediaPlayer CreateNewMediaPlayer()
        {
            RecreateMediaPlayerElement();
            var mp = new Windows.Media.Playback.MediaPlayer();
            // デフォルトではフレームサーバーは無効（標準表示）
            mp.IsVideoFrameServerEnabled = false;
            _internalMediaPlayer!.SetMediaPlayer(mp);
            return mp;
        }

        // Frame Server Mode
        private Windows.Graphics.Imaging.SoftwareBitmap? _frameBitmap;
        private SkiaSharp.SKBitmap? _skFrameBitmap;
        private readonly object _frameLock = new();
        private FFmpegMediaSource? _ffmpegSource;
        public bool IsVideoContent { get; set; } = false;
        public bool IsMediaReady { get; set; } = false;
        private uint _lastNaturalWidth = 0;
        private uint _lastNaturalHeight = 0;

        public void UpdateSoftwareSource(Microsoft.UI.Xaml.Media.Imaging.SoftwareBitmapSource source)
        {
            if (_currentSoftwareSource != null) _currentSoftwareSource.Dispose();
            _currentSoftwareSource = source;
            PageImage.Source = source;
        }

        public System.Threading.CancellationToken GetNewLoadToken()
        {
            _activeLoadCts?.Cancel();
            _activeLoadCts?.Dispose();
            _activeLoadCts = new System.Threading.CancellationTokenSource();
            return _activeLoadCts.Token;
        }

        public FFmpegMediaSource? FFmpegSource
        {
            get => _ffmpegSource;
            set
            {
                if (_ffmpegSource != value)
                {
                    _ffmpegSource?.Dispose();
                    _ffmpegSource = value;
                }
            }
        }

        public ViewerPageControl()
        {
            this.InitializeComponent();
            _settings = ((App)Application.Current).Services.GetService<Interfaces.ISettingsManager>()!;

            _hideTimer = new Microsoft.UI.Xaml.DispatcherTimer { Interval = System.TimeSpan.FromSeconds(3) };
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                bool hadFocus = false;
                try
                {
                    var focused = (this.IsLoaded && this.XamlRoot != null ? Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(this.XamlRoot) : null);
                    if (focused is DependencyObject dep && IsChildOf(dep, CustomTransportPanel))
                    {
                        hadFocus = true;
                    }
                }
                catch { }

                CustomTransportPanel.Visibility = Visibility.Collapsed;
                if (hadFocus)
                {
                    WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
                }
            };

            _sliderUpdateTimer = new Microsoft.UI.Xaml.DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(100) };
            _sliderUpdateTimer.Tick += (s, e) => UpdateSlider();

            _resizeDebounceTimer = new Microsoft.UI.Xaml.DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(Constants.LAYOUT_DEBOUNCE_MS) };
            _resizeDebounceTimer.Tick += (s, e) =>
            {
                _resizeDebounceTimer.Stop();
                UpdateVideoVisualSize(_pendingWidth, _pendingHeight);
            };


            TimelineSlider.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(TimelineSlider_PointerPressed), true);
            TimelineSlider.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(TimelineSlider_PointerReleased), true);


            InternalRootGrid.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(InternalRootGrid_PointerMoved), true);

            this.SizeChanged += ViewerPageControl_SizeChanged;
            RecreateMediaPlayerElement();
        }

        private void ViewerPageControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _pendingWidth = e.NewSize.Width;
            _pendingHeight = e.NewSize.Height;
            _resizeDebounceTimer.Stop();
            _resizeDebounceTimer.Start();

            UpdateClip(_pendingWidth, _pendingHeight);

            var mp = _internalMediaPlayer?.MediaPlayer;
            if (mp != null && mp.PlaybackSession.PlaybackState != MediaPlaybackState.None)
            {
                double scale = 1.0;
                try
                {
                    if (this.XamlRoot != null)
                        scale = this.XamlRoot.RasterizationScale;
                }
                catch { }

                uint width = (uint)System.Math.Max(1, this.ActualWidth * scale);
                uint height = (uint)System.Math.Max(1, this.ActualHeight * scale);
                try { mp.SetSurfaceSize(new Windows.Foundation.Size(width, height)); } catch { }
            }
        }

        private void UpdateClip(double w, double h)
        {
            if (w > 0 && h > 0)
            {
                InternalRootGrid.Clip = new RectangleGeometry
                {
                    Rect = new Windows.Foundation.Rect(0, 0, w, h)
                };
            }
            else
            {
                InternalRootGrid.Clip = null;
            }
        }

        public void PaintVideoFrame(SkiaSharp.SKCanvas canvas, SkiaSharp.SKImageInfo info, int hAlign, int vAlign)
        {
            lock (_frameLock)
            {
                if (_skFrameBitmap != null)
                {
                    float scale;
                    if (InternalPageImage.Stretch == Stretch.UniformToFill)
                        scale = System.Math.Max((float)info.Width / _skFrameBitmap.Width, (float)info.Height / _skFrameBitmap.Height);
                    else if (InternalPageImage.Stretch == Stretch.Uniform)
                        scale = System.Math.Min((float)info.Width / _skFrameBitmap.Width, (float)info.Height / _skFrameBitmap.Height);
                    else
                        scale = 1.0f;

                    float w = _skFrameBitmap.Width * scale;
                    float h = _skFrameBitmap.Height * scale;
                    float x = (info.Width - w) / 2f;
                    float y = (info.Height - h) / 2f;

                    if (InternalPageImage.Stretch != Stretch.UniformToFill)
                    {
                        if (hAlign == 0) x = 0;
                        else if (hAlign == 2) x = info.Width - w;
                        if (vAlign == 0) y = 0;
                        else if (vAlign == 2) y = info.Height - h;
                    }

                    canvas.DrawBitmap(_skFrameBitmap, new SkiaSharp.SKRect(x, y, x + w, y + h));
                }
            }
        }

        [System.Runtime.InteropServices.ComImport]
        [System.Runtime.InteropServices.Guid("5B0D3235-4DB1-4A45-9100-2414344B004F")]
        [System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMemoryBufferByteAccess
        {
            unsafe void GetBuffer(out byte* buffer, out uint capacity);
        }

        public MediaPlayer GetOrCreateMediaPlayer()
        {
            var mp = _internalMediaPlayer?.MediaPlayer;
            if (mp == null)
            {
                RecreateMediaPlayerElement();
                mp = new MediaPlayer
                {
                    IsLoopingEnabled = true,
                    AutoPlay = true,
                    Volume = _settings.VideoVolume,
                    IsMuted = _settings.VideoVolume <= 0
                };

                // Ensure loop mode continues to work for successive videos loaded in the same player
                mp.MediaOpened += (s, args) =>
                {
                    if (s is MediaPlayer player)
                    {
                        player.IsLoopingEnabled = true;
                    }
                };
                _internalMediaPlayer!.SetMediaPlayer(mp);
            }


            mp.IsVideoFrameServerEnabled = false;

            _internalMediaPlayer!.Visibility = Visibility.Visible;
            _internalMediaPlayer!.Opacity = 1.0;
            MediaPlayerContainer.Visibility = Visibility.Visible;

            InternalPageCanvas.Visibility = Visibility.Collapsed;
            VideoVisualHost.Visibility = Visibility.Collapsed;
            return mp;
        }

        private void OnVideoFrameAvailable(MediaPlayer sender, object args)
        {
            try
            {
                var session = sender.PlaybackSession;
                if (session == null) return;

                uint width = 0;
                uint height = 0;
                try
                {
                    width = session.NaturalVideoWidth;
                    height = session.NaturalVideoHeight;
                }
                catch { return; }

                if (width == 0 || height == 0) return;

                lock (_frameLock)
                {
                    if (_frameBitmap == null ||
                        _frameBitmap.PixelWidth != (int)width ||
                        _frameBitmap.PixelHeight != (int)height)
                    {
                        _frameBitmap?.Dispose();
                        _frameBitmap = new Windows.Graphics.Imaging.SoftwareBitmap(
                            Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                            (int)width,
                            (int)height,
                            Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);

                        _skFrameBitmap?.Dispose();
                        _skFrameBitmap = new SkiaSharp.SKBitmap((int)width, (int)height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                    }

                    dynamic dSender = sender;
                    dSender.CopyFrameToSoftwareBitmap(_frameBitmap);


                    UpdateSKFrameBitmap();
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    InternalPageCanvas.Invalidate();
                });
            }
            catch { }
        }

        private unsafe void UpdateSKFrameBitmap()
        {
            if (_frameBitmap == null || _skFrameBitmap == null) return;
            using (var buffer = _frameBitmap.LockBuffer(BitmapBufferAccessMode.Read))
            using (var reference = buffer.CreateReference())
            {
                ((IMemoryBufferByteAccess)reference).GetBuffer(out byte* dataIn, out uint capacity);

                var desc = buffer.GetPlaneDescription(0);
                int inputStride = desc.Stride;
                int outputStride = _skFrameBitmap.RowBytes;
                int widthInBytes = _frameBitmap.PixelWidth * 4;
                int height = _frameBitmap.PixelHeight;

                byte* dataOut = (byte*)_skFrameBitmap.GetPixels();

                if (inputStride == outputStride && inputStride == widthInBytes)
                {

                    System.Buffer.MemoryCopy(dataIn, dataOut, capacity, (uint)(outputStride * height));
                }
                else
                {

                    for (int y = 0; y < height; y++)
                    {
                        System.Buffer.MemoryCopy(
                            dataIn + (y * inputStride),
                            dataOut + (y * outputStride),
                            (uint)widthInBytes,
                            (uint)widthInBytes);
                    }
                }
            }
        }

        public void SetupPlayer(MediaPlayer player)
        {
            if (_internalMediaPlayer == null) RecreateMediaPlayerElement();
            if (_internalMediaPlayer!.MediaPlayer != player)
            {
                _internalMediaPlayer.SetMediaPlayer(player);
            }
            player.Volume = _settings.VideoVolume;
            player.IsMuted = _settings.VideoVolume <= 0;
            _internalMediaPlayer.Visibility = Visibility.Visible;
            MediaPlayerContainer.Visibility = Visibility.Visible;
            VideoVisualHost.Visibility = Visibility.Collapsed;
        }

        public void UpdateVolume()
        {
            var mp = _internalMediaPlayer?.MediaPlayer;
            if (mp != null)
            {
                mp.Volume = _settings.VideoVolume;
                mp.IsMuted = _settings.VideoVolume <= 0;
            }
        }

        public void SetContentAlignment(HorizontalAlignment h, VerticalAlignment v)
        {
            InternalPageImage.HorizontalAlignment = h;
            InternalPageImage.VerticalAlignment = v;
            if (_internalMediaPlayer != null)
            {
                _internalMediaPlayer.HorizontalAlignment = h;
                _internalMediaPlayer.VerticalAlignment = v;
            }

            VideoVisualHost.HorizontalAlignment = h;
            VideoVisualHost.VerticalAlignment = v;

            UpdateVideoVisualSize();
        }

        internal void InvokeVideoSizeChanged(Windows.Foundation.Size size)
        {
            UpdateVideoVisualSize();
            VideoSizeChanged?.Invoke(this, size);
        }

        private bool _wasPlayingBeforeTransition = false;

        public void PauseVideo()
        {
            var mp = _internalMediaPlayer?.MediaPlayer;
            if (mp != null)
            {
                var session = mp.PlaybackSession;
                _wasPlayingBeforeTransition = (session.PlaybackState == MediaPlaybackState.Playing);
                if (_wasPlayingBeforeTransition)
                {
                    mp.Pause();
                }
            }
        }

        public void ResumeVideo()
        {
            var mp = _internalMediaPlayer?.MediaPlayer;
            if (mp != null && _wasPlayingBeforeTransition)
            {
                mp.Play();
                _wasPlayingBeforeTransition = false;
            }
        }

        internal void UpdateVideoVisualSize(double overrideW = -1, double overrideH = -1)
        {
            var mp = _internalMediaPlayer?.MediaPlayer;
            if (mp == null || !IsVideoContent || _internalMediaPlayer == null) return;

            var session = mp.PlaybackSession;
            if (session == null) return;

            uint currentW = 0;
            uint currentH = 0;

            try
            {
                // Accessing session properties can throw COMException during transitions
                currentW = session.NaturalVideoWidth;
                currentH = session.NaturalVideoHeight;

                if (currentW > 0 && currentH > 0)
                {
                    _lastNaturalWidth = currentW;
                    _lastNaturalHeight = currentH;
                }
            }
            catch
            {
                // Fallback to last known size if current access fails
                currentW = _lastNaturalWidth;
                currentH = _lastNaturalHeight;
            }

            if (currentW == 0 || currentH == 0) return;

            double containerW = overrideW >= 0 ? overrideW : this.ActualWidth;
            double containerH = overrideH >= 0 ? overrideH : this.ActualHeight;
            if (containerW <= 0 || containerH <= 0) return;

            double videoW = currentW;
            double videoH = currentH;

            if (_internalMediaPlayer.Stretch == Stretch.Fill)
            {
                _internalMediaPlayer.Width = double.NaN;
                _internalMediaPlayer.Height = double.NaN;
                _internalMediaPlayer.Margin = new Thickness(0);
                return;
            }

            double scale = System.Math.Min(containerW / videoW, containerH / videoH);
            if (_internalMediaPlayer.Stretch == Stretch.UniformToFill)
            {
                scale = System.Math.Max(containerW / videoW, containerH / videoH);
            }
            else if (_internalMediaPlayer.Stretch == Stretch.None)
            {
                scale = 1.0;
            }

            double targetW = videoW * scale;
            double targetH = videoH * scale;

            _internalMediaPlayer.Width = targetW;
            _internalMediaPlayer.Height = targetH;

            // 4分割レイアウトなどでコンテンツの端を揃えるための位置調整
            // 位置の参照元には、書き換えの発生しない InternalPageImage のアライメントを使用する
            double left = 0;
            double top = 0;
            var hAlign = InternalPageImage.HorizontalAlignment;
            var vAlign = InternalPageImage.VerticalAlignment;

            if (hAlign == HorizontalAlignment.Center) left = (containerW - targetW) / 2;
            else if (hAlign == HorizontalAlignment.Right) left = containerW - targetW;

            if (vAlign == VerticalAlignment.Center) top = (containerH - targetH) / 2;
            else if (vAlign == VerticalAlignment.Bottom) top = containerH - targetH;

            _internalMediaPlayer.Margin = new Thickness(left, top, 0, 0);
            _internalMediaPlayer.HorizontalAlignment = HorizontalAlignment.Left;
            _internalMediaPlayer.VerticalAlignment = VerticalAlignment.Top;
        }

        private void InternalRootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            ShowControls();
        }

        private void TimelineSlider_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingSlider = true;
            ShowControls();
        }

        private void TimelineSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingSlider = false;
            try
            {
                var mp = _internalMediaPlayer?.MediaPlayer;
                if (mp != null)
                {
                    mp.PlaybackSession.Position = TimeSpan.FromSeconds(TimelineSlider.Value);
                }
            }
            catch (System.Exception)
            {
            }
        }

        private void UpdateSlider()
        {
            var player = _internalMediaPlayer?.MediaPlayer;
            if (_isDraggingSlider || player == null) return;

            try
            {
                var session = player.PlaybackSession;
                if (session == null) return;


                if (session.PlaybackState == MediaPlaybackState.Opening ||
                    session.PlaybackState == MediaPlaybackState.None) return;

                if (session.NaturalDuration.TotalSeconds > 0)
                {
                    TimelineSlider.Maximum = session.NaturalDuration.TotalSeconds;
                    TimelineSlider.Value = session.Position.TotalSeconds;
                    TimeText.Text = $"{FormatTime(session.Position)} / {FormatTime(session.NaturalDuration)}";

                    // 蜀 逕溘い繧､繧ｳ繝ｳ縺ｮ譖ｴ譁
                    UpdatePlayPauseIcon(session.PlaybackState);
                }
            }
            catch (System.Exception)
            {

            }
        }

        private static string FormatTime(TimeSpan time) => $"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}";

        private void UpdatePlayPauseIcon(MediaPlaybackState state)
        {
            PlayPauseIcon.Glyph = state == MediaPlaybackState.Playing ? "\uE769" : "\uE768";
        }

        public void ShowControls()
        {
            if (IsVideoContent)
            {
                var mainView = ((App)Application.Current).MainView;
                if (mainView?.SlideshowManager?.IsSlideshowRunning == true)
                {
                    CustomTransportPanel.Visibility = Visibility.Collapsed;
                    return;
                }
                CustomTransportPanel.Visibility = Visibility.Visible;

                _hideTimer.Stop();
                _hideTimer.Start();

                if (!_sliderUpdateTimer.IsEnabled) _sliderUpdateTimer.Start();
            }
        }

        private void InternalRootGrid_PointerExited(object sender, PointerRoutedEventArgs e)
        {

            _hideTimer.Stop();

            bool hadFocus = false;
            try
            {
                var focused = (this.IsLoaded && this.XamlRoot != null ? Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(this.XamlRoot) : null);
                if (focused is DependencyObject dep && IsChildOf(dep, CustomTransportPanel))
                {
                    hadFocus = true;
                }
            }
            catch { }

            CustomTransportPanel.Visibility = Visibility.Collapsed;
            if (hadFocus)
            {
                WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
            }
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            var player = _internalMediaPlayer?.MediaPlayer;
            if (player == null) return;

            var session = player.PlaybackSession;
            if (session.PlaybackState == MediaPlaybackState.Playing)
                player.Pause();
            else
                player.Play();

            UpdatePlayPauseIcon(session.PlaybackState);
            ShowControls();
        }

        private void TimelineSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {

            var player = _internalMediaPlayer?.MediaPlayer;
            if (_isDraggingSlider && player != null)
            {
                try
                {
                    player.PlaybackSession.Position = TimeSpan.FromSeconds(e.NewValue);
                    ShowControls();
                }
                catch (System.Exception)
                {
                }
            }
        }

        private void TimelineSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingSlider = false;
        }

        public async Task ResetPlaybackAsync()
        {
            // 実行中のロードタスクがあればキャンセル
            _activeLoadCts?.Cancel();
            _activeLoadCts?.Dispose();
            _activeLoadCts = null;

            await _loadingSemaphore.WaitAsync();
            try
            {
                var mp = _internalMediaPlayer?.MediaPlayer;
                if (mp != null)
                {
                    try { mp.Pause(); } catch { }

                    // イベント解除
                    mp.MediaOpened -= _onMediaOpenedHandler;
                    mp.MediaFailed -= _onMediaFailedHandler;
                    mp.VideoFrameAvailable -= OnVideoFrameAvailable;

                    // ソースの解除（明示的にnullをセット）
                    try { mp.Source = null; } catch { }
                    try { _internalMediaPlayer?.Source = null; } catch { }

                    // プレイヤーがソースを解放するまで少し待機
                    await Task.Delay(20);
                }

                lock (_frameLock)
                {
                    _frameBitmap?.Dispose();
                    _frameBitmap = null;
                    _skFrameBitmap?.Dispose();
                    _skFrameBitmap = null;
                }

                IsVideoContent = false;
                IsMediaReady = false;
                _lastNaturalWidth = 0;
                _lastNaturalHeight = 0;
                if (_ffmpegSource != null)
                {
                    try { _ffmpegSource.PlaybackSession = null; } catch { }
                    _ffmpegSource.Dispose();
                    _ffmpegSource = null;
                }

                if (_currentSoftwareSource != null)
                {
                    _currentSoftwareSource.Dispose();
                    _currentSoftwareSource = null;
                }
                PageImage.Source = null;
                PageImage.Opacity = 1.0;
                if (_internalMediaPlayer != null)
                {
                    _internalMediaPlayer.Width = double.NaN;
                    _internalMediaPlayer.Height = double.NaN;
                    _internalMediaPlayer.Margin = new Thickness(0);
                    _internalMediaPlayer.Visibility = Visibility.Collapsed;
                }
                VideoVisualHost.Visibility = Visibility.Collapsed;
                MediaPlayerContainer.Visibility = Visibility.Collapsed;
                InternalPageCanvas.Visibility = Visibility.Collapsed;

                bool hadFocus = false;
                try
                {
                    var focused = (this.IsLoaded && this.XamlRoot != null ? Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(this.XamlRoot) : null);
                    if (focused is DependencyObject dep && IsChildOf(dep, CustomTransportPanel))
                    {
                        hadFocus = true;
                    }
                }
                catch { }

                CustomTransportPanel.Visibility = Visibility.Collapsed;
                if (hadFocus)
                {
                    WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
                }

                _sliderUpdateTimer.Stop();
                _hideTimer.Stop();
                _resizeDebounceTimer.Stop();

                // 重いリソース（FFmpeg）を解放
                System.GC.Collect(1, System.GCCollectionMode.Optimized, false);

                // クールダウン
                await Task.Delay(20);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ViewerPageControl] ResetPlaybackAsync Error: {ex.Message}");
            }
            finally
            {
                _loadingSemaphore.Release();
            }
        }

        public void ResetPlayback()
        {
            _ = ResetPlaybackAsync();
        }

        private Windows.Foundation.TypedEventHandler<MediaPlayer, object>? _onMediaOpenedHandler;
        private Windows.Foundation.TypedEventHandler<MediaPlayer, MediaPlayerFailedEventArgs>? _onMediaFailedHandler;

        public void SetMediaHandlers(Windows.Foundation.TypedEventHandler<MediaPlayer, object> opened, Windows.Foundation.TypedEventHandler<MediaPlayer, MediaPlayerFailedEventArgs> failed)
        {
            var mp = _internalMediaPlayer?.MediaPlayer;
            if (mp != null)
            {
                mp.MediaOpened -= _onMediaOpenedHandler;
                mp.MediaFailed -= _onMediaFailedHandler;
                _onMediaOpenedHandler = opened;
                _onMediaFailedHandler = failed;
                mp.MediaOpened += _onMediaOpenedHandler;
                mp.MediaFailed += _onMediaFailedHandler;
            }
        }

        private static bool IsChildOf(DependencyObject child, DependencyObject parent)
        {
            if (child == null || parent == null) return false;
            if (child == parent) return true;
            var current = child;
            while (current != null)
            {
                if (current == parent) return true;
                current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
            }
            return false;
        }
    }
}
