using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Linq;

namespace grid_image_viewer
{
    internal class AnimationService : IAnimationService
    {
        private readonly IMainView _window;
        private readonly ISettingsManager _settings;
        private readonly DispatcherTimer _animationTimer;
        private Storyboard? _currentCrossfadeStoryboard;
        private Grid? _currentCrossfadeGrid;
        private Grid? _prevCrossfadeGrid;

        public AnimationService(IMainView window, ISettingsManager settings)
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
            StopCrossfade();
            var sb = new Storyboard();
            _currentCrossfadeStoryboard = sb;
            _currentCrossfadeGrid = currentGrid;
            _prevCrossfadeGrid = prevGrid;

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
                currentGrid.Opacity = 1;
                currentGrid.Translation = new System.Numerics.Vector3(0, 0, 0); // Reset translation if needed

                if (_currentCrossfadeStoryboard == sb)
                {
                    _currentCrossfadeStoryboard = null;
                    _currentCrossfadeGrid = null;
                    _prevCrossfadeGrid = null;
                }
            };

            // Ensure smooth transition: 
            // 1. Current grid (new image) should be on top
            // 2. Previous grid should remain fully visible until transition starts
            Canvas.SetZIndex(currentGrid, 10);
            Canvas.SetZIndex(prevGrid, 0);

            currentGrid.Opacity = 0;
            currentGrid.Visibility = Visibility.Visible;
            prevGrid.Opacity = 1;
            prevGrid.Visibility = Visibility.Visible;

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

        public void StopCrossfade()
        {
            if (_currentCrossfadeStoryboard != null)
            {
                try
                {
                    _currentCrossfadeStoryboard.Stop();
                }
                catch { }
                _currentCrossfadeStoryboard = null;
            }

            if (_currentCrossfadeGrid != null)
            {
                _currentCrossfadeGrid.Opacity = 1;
                _currentCrossfadeGrid.Visibility = Visibility.Visible;
                _currentCrossfadeGrid = null;
            }

            if (_prevCrossfadeGrid != null)
            {
                _prevCrossfadeGrid.Opacity = 0;
                _prevCrossfadeGrid.Visibility = Visibility.Collapsed;
                _prevCrossfadeGrid = null;
            }
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
