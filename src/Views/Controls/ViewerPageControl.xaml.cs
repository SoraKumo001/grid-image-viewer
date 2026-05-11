using FFmpegInteropX;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.Media.Playback;

namespace quick_image_viewer.Views.Controls
{
    public sealed partial class ViewerPageControl : UserControl
    {
        public event System.EventHandler<Windows.Foundation.Size>? VideoSizeChanged;
        public Grid RootGrid => InternalRootGrid;
        public Image PageImage => InternalPageImage;
        public SkiaSharp.Views.Windows.SKSwapChainPanel PageCanvas => InternalPageCanvas;
        public ProgressRing LoadingRing => InternalLoadingRing;
        public Border FocusBorder => InternalFocusBorder;

        public VideoPlayerControl VideoPlayer => InternalVideoPlayer;

        public MediaPlayerElement PagePlayer => VideoPlayer.PagePlayer;
        public FFmpegMediaSource? FFmpegSource { get => VideoPlayer.FFmpegSource; set => VideoPlayer.FFmpegSource = value; }

        public bool IsVideoContent
        {
            get => VideoPlayer.IsVideoContent;
            set
            {
                VideoPlayer.IsVideoContent = value;
                InternalVideoPlayer.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public bool IsMediaReady { get => VideoPlayer.IsMediaReady; set => VideoPlayer.IsMediaReady = value; }

        private readonly System.Threading.SemaphoreSlim _loadingSemaphore = new(1, 1);
        private static readonly System.Threading.SemaphoreSlim _globalVideoInitSemaphore = new(1, 1);
        private Microsoft.UI.Xaml.Media.Imaging.SoftwareBitmapSource? _currentSoftwareSource;
        private System.Threading.CancellationTokenSource? _activeLoadCts;

        public static System.Threading.SemaphoreSlim GetGlobalInitSemaphore() => _globalVideoInitSemaphore;

        public ViewerPageControl()
        {
            this.InitializeComponent();
            InternalVideoPlayer.InvalidateCanvasRequested += () => DispatcherQueue.TryEnqueue(() => InternalPageCanvas.Invalidate());

            // VideoPlayerControl 側からのサイズ変更通知を ViewerPageControl のイベントとして転送する。
            // 以前は InvokeVideoSizeChanged(size) を呼んでいたが、それが VideoPlayer 側を再度呼び出し
            // 無限ループ (StackOverflow) になっていたため、イベントの直接発火のみを行う。
            InternalVideoPlayer.VideoSizeChanged += (s, size) => VideoSizeChanged?.Invoke(this, size);
        }

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

        public MediaPlayer GetOrCreateMediaPlayer() => VideoPlayer.GetOrCreateMediaPlayer();
        public MediaPlayer CreateNewMediaPlayer() => VideoPlayer.CreateNewMediaPlayer();
        public void SetMediaHandlers(Windows.Foundation.TypedEventHandler<MediaPlayer, object> opened, Windows.Foundation.TypedEventHandler<MediaPlayer, MediaPlayerFailedEventArgs> failed) => VideoPlayer.SetMediaHandlers(opened, failed);

        public void UpdateVolume() => VideoPlayer.UpdateVolume();
        public void PauseVideo() => VideoPlayer.PauseVideo();
        public void ResumeVideo() => VideoPlayer.ResumeVideo();

        internal void InvokeVideoSizeChanged(Windows.Foundation.Size size)
        {
            VideoPlayer.InvokeVideoSizeChanged(size, InternalPageImage.HorizontalAlignment, InternalPageImage.VerticalAlignment);
            VideoSizeChanged?.Invoke(this, size);
        }

        internal void UpdateVideoVisualSize(double overrideW = -1, double overrideH = -1)
        {
            VideoPlayer.UpdateVideoVisualSize(InternalPageImage.Stretch, InternalPageImage.HorizontalAlignment, InternalPageImage.VerticalAlignment, overrideW, overrideH);
        }

        public void PaintVideoFrame(SkiaSharp.SKCanvas canvas, SkiaSharp.SKImageInfo info, int hAlign, int vAlign)
        {
            VideoPlayer.PaintVideoFrame(canvas, info, InternalPageImage.Stretch, hAlign, vAlign);
        }

        public async Task ResetPlaybackAsync()
        {
            _activeLoadCts?.Cancel();
            _activeLoadCts?.Dispose();
            _activeLoadCts = null;

            await _loadingSemaphore.WaitAsync();
            try
            {
                IsVideoContent = false;
                await VideoPlayer.ResetPlaybackAsync();

                if (_currentSoftwareSource != null)
                {
                    _currentSoftwareSource.Dispose();
                    _currentSoftwareSource = null;
                }
                PageImage.Source = null;
                PageImage.Opacity = 1.0;

                InternalPageCanvas.Visibility = Visibility.Collapsed;

                System.GC.Collect(1, System.GCCollectionMode.Optimized, false);

                await Task.Delay(20);
            }
            catch (System.Exception) { }
            finally
            {
                _loadingSemaphore.Release();
            }
        }

        public void ResetPlayback()
        {
            _ = ResetPlaybackAsync();
        }

        public void SetContentAlignment(HorizontalAlignment h, VerticalAlignment v)
        {
            InternalPageImage.HorizontalAlignment = h;
            InternalPageImage.VerticalAlignment = v;
            VideoPlayer.SetContentAlignment(h, v);
        }
    }
}

