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
            // This method is now legacy for individual items. 
            // We'll focus on StartGridCrossfade for the entire grid.
        }

        public void StartGridCrossfade(Grid currentGrid, Grid prevGrid)
        {
            var sb = new Storyboard();

            var animIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(_settings.SlideshowCrossfadeDuration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animIn, currentGrid);
            Storyboard.SetTargetProperty(animIn, "Opacity");
            sb.Children.Add(animIn);

            var animOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(_settings.SlideshowCrossfadeDuration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animOut, prevGrid);
            Storyboard.SetTargetProperty(animOut, "Opacity");
            sb.Children.Add(animOut);

            sb.Completed += (s, e) =>
            {
                prevGrid.Opacity = 0;
                prevGrid.Visibility = Visibility.Collapsed;
            };

            currentGrid.Visibility = Visibility.Visible;
            currentGrid.Opacity = 0;

            try
            {
                sb.Begin();
            }
            catch (Exception)
            {
            }

            // Update metadata after transition
            try
            {
                _window.MetadataDisplayService.UpdateMetadataPanel();
            }
            catch (Exception)
            {
            }
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
                    // Invalidate the specific page through ViewerManager
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
