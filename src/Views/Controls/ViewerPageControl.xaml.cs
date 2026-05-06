using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Media.Playback;
namespace quick_image_viewer.Views.Controls
{
    public sealed partial class ViewerPageControl : UserControl
    {
        public Grid RootGrid => InternalRootGrid;
        public Image PageImage => InternalPageImage;
        public SkiaSharp.Views.Windows.SKXamlCanvas PageCanvas => InternalPageCanvas;
        public MediaPlayerElement PagePlayer => InternalMediaPlayer;

        public ProgressRing LoadingRing => InternalLoadingRing;
        public Border FocusBorder => InternalFocusBorder;

        private Microsoft.UI.Xaml.DispatcherTimer _hideTimer;
        private Microsoft.UI.Xaml.DispatcherTimer _sliderUpdateTimer;
        private bool _isDraggingSlider = false;

        public ViewerPageControl()
        {
            this.InitializeComponent();

            _hideTimer = new Microsoft.UI.Xaml.DispatcherTimer { Interval = System.TimeSpan.FromSeconds(3) };
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                CustomTransportPanel.Visibility = Visibility.Collapsed;
            };

            _sliderUpdateTimer = new Microsoft.UI.Xaml.DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(100) };
            _sliderUpdateTimer.Tick += (s, e) => UpdateSlider();

            // スライダーの操作開始と終了を確実に検知する
            TimelineSlider.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(TimelineSlider_PointerPressed), true);
            TimelineSlider.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(TimelineSlider_PointerReleased), true);

            // 分割モード対応: 自分のエリア内でのマウス移動だけを監視する
            InternalRootGrid.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(InternalRootGrid_PointerMoved), true);
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
                if (InternalMediaPlayer.MediaPlayer != null)
                {
                    InternalMediaPlayer.MediaPlayer.PlaybackSession.Position = System.TimeSpan.FromSeconds(TimelineSlider.Value);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ViewerPageControl] Seek Error (Released): {ex.Message}");
            }
        }

        private void UpdateSlider()
        {
            if (_isDraggingSlider || InternalMediaPlayer.MediaPlayer == null) return;

            try
            {
                var session = InternalMediaPlayer.MediaPlayer.PlaybackSession;
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
            catch (System.Exception ex)
            {
                // ここは頻繁に呼ばれるので、特定のCOMExceptionなどは無視しても良いが調査用に出力
                System.Diagnostics.Debug.WriteLine($"[ViewerPageControl] UpdateSlider Exception: {ex.Message}");
            }
        }

        private string FormatTime(System.TimeSpan time) => $"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}";

        private void UpdatePlayPauseIcon(MediaPlaybackState state)
        {
            PlayPauseIcon.Glyph = state == MediaPlaybackState.Playing ? "\uE769" : "\uE768";
        }

        public void ShowControls()
        {
            if (InternalMediaPlayer.Visibility == Microsoft.UI.Xaml.Visibility.Visible)
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
            CustomTransportPanel.Visibility = Visibility.Collapsed;
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (InternalMediaPlayer.MediaPlayer == null) return;

            var session = InternalMediaPlayer.MediaPlayer.PlaybackSession;
            if (session.PlaybackState == MediaPlaybackState.Playing)
                InternalMediaPlayer.MediaPlayer.Pause();
            else
                InternalMediaPlayer.MediaPlayer.Play();

            UpdatePlayPauseIcon(session.PlaybackState);
            ShowControls();
        }

        private void TimelineSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            // ドラッグ中のみ、再生位置を同期させる（タイマー更新との競合を防ぐ）
            if (_isDraggingSlider && InternalMediaPlayer.MediaPlayer != null)
            {
                try
                {
                    InternalMediaPlayer.MediaPlayer.PlaybackSession.Position = System.TimeSpan.FromSeconds(e.NewValue);
                    ShowControls();
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ViewerPageControl] Seek Error (ValueChanged): {ex.Message}");
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
                    InternalMediaPlayer.MediaPlayer.Pause();
                    InternalMediaPlayer.Source = null;
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ViewerPageControl] ResetPlayback Error: {ex.Message}");
            }
            InternalMediaPlayer.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            CustomTransportPanel.Visibility = Visibility.Collapsed;
            _sliderUpdateTimer.Stop();
            _hideTimer.Stop();
        }
    }
}