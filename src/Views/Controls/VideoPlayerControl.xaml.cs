using CommunityToolkit.Mvvm.Messaging;
using FFmpegInteropX;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using quick_image_viewer.Common;
using quick_image_viewer.Helpers;
using quick_image_viewer.Managers;
using SkiaSharp;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Playback;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace quick_image_viewer.Views.Controls
{
    public sealed partial class VideoPlayerControl : UserControl
    {
        public event System.EventHandler<Windows.Foundation.Size>? VideoSizeChanged;
        public Action? InvalidateCanvasRequested;

        private MediaPlayerElement? _internalMediaPlayer;
        public MediaPlayerElement PagePlayer
        {
            get
            {
                if (_internalMediaPlayer == null)
                {
                    RecreateMediaPlayerElement();
                }

                return _internalMediaPlayer ?? throw new InvalidOperationException("MediaPlayerElement not initialized");
            }
        }

        private readonly Microsoft.UI.Xaml.DispatcherTimer _hideTimer;
        private readonly Microsoft.UI.Xaml.DispatcherTimer _sliderUpdateTimer;
        private readonly Microsoft.UI.Xaml.DispatcherTimer _resizeDebounceTimer;
        private double _pendingWidth;
        private double _pendingHeight;
        private Stretch _pendingStretch;
        private HorizontalAlignment _pendingHAlign;
        private VerticalAlignment _pendingVAlign;
        private bool _isDraggingSlider = false;
        private bool _isUpdatingSliderFromCode = false;
        private readonly Interfaces.ISettingsManager _settings;

        public void RecreateMediaPlayerElement()
        {
            if (MediaPlayerContainer == null)
            {
                return;
            }

            if (_internalMediaPlayer != null)
            {
                try
                {
                    var oldPlayer = _internalMediaPlayer.MediaPlayer;
                    if (oldPlayer != null)
                    {
                        DetachMediaPlayer(oldPlayer);
                    }
                    _internalMediaPlayer.Source = null;
                }
                catch (Exception ex) { AppLog.Error("VideoPlayerControl", "RecreateMediaPlayerElement detach failed: ", ex); }
                try { MediaPlayerContainer.Children.Remove(_internalMediaPlayer); }
                catch (Exception ex) { AppLog.Error("VideoPlayerControl", "RecreateMediaPlayerElement remove child failed: ", ex); }
                _internalMediaPlayer = null;
            }

            _pendingStretch = (Stretch)_settings.ImageStretchMode;
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
                Visibility = Visibility.Collapsed,
                Stretch = _pendingStretch
            };

            MediaPlayerContainer.Children.Add(_internalMediaPlayer);
        }

        private void DetachMediaPlayer(MediaPlayer player)
        {
            try { player.MediaOpened -= _onMediaOpenedHandler; } catch (Exception ex) { AppLog.Error("VideoPlayerControl", "Detach MediaOpened failed: ", ex); }
            try { player.MediaFailed -= _onMediaFailedHandler; } catch (Exception ex) { AppLog.Error("VideoPlayerControl", "Detach MediaFailed failed: ", ex); }
            try { player.VideoFrameAvailable -= OnVideoFrameAvailable; } catch (Exception ex) { AppLog.Error("VideoPlayerControl", "Detach VideoFrameAvailable failed: ", ex); }
            try { player.Pause(); } catch (Exception ex) { AppLog.Error("VideoPlayerControl", "Pause during detach failed: ", ex); }
            try { player.Source = null; } catch (Exception ex) { AppLog.Error("VideoPlayerControl", "Clear source during detach failed: ", ex); }

            _onMediaOpenedHandler = null;
            _onMediaFailedHandler = null;
        }

        public Windows.Media.Playback.MediaPlayer CreateNewMediaPlayer()
        {
            if (_internalMediaPlayer == null)
            {
                RecreateMediaPlayerElement();
            }

            if (_internalMediaPlayer == null)
            {
                throw new InvalidOperationException("MediaPlayerElement container is not ready");
            }

            var oldPlayer = _internalMediaPlayer.MediaPlayer;
            if (oldPlayer != null)
            {
                DetachMediaPlayer(oldPlayer);
            }

            var mp = new Windows.Media.Playback.MediaPlayer();
            _internalMediaPlayer.SetMediaPlayer(mp);
            ApplyRenderMode();
            return mp;
        }

        // Frame Server Mode
        private Windows.Graphics.Imaging.SoftwareBitmap? _frameBitmap;
        private SkiaSharp.SKBitmap? _skFrameBitmap;
        private readonly object _frameLock = new();
        private long _videoFrameCount = 0;
        private DispatcherTimer? _frameServerFallbackTimer;
        private bool _frameServerCopyUnsupported = false;
        private const bool PreferFfmpegVideoFrames = true;
        private static readonly MethodInfo? _copyFrameToSoftwareBitmapMethod =
            typeof(MediaPlayer).GetMethod("CopyFrameToSoftwareBitmap", new[] { typeof(SoftwareBitmap) });
        private string? _currentVideoPath;
        private FrameGrabber? _ffmpegFrameGrabber;
        private Stream? _ffmpegArchiveStream;
        private IRandomAccessStream? _ffmpegArchiveRandomAccessStream;
        private CancellationTokenSource? _ffmpegFrameCts;
        private Task? _ffmpegFrameLoopTask;
        private bool _ffmpegFrameTypeLogged = false;
        private int _ffmpegLoopState = 0; // 0: stopped, 1: starting, 2: running
        private long _ffmpegLoopGeneration = 0;
        private const int MaxAnime4KFrameDim = 1280;
        private int _lastConfiguredDecodeW = -1;
        private int _lastConfiguredDecodeH = -1;
        private FFmpegMediaSource? _ffmpegSource;
        public bool IsVideoContent { get; set; } = false;
        public bool IsMediaReady { get; set; } = false;
        public bool HasProcessedFrame
        {
            get
            {
                lock (_frameLock)
                {
                    return _skFrameBitmap != null;
                }
            }
        }

        // Slide Animation States for Video
        private bool _panInitialized = false;
        private float _panStartX = 0.5f;
        private float _panStartY = 0.5f;
        private float _panEndX = 0.5f;
        private float _panEndY = 0.5f;
        private DateTime _panStartTime;
        private double _panDurationMs = 10000;
        private static readonly Random _panRand = new Random();

        public void ResetPanAnimation()
        {
            _panInitialized = false;
        }

        public double VideoAspectRatio
        {
            get
            {
                uint w = _lastNaturalWidth;
                uint h = _lastNaturalHeight;
                if (w > 0 && h > 0) return (double)w / h;

                var mp = _internalMediaPlayer?.MediaPlayer;
                if (mp != null)
                {
                    try
                    {
                        var session = mp.PlaybackSession;
                        if (session != null)
                        {
                            var sw = session.NaturalVideoWidth;
                            var sh = session.NaturalVideoHeight;
                            if (sw > 0 && sh > 0) return (double)sw / sh;
                        }
                    }
                    catch (Exception ex) { AppLog.Error("VideoPlayerControl", "VideoAspectRatio read failed: ", ex); }
                }
                return 0.75;
            }
        }

        private uint _lastNaturalWidth = 0;
        private uint _lastNaturalHeight = 0;
        private bool _isLayoutUpdateQueued = false;

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

        public VideoPlayerControl()
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
                catch (Exception ex) { AppLog.Error("VideoPlayerControl", "Hide timer focus check failed: ", ex); }

                CustomTransportPanel.Visibility = Visibility.Collapsed;
                if (hadFocus)
                {
                    WeakReferenceMessenger.Default.Send(new ViewModels.FocusRequestMessage());
                }
            };

            _sliderUpdateTimer = new Microsoft.UI.Xaml.DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(100) };
            _sliderUpdateTimer.Tick += (s, e) => UpdateSlider();

            _resizeDebounceTimer = new Microsoft.UI.Xaml.DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(Constants.LAYOUT_DEBOUNCE_MS) };
            _resizeDebounceTimer.Tick += (s, e) =>
            {
                _resizeDebounceTimer.Stop();
                UpdateVideoVisualSize(_pendingStretch, _pendingHAlign, _pendingVAlign, _pendingWidth, _pendingHeight);
            };

            this.SizeChanged += VideoPlayerControl_SizeChanged;
            this.Loaded += (s, e) =>
            {
                if (_internalMediaPlayer == null)
                {
                    RecreateMediaPlayerElement();
                }
            };
            this.Unloaded += (s, e) =>
            {
                var mainView = ((App)Application.Current).MainView;
                if (mainView?.ViewModel != null)
                {
                    mainView.ViewModel.IsVideoTransportHovered = false;
                }
            };
        }

        private void VideoPlayerControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _pendingWidth = e.NewSize.Width;
            _pendingHeight = e.NewSize.Height;
            UpdateVideoVisualSize(_pendingStretch, _pendingHAlign, _pendingVAlign, _pendingWidth, _pendingHeight);
            QueueVideoVisualSizeUpdateAfterLayout();
            _resizeDebounceTimer.Stop();
            _resizeDebounceTimer.Start();

            UpdateClip(_pendingWidth, _pendingHeight);

        }

        private void QueueVideoVisualSizeUpdateAfterLayout()
        {
            if (!_isLayoutUpdateQueued)
            {
                _isLayoutUpdateQueued = true;
                LayoutUpdated += VideoPlayerControl_LayoutUpdated;
            }

            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                UpdateVideoVisualSize(_pendingStretch, _pendingHAlign, _pendingVAlign, ActualWidth, ActualHeight);
            });

            _ = UpdateVideoVisualSizeAfterLayoutDelayAsync();
        }

        private void VideoPlayerControl_LayoutUpdated(object? sender, object e)
        {
            LayoutUpdated -= VideoPlayerControl_LayoutUpdated;
            _isLayoutUpdateQueued = false;
            UpdateVideoVisualSize(_pendingStretch, _pendingHAlign, _pendingVAlign, ActualWidth, ActualHeight);
        }

        private async Task UpdateVideoVisualSizeAfterLayoutDelayAsync()
        {
            await Task.Delay(32);
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateVideoVisualSize(_pendingStretch, _pendingHAlign, _pendingVAlign, ActualWidth, ActualHeight);
            });
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

        public void PaintVideoFrame(SkiaSharp.SKCanvas canvas, SkiaSharp.SKImageInfo info, Stretch stretch, int hAlign, int vAlign)
        {
            lock (_frameLock)
            {
                if (_skFrameBitmap != null)
                {
                    float scale;
                    if (stretch == Stretch.UniformToFill)
                        scale = System.Math.Max((float)info.Width / _skFrameBitmap.Width, (float)info.Height / _skFrameBitmap.Height);
                    else if (stretch == Stretch.Uniform)
                        scale = System.Math.Min((float)info.Width / _skFrameBitmap.Width, (float)info.Height / _skFrameBitmap.Height);
                    else
                        scale = 1.0f;

                    float w = _skFrameBitmap.Width * scale;
                    float h = _skFrameBitmap.Height * scale;
                    float x = 0;
                    float y = 0;

                    if (stretch == Stretch.UniformToFill && _settings.EnablePanAnimation)
                    {
                        float diffX = w - info.Width;
                        float diffY = h - info.Height;

                        bool canPanX = diffX > 0.5f;
                        bool canPanY = diffY > 0.5f;

                        if (canPanX || canPanY)
                        {
                            if (!_panInitialized)
                            {
                                _panStartX = (float)_panRand.NextDouble();
                                _panStartY = (float)_panRand.NextDouble();
                                _panEndX = (float)_panRand.NextDouble();
                                _panEndY = (float)_panRand.NextDouble();
                                _panStartTime = DateTime.Now;
                                _panDurationMs = (8000 + _panRand.NextDouble() * 7000) / _settings.PanAnimationSpeed;
                                _panInitialized = true;
                            }

                            double elapsed = (DateTime.Now - _panStartTime).TotalMilliseconds;
                            double t = elapsed / _panDurationMs;

                            if (t >= 1.0)
                            {
                                _panStartX = _panEndX;
                                _panStartY = _panEndY;
                                _panEndX = (float)_panRand.NextDouble();
                                _panEndY = (float)_panRand.NextDouble();
                                _panStartTime = DateTime.Now;
                                _panDurationMs = (8000 + _panRand.NextDouble() * 7000) / _settings.PanAnimationSpeed;
                                t = 0.0;
                            }

                            // Linear interpolation to keep moving without pauses at the ends
                            double easedT = t;

                            float curX = _panStartX + (float)(easedT * (_panEndX - _panStartX));
                            float curY = _panStartY + (float)(easedT * (_panEndY - _panStartY));

                            x = canPanX ? -diffX * curX : -diffX * 0.5f;
                            y = canPanY ? -diffY * curY : -diffY * 0.5f;
                        }
                        else
                        {
                            x = (info.Width - w) / 2f;
                            y = (info.Height - h) / 2f;
                        }
                    }
                    else
                    {
                        x = (info.Width - w) / 2f;
                        y = (info.Height - h) / 2f;

                        if (stretch != Stretch.UniformToFill)
                        {
                            if (hAlign == 0) x = 0;
                            else if (hAlign == 2) x = info.Width - w;
                            if (vAlign == 0) y = 0;
                            else if (vAlign == 2) y = info.Height - h;
                        }
                    }

                    using var paint = new SKPaint();
                    SKShader? effectShader = null;
                    if (_settings.EnableAnime4K)
                    {
                        // Moving content usually needs a stronger gain than still images to make ON/OFF visible.
                        float videoStrength = (float)Math.Clamp(_settings.Anime4KStrength, 0.1, 3.0);
                        using var frameImage = SKImage.FromBitmap(_skFrameBitmap);
                        effectShader = Anime4KEffect.CreateShader(frameImage, videoStrength);
                        if (effectShader != null)
                        {
                            paint.Shader = effectShader;
                        }
                        else
                        {
                            AppLog.Warn("VideoPlayerControl", "Anime4K shader unavailable for video frame");
                        }
                    }
                    if (effectShader != null)
                    {
                        canvas.Save();
                        canvas.Translate(x, y);
                        canvas.Scale(scale);
                        canvas.DrawRect(new SkiaSharp.SKRect(0, 0, _skFrameBitmap.Width, _skFrameBitmap.Height), paint);
                        canvas.Restore();
                    }
                    else
                    {
                        canvas.DrawBitmap(_skFrameBitmap, new SkiaSharp.SKRect(x, y, x + w, y + h), paint);
                    }
                    effectShader?.Dispose();
                }
            }
        }

        public void SetCurrentVideoPath(string? path)
        {
            _currentVideoPath = path;
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

                mp.MediaOpened += (s, args) =>
                {
                    if (s is MediaPlayer player)
                    {
                        player.IsLoopingEnabled = true;
                    }
                };
                _internalMediaPlayer!.SetMediaPlayer(mp);
            }

            _internalMediaPlayer!.Opacity = 1.0;
            ApplyRenderMode();
            return mp;
        }

        public bool IsFrameServerRenderMode => _settings.EnableAnime4K;

        public void ApplyRenderMode()
        {
            var mp = _internalMediaPlayer?.MediaPlayer;
            bool useFrameServer = _settings.EnableAnime4K && !_frameServerCopyUnsupported && !PreferFfmpegVideoFrames;
            bool shouldResumePlayback = false;

            if (mp != null)
            {
                try
                {
                    var state = mp.PlaybackSession?.PlaybackState ?? MediaPlaybackState.None;
                    shouldResumePlayback = state == MediaPlaybackState.Playing;
                }
                catch (Exception ex)
                {
                    AppLog.Error("VideoPlayerControl", "ApplyRenderMode playback state read failed", ex);
                }
            }

            if (mp != null)
            {
                try { mp.VideoFrameAvailable -= OnVideoFrameAvailable; }
                catch (Exception ex) { AppLog.Error("VideoPlayerControl", "ApplyRenderMode detach frame handler failed", ex); }

                try
                {
                    mp.IsVideoFrameServerEnabled = useFrameServer;
                    if (useFrameServer)
                    {
                        mp.VideoFrameAvailable += OnVideoFrameAvailable;
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Error("VideoPlayerControl", "ApplyRenderMode frame server switch failed", ex);
                }
            }

            if (_internalMediaPlayer != null)
            {
                _internalMediaPlayer.Visibility = Visibility.Visible;
                _internalMediaPlayer.Opacity = useFrameServer ? 0.0 : 1.0;
            }

            MediaPlayerContainer.Visibility = Visibility.Visible;
            VideoVisualHost.Visibility = Visibility.Collapsed;

            if (_settings.EnableAnime4K)
            {
                EnsureFfmpegFrameLoopStarted();
                _ = CaptureCurrentFrameForAnime4KAsync();
            }
            else
            {
                StopFrameServerFallbackWatch();
                StopFfmpegFrameLoop();
            }

            if (mp != null && IsVideoContent && shouldResumePlayback)
            {
                try
                {
                    var state = mp.PlaybackSession?.PlaybackState ?? MediaPlaybackState.None;
                    if (state != MediaPlaybackState.Playing)
                    {
                        mp.Play();
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Error("VideoPlayerControl", "ApplyRenderMode playback resume failed", ex);
                }
            }
        }

        public void StartFrameServerFallbackWatch()
        {
            if (!IsFrameServerRenderMode) return;
            var mp = _internalMediaPlayer?.MediaPlayer;
            if (mp == null) return;

            StopFrameServerFallbackWatch();

            long startFrameCount = Interlocked.Read(ref _videoFrameCount);
            _frameServerFallbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            _frameServerFallbackTimer.Tick += (_, _) =>
            {
                StopFrameServerFallbackWatch();

                if (!IsFrameServerRenderMode) return;
                if (Interlocked.Read(ref _videoFrameCount) > startFrameCount) return;

                try
                {
                    mp.VideoFrameAvailable -= OnVideoFrameAvailable;
                    mp.IsVideoFrameServerEnabled = false;
                }
                catch (Exception ex)
                {
                    AppLog.Error("VideoPlayerControl", "Frame server fallback switch failed", ex);
                }

                if (_internalMediaPlayer != null)
                {
                    _internalMediaPlayer.Visibility = Visibility.Visible;
                    _internalMediaPlayer.Opacity = 1.0;
                }
                MediaPlayerContainer.Visibility = Visibility.Visible;
                InvalidateCanvasRequested?.Invoke();

                AppLog.Warn("VideoPlayerControl", "Frame server fallback activated (no video frame received)");

                if (_settings.EnableAnime4K)
                {
                    EnsureFfmpegFrameLoopStarted();
                }
            };
            _frameServerFallbackTimer.Start();
        }

        private void StopFrameServerFallbackWatch()
        {
            if (_frameServerFallbackTimer == null) return;
            _frameServerFallbackTimer.Stop();
            _frameServerFallbackTimer = null;
        }

        private void OnVideoFrameAvailable(MediaPlayer sender, object args)
        {
            try
            {
                if (_copyFrameToSoftwareBitmapMethod == null)
                {
                    DisableFrameServerBecauseUnsupported(sender, "CopyFrameToSoftwareBitmap is unavailable on this runtime");
                    return;
                }

                var session = sender.PlaybackSession;
                if (session == null) return;

                uint width = 0;
                uint height = 0;
                try
                {
                    width = session.NaturalVideoWidth;
                    height = session.NaturalVideoHeight;
                }
                catch (Exception ex)
                {
                    AppLog.Error("VideoPlayerControl", "Natural video size read failed: ", ex);
                    return;
                }

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

                    try
                    {
                        _copyFrameToSoftwareBitmapMethod.Invoke(sender, new object[] { _frameBitmap });
                    }
                    catch (TargetInvocationException tie)
                    {
                        throw tie.InnerException ?? tie;
                    }

                    UpdateSKFrameBitmap();
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    Interlocked.Increment(ref _videoFrameCount);
                    InvalidateCanvasRequested?.Invoke();
                });
            }
            catch (Exception ex)
            {
                if (ex is MissingMethodException || ex is MissingMemberException)
                {
                    DisableFrameServerBecauseUnsupported(sender, ex.Message);
                    return;
                }
                DisableFrameServerBecauseUnsupported(sender, $"OnVideoFrameAvailable failed: {ex.Message}");
            }
        }

        private void DisableFrameServerBecauseUnsupported(MediaPlayer sender, string reason)
        {
            _frameServerCopyUnsupported = true;
            StopFrameServerFallbackWatch();

            try { sender.VideoFrameAvailable -= OnVideoFrameAvailable; }
            catch (Exception ex) { AppLog.Error("VideoPlayerControl", "Disable unsupported frame server detach failed", ex); }

            try { sender.IsVideoFrameServerEnabled = false; }
            catch (Exception ex) { AppLog.Error("VideoPlayerControl", "Disable unsupported frame server switch failed", ex); }

            if (_internalMediaPlayer != null)
            {
                _internalMediaPlayer.Visibility = Visibility.Visible;
                _internalMediaPlayer.Opacity = 1.0;
            }
            MediaPlayerContainer.Visibility = Visibility.Visible;

            AppLog.Warn("VideoPlayerControl", $"Anime4K video frame-server disabled: {reason}");
            DispatcherQueue.TryEnqueue(() => InvalidateCanvasRequested?.Invoke());

            if (_settings.EnableAnime4K)
            {
                EnsureFfmpegFrameLoopStarted();
            }
        }

        private void EnsureFfmpegFrameLoopStarted()
        {
            if (!_settings.EnableAnime4K) return;
            if (string.IsNullOrWhiteSpace(_currentVideoPath)) return;
            if (Interlocked.CompareExchange(ref _ffmpegLoopState, 1, 0) != 0) return;
            long generation = Interlocked.Increment(ref _ffmpegLoopGeneration);
            _ = StartFfmpegFrameLoopAsync(_currentVideoPath!, generation);
        }

        private async Task StartFfmpegFrameLoopAsync(string path, long generation)
        {
            StopFfmpegFrameLoop(resetGeneration: false);
            _ffmpegFrameCts = new CancellationTokenSource();
            var token = _ffmpegFrameCts.Token;

            try
            {
                if (generation != Interlocked.Read(ref _ffmpegLoopGeneration))
                {
                    Interlocked.Exchange(ref _ffmpegLoopState, 0);
                    return;
                }

                if (ArchiveManager.IsArchivePath(path))
                {
                    var (arc, entry) = ArchiveManager.SplitArchivePath(path);
                    _ffmpegArchiveStream = ArchiveManager.GetEntryStream(arc, entry);
                    if (_ffmpegArchiveStream == null) return;
                    _ffmpegArchiveRandomAccessStream = _ffmpegArchiveStream.AsRandomAccessStream();
                    _ffmpegFrameGrabber = await FrameGrabber.CreateFromStreamAsync(_ffmpegArchiveRandomAccessStream);
                }
                else
                {
                    _ffmpegFrameGrabber = await FrameGrabber.CreateFromFileAsync(path);
                }

                if (_ffmpegFrameGrabber == null || token.IsCancellationRequested) return;
                ConfigureFrameGrabberDecodeSize(_ffmpegFrameGrabber);

                if (generation != Interlocked.Read(ref _ffmpegLoopGeneration))
                {
                    Interlocked.Exchange(ref _ffmpegLoopState, 0);
                    return;
                }

                Interlocked.Exchange(ref _ffmpegLoopState, 2);
                _ffmpegFrameLoopTask = Task.Run(async () =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var pos = await GetPlaybackPositionAsync(token);
                            if (pos == null)
                            {
                                await Task.Delay(120, token);
                                continue;
                            }

                            await ExtractAndPresentFrameAsync(pos.Value, token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            AppLog.Error(
                                "VideoPlayerControl",
                                $"FFmpeg frame loop iteration failed ({ex.GetType().FullName}, 0x{ex.HResult:X8})",
                                ex,
                                includeStackTrace: true);
                        }

                        await Task.Delay(80, token);
                    }
                }, token);
            }
            catch (Exception ex)
            {
                AppLog.Error("VideoPlayerControl", "StartFfmpegFrameLoopAsync failed", ex);
                Interlocked.Exchange(ref _ffmpegLoopState, 0);
            }
        }

        public async Task CaptureCurrentFrameForAnime4KAsync()
        {
            if (!_settings.EnableAnime4K) return;
            if (_ffmpegFrameGrabber == null) return;

            var token = _ffmpegFrameCts?.Token ?? CancellationToken.None;
            var pos = await GetPlaybackPositionAsync(token);
            if (pos == null) return;

            try
            {
                await ExtractAndPresentFrameAsync(pos.Value, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                AppLog.Error("VideoPlayerControl", "CaptureCurrentFrameForAnime4KAsync failed", ex);
            }
        }

        private async Task ExtractAndPresentFrameAsync(TimeSpan position, CancellationToken token)
        {
            if (_ffmpegFrameGrabber == null) return;
            ConfigureFrameGrabberDecodeSize(_ffmpegFrameGrabber);

            using var frame = await _ffmpegFrameGrabber.ExtractVideoFrameAsync(position, exactSeek: false, maxFrameSkip: 3);
            if (frame == null) return;

            if (!_ffmpegFrameTypeLogged)
            {
                _ffmpegFrameTypeLogged = true;
                AppLog.Info("VideoPlayerControl", $"FFmpeg frame type: {frame.GetType().FullName}");
            }

            using var encodedStream = new InMemoryRandomAccessStream();
            await frame.EncodeAsJpegAsync(encodedStream);
            token.ThrowIfCancellationRequested();

            encodedStream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(encodedStream);
            using var software = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            if (software == null) return;

            lock (_frameLock)
            {
                _frameBitmap?.Dispose();
                _frameBitmap = SoftwareBitmap.Copy(software);

                if (_skFrameBitmap == null
                    || _skFrameBitmap.Width != _frameBitmap.PixelWidth
                    || _skFrameBitmap.Height != _frameBitmap.PixelHeight)
                {
                    _skFrameBitmap?.Dispose();
                    _skFrameBitmap = new SKBitmap(_frameBitmap.PixelWidth, _frameBitmap.PixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
                }

                UpdateSKFrameBitmap();
            }

            Interlocked.Increment(ref _videoFrameCount);
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_settings.EnableAnime4K && _internalMediaPlayer != null)
                {
                    _internalMediaPlayer.Opacity = 0.0;
                }
                InvalidateCanvasRequested?.Invoke();
            });
        }

        private async Task<TimeSpan?> GetPlaybackPositionAsync(CancellationToken token)
        {
            var tcs = new TaskCompletionSource<TimeSpan?>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool enqueued = DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    var mp = _internalMediaPlayer?.MediaPlayer;
                    var session = mp?.PlaybackSession;
                    tcs.TrySetResult(session?.Position);
                }
                catch (Exception ex)
                {
                    AppLog.Error("VideoPlayerControl", "GetPlaybackPositionAsync failed", ex);
                    tcs.TrySetResult(null);
                }
            });

            if (!enqueued) return null;

            using (token.Register(() => tcs.TrySetCanceled(token)))
            {
                try
                {
                    return await tcs.Task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }
        }

        private void ConfigureFrameGrabberDecodeSize(FrameGrabber frameGrabber)
        {
            try
            {
                dynamic stream = frameGrabber.CurrentVideoStream;
                int srcW = (int)stream.PixelWidth;
                int srcH = (int)stream.PixelHeight;
                if (srcW <= 0 || srcH <= 0) return;

                double viewW = ActualWidth > 0 ? ActualWidth : srcW;
                double viewH = ActualHeight > 0 ? ActualHeight : srcH;
                double targetW = Math.Min(srcW, Math.Max(320, viewW * 1.25));
                double targetH = Math.Min(srcH, Math.Max(180, viewH * 1.25));
                double scale = Math.Min(targetW / srcW, targetH / srcH);
                scale = Math.Min(scale, 1.0);

                int decodeW = Math.Max(2, ((int)Math.Round(srcW * scale)) & ~1);
                int decodeH = Math.Max(2, ((int)Math.Round(srcH * scale)) & ~1);

                int maxDim = Math.Max(decodeW, decodeH);
                if (maxDim > MaxAnime4KFrameDim)
                {
                    double clampScale = (double)MaxAnime4KFrameDim / maxDim;
                    decodeW = Math.Max(2, ((int)Math.Round(decodeW * clampScale)) & ~1);
                    decodeH = Math.Max(2, ((int)Math.Round(decodeH * clampScale)) & ~1);
                }

                if (decodeW == _lastConfiguredDecodeW && decodeH == _lastConfiguredDecodeH) return;

                frameGrabber.DecodePixelWidth = decodeW;
                frameGrabber.DecodePixelHeight = decodeH;
                _lastConfiguredDecodeW = decodeW;
                _lastConfiguredDecodeH = decodeH;
            }
            catch (Exception ex)
            {
                AppLog.Error("VideoPlayerControl", "ConfigureFrameGrabberDecodeSize failed", ex);
            }
        }

        private void StopFfmpegFrameLoop(bool resetGeneration = true)
        {
            if (resetGeneration)
            {
                Interlocked.Increment(ref _ffmpegLoopGeneration);
            }
            Interlocked.Exchange(ref _ffmpegLoopState, 0);
            var cts = _ffmpegFrameCts;
            var task = _ffmpegFrameLoopTask;
            var grabber = _ffmpegFrameGrabber;
            var ras = _ffmpegArchiveRandomAccessStream;
            var stream = _ffmpegArchiveStream;

            _ffmpegFrameCts = null;
            _ffmpegFrameLoopTask = null;
            _ffmpegFrameGrabber = null;
            _ffmpegArchiveRandomAccessStream = null;
            _ffmpegArchiveStream = null;
            _lastConfiguredDecodeW = -1;
            _lastConfiguredDecodeH = -1;

            try { cts?.Cancel(); } catch { }
            if (task != null)
            {
                // Avoid surfacing expected cancellation exceptions during shutdown.
                try { ((IAsyncResult)task).AsyncWaitHandle.WaitOne(300); } catch { }
            }
            cts?.Dispose();

            (grabber as IDisposable)?.Dispose();
            ras?.Dispose();
            stream?.Dispose();
        }

        private void UpdateSKFrameBitmap()
        {
            if (_frameBitmap == null || _skFrameBitmap == null) return;

            uint size = (uint)(_frameBitmap.PixelWidth * _frameBitmap.PixelHeight * 4);
            if (size == 0) return;

            var winrtBuffer = new Windows.Storage.Streams.Buffer(size);
            _frameBitmap.CopyToBuffer(winrtBuffer);
            CryptographicBuffer.CopyToByteArray(winrtBuffer, out byte[] src);

            int widthInBytes = _frameBitmap.PixelWidth * 4;
            int height = _frameBitmap.PixelHeight;
            int dstStride = _skFrameBitmap.RowBytes;
            IntPtr dstPtr = _skFrameBitmap.GetPixels();
            if (dstPtr == IntPtr.Zero) return;

            if (dstStride == widthInBytes)
            {
                Marshal.Copy(src, 0, dstPtr, Math.Min(src.Length, dstStride * height));
                return;
            }

            int lines = Math.Min(height, src.Length / widthInBytes);
            for (int y = 0; y < lines; y++)
            {
                IntPtr linePtr = IntPtr.Add(dstPtr, y * dstStride);
                Marshal.Copy(src, y * widthInBytes, linePtr, widthInBytes);
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
            ApplyRenderMode();
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
            if (_internalMediaPlayer != null)
            {
                _internalMediaPlayer.HorizontalAlignment = h;
                _internalMediaPlayer.VerticalAlignment = v;
            }

            VideoVisualHost.HorizontalAlignment = h;
            VideoVisualHost.VerticalAlignment = v;

            _pendingHAlign = h;
            _pendingVAlign = v;

            UpdateVideoVisualSize(_pendingStretch, h, v);
        }

        internal void InvokeVideoSizeChanged(Windows.Foundation.Size size, HorizontalAlignment hAlign, VerticalAlignment vAlign)
        {
            UpdateVideoVisualSize(_pendingStretch, hAlign, vAlign);
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

        public void NotifyStretchChanged(Stretch stretch)
        {
            if (_internalMediaPlayer != null) _internalMediaPlayer.Stretch = stretch;
            _pendingStretch = stretch;
        }

        internal void UpdateVideoVisualSize(Stretch imageStretch, HorizontalAlignment hAlign, VerticalAlignment vAlign, double overrideW = -1, double overrideH = -1)
        {
            _pendingStretch = imageStretch;
            _pendingHAlign = hAlign;
            _pendingVAlign = vAlign;

            var mp = _internalMediaPlayer?.MediaPlayer;
            if (mp == null || !IsVideoContent || _internalMediaPlayer == null) return;

            var session = mp.PlaybackSession;
            if (session == null) return;

            uint currentW = 0;
            uint currentH = 0;

            try
            {
                currentW = session.NaturalVideoWidth;
                currentH = session.NaturalVideoHeight;

                if (currentW > 0 && currentH > 0)
                {
                    _lastNaturalWidth = currentW;
                    _lastNaturalHeight = currentH;
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("VideoPlayerControl", "UpdateVideoVisualSize natural size read fallback: ", ex);
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
                ApplyMediaSurfaceSize(containerW, containerH);
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

            double left = 0;
            double top = 0;

            if (_internalMediaPlayer.Stretch == Stretch.UniformToFill && _settings.EnablePanAnimation)
            {
                double diffX = targetW - containerW;
                double diffY = targetH - containerH;

                bool canPanX = diffX > 0.5;
                bool canPanY = diffY > 0.5;

                if (canPanX || canPanY)
                {
                    if (!_panInitialized)
                    {
                        _panStartX = (float)_panRand.NextDouble();
                        _panStartY = (float)_panRand.NextDouble();
                        _panEndX = (float)_panRand.NextDouble();
                        _panEndY = (float)_panRand.NextDouble();
                        _panStartTime = DateTime.Now;
                        _panDurationMs = (8000 + _panRand.NextDouble() * 7000) / _settings.PanAnimationSpeed;
                        _panInitialized = true;
                    }

                    double elapsed = (DateTime.Now - _panStartTime).TotalMilliseconds;
                    double t = elapsed / _panDurationMs;

                    if (t >= 1.0)
                    {
                        _panStartX = _panEndX;
                        _panStartY = _panEndY;
                        _panEndX = (float)_panRand.NextDouble();
                        _panEndY = (float)_panRand.NextDouble();
                        _panStartTime = DateTime.Now;
                        _panDurationMs = (8000 + _panRand.NextDouble() * 7000) / _settings.PanAnimationSpeed;
                        t = 0.0;
                    }

                    // Linear interpolation to keep moving without pauses at the ends
                    double easedT = t;

                    float curX = _panStartX + (float)(easedT * (_panEndX - _panStartX));
                    float curY = _panStartY + (float)(easedT * (_panEndY - _panStartY));

                    left = canPanX ? -diffX * curX : -diffX * 0.5;
                    top = canPanY ? -diffY * curY : -diffY * 0.5;
                }
                else
                {
                    if (hAlign == HorizontalAlignment.Center) left = (containerW - targetW) / 2;
                    else if (hAlign == HorizontalAlignment.Right) left = containerW - targetW;

                    if (vAlign == VerticalAlignment.Center) top = (containerH - targetH) / 2;
                    else if (vAlign == VerticalAlignment.Bottom) top = containerH - targetH;
                }
            }
            else
            {
                if (hAlign == HorizontalAlignment.Center) left = (containerW - targetW) / 2;
                else if (hAlign == HorizontalAlignment.Right) left = containerW - targetW;

                if (vAlign == VerticalAlignment.Center) top = (containerH - targetH) / 2;
                else if (vAlign == VerticalAlignment.Bottom) top = containerH - targetH;
            }

            _internalMediaPlayer.Margin = new Thickness(left, top, 0, 0);
            _internalMediaPlayer.HorizontalAlignment = HorizontalAlignment.Left;
            _internalMediaPlayer.VerticalAlignment = VerticalAlignment.Top;
            ApplyMediaSurfaceSize(targetW, targetH);
        }

        private void ApplyMediaSurfaceSize(double width, double height)
        {
            var mp = _internalMediaPlayer?.MediaPlayer;
            if (mp == null || width <= 0 || height <= 0) return;

            try
            {
                if (mp.PlaybackSession.PlaybackState == MediaPlaybackState.None) return;
            }
            catch (Exception ex)
            {
                AppLog.Error("VideoPlayerControl", "ApplyMediaSurfaceSize state read failed: ", ex);
                return;
            }

            double scale = 1.0;
            try
            {
                if (XamlRoot != null)
                {
                    scale = XamlRoot.RasterizationScale;
                }
            }
            catch (Exception ex) { AppLog.Error("VideoPlayerControl", "RasterizationScale read failed: ", ex); }

            uint surfaceWidth = (uint)System.Math.Max(1, width * scale);
            uint surfaceHeight = (uint)System.Math.Max(1, height * scale);
            try { mp.SetSurfaceSize(new Windows.Foundation.Size(surfaceWidth, surfaceHeight)); }
            catch (Exception ex) { AppLog.Error("VideoPlayerControl", "SetSurfaceSize failed: ", ex); }
        }

        private void InternalRootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            ShowControls();
        }

        private void InternalRootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            ShowControls();
        }

        private void CustomTransportPanel_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var mainView = ((App)Application.Current).MainView;
            if (mainView?.ViewModel != null)
            {
                mainView.ViewModel.IsVideoTransportHovered = true;
            }
        }

        private void CustomTransportPanel_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            var mainView = ((App)Application.Current).MainView;
            if (mainView?.ViewModel != null)
            {
                mainView.ViewModel.IsVideoTransportHovered = false;
            }
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
            catch (System.Exception ex)
            {
                AppLog.Error("VideoPlayerControl", "TimelineSlider_PointerReleased failed: ", ex);
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
                    _isUpdatingSliderFromCode = true;
                    TimelineSlider.Maximum = session.NaturalDuration.TotalSeconds;
                    TimelineSlider.Value = session.Position.TotalSeconds;
                    TimeText.Text = $"{FormatTime(session.Position)} / {FormatTime(session.NaturalDuration)}";
                    UpdatePlayPauseIcon(session.PlaybackState);
                    _isUpdatingSliderFromCode = false;
                }
            }
            catch (System.Exception ex)
            {
                AppLog.Error("VideoPlayerControl", "UpdateSlider failed: ", ex);
                _isUpdatingSliderFromCode = false;
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
            catch (Exception ex) { AppLog.Error("VideoPlayerControl", "PointerExited focus check failed: ", ex); }

            CustomTransportPanel.Visibility = Visibility.Collapsed;
            if (hadFocus)
            {
                WeakReferenceMessenger.Default.Send(new ViewModels.FocusRequestMessage());
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
            if (_isUpdatingSliderFromCode) return;

            var player = _internalMediaPlayer?.MediaPlayer;
            if (player != null)
            {
                try
                {
                    player.PlaybackSession.Position = TimeSpan.FromSeconds(e.NewValue);
                    ShowControls();
                }
                catch (System.Exception ex)
                {
                    AppLog.Error("VideoPlayerControl", "TimelineSlider_ValueChanged failed: ", ex);
                }
            }
        }

        private void TimelineSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingSlider = false;
        }

        public async Task ResetPlaybackAsync()
        {
            StopFfmpegFrameLoop();
            var mp = _internalMediaPlayer?.MediaPlayer;
            if (mp != null)
            {
                try { mp.Pause(); } catch (Exception ex) { AppLog.Error("VideoPlayerControl", "ResetPlayback pause failed: ", ex); }

                mp.MediaOpened -= _onMediaOpenedHandler;
                mp.MediaFailed -= _onMediaFailedHandler;
                mp.VideoFrameAvailable -= OnVideoFrameAvailable;
                _onMediaOpenedHandler = null;
                _onMediaFailedHandler = null;

                try { mp.Source = null; } catch (Exception ex) { AppLog.Error("VideoPlayerControl", "ResetPlayback clear MediaPlayer source failed: ", ex); }
                try { _internalMediaPlayer?.Source = null; } catch (Exception ex) { AppLog.Error("VideoPlayerControl", "ResetPlayback clear element source failed: ", ex); }

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
            ResetPanAnimation();
            if (_ffmpegSource != null)
            {
                try { _ffmpegSource.PlaybackSession = null; } catch (Exception ex) { AppLog.Error("VideoPlayerControl", "ResetPlayback clear FFmpeg playback session failed: ", ex); }
                _ffmpegSource.Dispose();
                _ffmpegSource = null;
            }

            if (_internalMediaPlayer != null)
            {
                _internalMediaPlayer.Width = double.NaN;
                _internalMediaPlayer.Height = double.NaN;
                _internalMediaPlayer.Margin = new Thickness(0);
                _internalMediaPlayer.Visibility = Visibility.Collapsed;
            }
            VideoVisualHost.Visibility = Visibility.Collapsed;
            MediaPlayerContainer.Visibility = Visibility.Collapsed;

            bool hadFocus = false;
            try
            {
                var focused = (this.IsLoaded && this.XamlRoot != null ? Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(this.XamlRoot) : null);
                if (focused is DependencyObject dep && IsChildOf(dep, CustomTransportPanel))
                {
                    hadFocus = true;
                }
            }
            catch (Exception ex) { AppLog.Error("VideoPlayerControl", "ResetPlayback focus check failed: ", ex); }

            CustomTransportPanel.Visibility = Visibility.Collapsed;
            if (hadFocus)
            {
                WeakReferenceMessenger.Default.Send(new ViewModels.FocusRequestMessage());
            }

            _sliderUpdateTimer.Stop();
            _hideTimer.Stop();
            _resizeDebounceTimer.Stop();

            var mainView = ((App)Application.Current).MainView;
            if (mainView?.ViewModel != null)
            {
                mainView.ViewModel.IsVideoTransportHovered = false;
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
