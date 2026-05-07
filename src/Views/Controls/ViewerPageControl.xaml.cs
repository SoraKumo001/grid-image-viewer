using CommunityToolkit.Mvvm.Messaging;
using FFmpegInteropX;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using quick_image_viewer.ViewModels;
using System;
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
        public MediaPlayerElement PagePlayer => InternalMediaPlayer;

        public ProgressRing LoadingRing => InternalLoadingRing;
        public Border FocusBorder => InternalFocusBorder;

        private readonly Microsoft.UI.Xaml.DispatcherTimer _hideTimer;
        private readonly Microsoft.UI.Xaml.DispatcherTimer _sliderUpdateTimer;
        private bool _isDraggingSlider = false;
        private readonly Interfaces.ISettingsManager _settings;

        // Frame Server Mode
        private Windows.Graphics.Imaging.SoftwareBitmap? _frameBitmap;
        private SkiaSharp.SKBitmap? _skFrameBitmap;
        private readonly object _frameLock = new();
        private FFmpegMediaSource? _ffmpegSource;
        public bool IsVideoContent { get; set; } = false;

        public FFmpegMediaSource? FFmpegSource
        {
            get => _ffmpegSource;
            set
            {
                _ffmpegSource?.Dispose();
                _ffmpegSource = value;
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

            // スライダーの操作開始と終了を確実に検知する
            TimelineSlider.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(TimelineSlider_PointerPressed), true);
            TimelineSlider.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(TimelineSlider_PointerReleased), true);

            // 分割モード対応: 自分のエリア内でのマウス移動だけを監視する
            InternalRootGrid.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(InternalRootGrid_PointerMoved), true);

            this.SizeChanged += ViewerPageControl_SizeChanged;
        }

        private void ViewerPageControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var mp = InternalMediaPlayer.MediaPlayer;
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

        public void PaintVideoFrame(SkiaSharp.SKCanvas canvas, SkiaSharp.SKImageInfo info, int hAlign, int vAlign)
        {
            lock (_frameLock)
            {
                if (_skFrameBitmap != null)
                {
                    float scale;
                    if (InternalPageImage.Stretch == Microsoft.UI.Xaml.Media.Stretch.UniformToFill)
                        scale = System.Math.Max((float)info.Width / _skFrameBitmap.Width, (float)info.Height / _skFrameBitmap.Height);
                    else if (InternalPageImage.Stretch == Microsoft.UI.Xaml.Media.Stretch.Uniform)
                        scale = System.Math.Min((float)info.Width / _skFrameBitmap.Width, (float)info.Height / _skFrameBitmap.Height);
                    else
                        scale = 1.0f;

                    float w = _skFrameBitmap.Width * scale;
                    float h = _skFrameBitmap.Height * scale;
                    float x = (info.Width - w) / 2f;
                    float y = (info.Height - h) / 2f;

                    if (InternalPageImage.Stretch != Microsoft.UI.Xaml.Media.Stretch.UniformToFill)
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
            var mp = InternalMediaPlayer.MediaPlayer;
            if (mp == null)
            {
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
                InternalMediaPlayer.SetMediaPlayer(mp);
            }

            // WinUI 3の制約: CopyFrameToSoftwareBitmap が不安定な場合があるため、
            // デフォルトでは直接描画を使用する。
            mp.IsVideoFrameServerEnabled = false;

            InternalMediaPlayer.Visibility = Visibility.Visible;
            InternalMediaPlayer.Opacity = 1.0;

            InternalPageCanvas.Visibility = Visibility.Collapsed;
            VideoVisualHost.Visibility = Visibility.Collapsed;
            return mp;
        }

        private void OnVideoFrameAvailable(MediaPlayer sender, object args)
        {
            try
            {
                uint width = sender.PlaybackSession.NaturalVideoWidth;
                uint height = sender.PlaybackSession.NaturalVideoHeight;
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

                    // SoftwareBitmapからSKBitmapへ転送
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
                    // 最適化: ストライドが一致しパディングがない場合は一括コピー
                    System.Buffer.MemoryCopy(dataIn, dataOut, capacity, (uint)(outputStride * height));
                }
                else
                {
                    // 行ごとにコピー（ストライド考慮）
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
            if (InternalMediaPlayer.MediaPlayer != player)
            {
                InternalMediaPlayer.SetMediaPlayer(player);
            }
            player.Volume = _settings.VideoVolume;
            player.IsMuted = _settings.VideoVolume <= 0;
            InternalMediaPlayer.Visibility = Visibility.Visible;
            VideoVisualHost.Visibility = Visibility.Collapsed;
        }

        public void UpdateVolume()
        {
            if (InternalMediaPlayer.MediaPlayer != null)
            {
                InternalMediaPlayer.MediaPlayer.Volume = _settings.VideoVolume;
                InternalMediaPlayer.MediaPlayer.IsMuted = _settings.VideoVolume <= 0;
            }
        }

        public void SetContentAlignment(HorizontalAlignment h, VerticalAlignment v)
        {
            InternalPageImage.HorizontalAlignment = h;
            InternalPageImage.VerticalAlignment = v;
            InternalMediaPlayer.HorizontalAlignment = h;
            InternalMediaPlayer.VerticalAlignment = v;
            // VideoVisualHost (軽量表示用) も同期
            VideoVisualHost.HorizontalAlignment = h;
            VideoVisualHost.VerticalAlignment = v;
        }

        internal void InvokeVideoSizeChanged(Windows.Foundation.Size size)
        {
            VideoSizeChanged?.Invoke(this, size);
        }

        private bool _wasPlayingBeforeTransition = false;

        public void PauseVideo()
        {
            if (InternalMediaPlayer.MediaPlayer != null)
            {
                var session = InternalMediaPlayer.MediaPlayer.PlaybackSession;
                _wasPlayingBeforeTransition = (session.PlaybackState == MediaPlaybackState.Playing);
                if (_wasPlayingBeforeTransition)
                {
                    InternalMediaPlayer.MediaPlayer.Pause();
                }
            }
        }

        public void ResumeVideo()
        {
            if (InternalMediaPlayer.MediaPlayer != null && _wasPlayingBeforeTransition)
            {
                InternalMediaPlayer.MediaPlayer.Play();
                _wasPlayingBeforeTransition = false;
            }
        }

        private void UpdateVideoVisualSize() { }

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
                if (InternalMediaPlayer.MediaPlayer != null)
                {
                    InternalMediaPlayer.MediaPlayer.PlaybackSession.Position = TimeSpan.FromSeconds(TimelineSlider.Value);
                }
            }
            catch (System.Exception)
            {
            }
        }

        private void UpdateSlider()
        {
            var player = InternalMediaPlayer.MediaPlayer;
            if (_isDraggingSlider || player == null) return;

            try
            {
                var session = player.PlaybackSession;
                if (session == null) return;

                // 読み込み中やエラー時はスキップ
                if (session.PlaybackState == MediaPlaybackState.Opening ||
                    session.PlaybackState == MediaPlaybackState.None) return;

                if (session.NaturalDuration.TotalSeconds > 0)
                {
                    TimelineSlider.Maximum = session.NaturalDuration.TotalSeconds;
                    TimelineSlider.Value = session.Position.TotalSeconds;
                    TimeText.Text = $"{FormatTime(session.Position)} / {FormatTime(session.NaturalDuration)}";

                    // 再生アイコンの更新
                    UpdatePlayPauseIcon(session.PlaybackState);
                }
            }
            catch (System.Exception)
            {
                // ここは頻繁に呼ばれるので、特定のCOMExceptionなどは無視しても良い
            }
        }

        private static string FormatTime(TimeSpan time) => $"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}";

        private void UpdatePlayPauseIcon(MediaPlaybackState state)
        {
            PlayPauseIcon.Glyph = state == MediaPlaybackState.Playing ? "\uE769" : "\uE768";
        }

        public void ShowControls()
        {
            var isVisible = IsVideoContent ||
                            InternalMediaPlayer.Visibility == Visibility.Visible ||
                            VideoVisualHost.Visibility == Visibility.Visible ||
                            InternalPageCanvas.Visibility == Visibility.Visible; // FrameServer時はCanvasがVisible
            if (isVisible)
            {
                CustomTransportPanel.Visibility = Visibility.Visible;

                _hideTimer.Stop();
                _hideTimer.Start();

                if (!_sliderUpdateTimer.IsEnabled) _sliderUpdateTimer.Start();
            }
        }

        private void InternalRootGrid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            // マウスがグリッド外に出たら即座に非表示
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
            var player = InternalMediaPlayer.MediaPlayer;
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
            // ドラッグ中のみ、再生位置を同期させる（タイマー更新との競合を防ぐ）
            var player = InternalMediaPlayer.MediaPlayer;
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

        public void ResetPlayback()
        {
            try
            {
                if (InternalMediaPlayer.MediaPlayer != null)
                {
                    // 再生を確実に停止し、リソースを解放する
                    InternalMediaPlayer.MediaPlayer.Pause();
                    InternalMediaPlayer.Source = null;
                    InternalMediaPlayer.MediaPlayer.Source = null;

                    // FrameServerイベント解除
                    InternalMediaPlayer.MediaPlayer.VideoFrameAvailable -= OnVideoFrameAvailable;
                }
            }
            catch (System.Exception)
            {
            }

            lock (_frameLock)
            {
                _frameBitmap?.Dispose();
                _frameBitmap = null;
                _skFrameBitmap?.Dispose();
                _skFrameBitmap = null;
            }

            IsVideoContent = false;
            FFmpegSource = null; // Dispose the old one
            PageImage.Opacity = 1.0;
            VideoVisualHost.Visibility = Visibility.Collapsed;
            InternalMediaPlayer.Visibility = Visibility.Collapsed;
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