using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using quick_image_viewer.ViewModels;
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
                bool hadFocus = false;
                try
                {
                    var focused = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(this.XamlRoot);
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
        }

        public Windows.Media.Playback.MediaPlayer GetOrCreateMediaPlayer()
        {
            var mp = InternalMediaPlayer.MediaPlayer;
            if (mp == null)
            {
                mp = new Windows.Media.Playback.MediaPlayer();
                mp.IsLoopingEnabled = true;
                mp.IsMuted = true;
                mp.AutoPlay = true;

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

            InternalMediaPlayer.Visibility = Visibility.Visible;
            VideoVisualHost.Visibility = Visibility.Collapsed;
            return mp;
        }

        public void SetupPlayer(Windows.Media.Playback.MediaPlayer player)
        {
            if (InternalMediaPlayer.MediaPlayer != player)
            {
                InternalMediaPlayer.SetMediaPlayer(player);
            }
            InternalMediaPlayer.Visibility = Visibility.Visible;
            VideoVisualHost.Visibility = Visibility.Collapsed;
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
                    InternalMediaPlayer.MediaPlayer.PlaybackSession.Position = System.TimeSpan.FromSeconds(TimelineSlider.Value);
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

        private string FormatTime(System.TimeSpan time) => $"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}";

        private void UpdatePlayPauseIcon(MediaPlaybackState state)
        {
            PlayPauseIcon.Glyph = state == MediaPlaybackState.Playing ? "\uE769" : "\uE768";
        }

        public void ShowControls()
        {
            var isVisible = InternalMediaPlayer.Visibility == Visibility.Visible || VideoVisualHost.Visibility == Visibility.Visible;
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
                var focused = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(this.XamlRoot);
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
                    player.PlaybackSession.Position = System.TimeSpan.FromSeconds(e.NewValue);
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
                    // Source = null は重い場合があるが、リソース解放のために必要
                    InternalMediaPlayer.MediaPlayer.Pause();
                    InternalMediaPlayer.Source = null;
                }
            }
            catch (System.Exception)
            {
            }

            PageImage.Opacity = 1.0;
            VideoVisualHost.Visibility = Visibility.Collapsed;
            InternalMediaPlayer.Visibility = Visibility.Collapsed;

            bool hadFocus = false;
            try
            {
                var focused = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(this.XamlRoot);
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

        private bool IsChildOf(DependencyObject child, DependencyObject parent)
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