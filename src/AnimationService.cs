using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Linq;

namespace grid_image_viewer
{
    internal class AnimationService
    {
        private readonly MainWindow _window;
        private readonly SettingsManager _settings;
        private readonly DispatcherTimer _animationTimer;

        public AnimationService(MainWindow window, SettingsManager settings)
        {
            _window = window;
            _settings = settings;

            _animationTimer = new DispatcherTimer();
            _animationTimer.Interval = TimeSpan.FromMilliseconds(30);
            _animationTimer.Tick += AnimationTimer_Tick;
        }

        public void StartCrossfade(int pageIndex, Grid[] currentContainers, Grid[] prevContainers)
        {
            var sb = new Storyboard();

            var animIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(_settings.SlideshowCrossfadeDuration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animIn, currentContainers[pageIndex]);
            Storyboard.SetTargetProperty(animIn, "Opacity");
            sb.Children.Add(animIn);

            var animOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(_settings.SlideshowCrossfadeDuration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animOut, prevContainers[pageIndex]);
            Storyboard.SetTargetProperty(animOut, "Opacity");
            sb.Children.Add(animOut);

            sb.Completed += (s, e) =>
            {
                prevContainers[pageIndex].Opacity = 0;
            };

            sb.Begin();

            // 画像が切り替わったのでメタデータも更新（パネルが開いている場合のみ）
            if (pageIndex == 0) _window.MetadataDisplayService.UpdateMetadataPanel();
        }

        public void StopAnimation()
        {
            _animationTimer.Stop();
        }

        public void StartAnimation()
        {
            var viewerManager = _window.ViewerManager;
            if (viewerManager == null) return;

            if (viewerManager.Pages.Any(p => p.IsAnimated))
            {
                int minInterval = 1000;
                for (int i = 0; i < 4; i++)
                {
                    if (viewerManager.Pages[i].IsAnimated && viewerManager.Pages[i].CurrentFrameDuration < minInterval)
                        minInterval = viewerManager.Pages[i].CurrentFrameDuration;
                }
                _animationTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(10, minInterval));
                _animationTimer.Start();
            }
        }

        private void AnimationTimer_Tick(object? sender, object e)
        {
            _animationTimer.Stop();

            var viewerManager = _window.ViewerManager;
            if (viewerManager == null) return;

            int minInterval = 1000;
            bool anyAnimated = false;

            for (int i = 0; i < 4; i++)
            {
                if (viewerManager.Pages[i].IsAnimated)
                {
                    int interval = viewerManager.Pages[i].AdvanceFrame();
                    if (interval < minInterval) minInterval = interval;
                    anyAnimated = true;
                    // _pageCanvases[i].Invalidate(); は MainWindow 経由で取得するか、ViewerManager にメソッドを作る
                    viewerManager.InvalidatePage(i);
                }
            }

            if (anyAnimated)
            {
                _animationTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(10, minInterval));
                _animationTimer.Start();
            }
        }
    }
}
