using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using quick_image_viewer.Common;
using quick_image_viewer.Interfaces;
using System;
using System.Linq;
namespace quick_image_viewer.Services
{
    internal class AnimationService : IAnimationService
    {
        private readonly IMainView _window;
        private readonly ISettingsManager _settings;
        private readonly DispatcherTimer _animationTimer;
        private Grid? _currentCrossfadeGrid;
        private Grid? _prevCrossfadeGrid;
        private Storyboard? _gridStoryboard;

        public AnimationService(IMainView window, ISettingsManager settings)
        {
            _window = window;
            _settings = settings;

            _animationTimer = new DispatcherTimer();
            _animationTimer.Interval = TimeSpan.FromMilliseconds(Constants.LAYOUT_DEBOUNCE_MS);
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

            _currentCrossfadeGrid = currentGrid;
            _prevCrossfadeGrid = prevGrid;

            float duration = (float)_settings.SlideshowCrossfadeDuration;
            if (duration <= 0) duration = 0.5f;

            // Ensure the NEW grid is rendered on top of the OLD one
            Canvas.SetZIndex(currentGrid, 10);
            Canvas.SetZIndex(prevGrid, 0);

            // Initial states for XAML animation
            currentGrid.Opacity = 0;
            currentGrid.Visibility = Visibility.Visible;
            prevGrid.Opacity = 1;
            prevGrid.Visibility = Visibility.Visible;

            _gridStoryboard = new Storyboard();
            var ease = new ExponentialEase { Exponent = 6, EasingMode = EasingMode.EaseInOut };

            // Fade in NEW
            var animIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(duration),
                EasingFunction = ease
            };
            Storyboard.SetTarget(animIn, currentGrid);
            Storyboard.SetTargetProperty(animIn, "Opacity");

            // Fade out OLD
            var animOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(duration),
                EasingFunction = ease
            };
            Storyboard.SetTarget(animOut, prevGrid);
            Storyboard.SetTargetProperty(animOut, "Opacity");

            _gridStoryboard.Children.Add(animIn);
            _gridStoryboard.Children.Add(animOut);

            _gridStoryboard.Completed += (s, e) =>
            {
                _window.DispatcherQueue.TryEnqueue(() =>
                {
                    prevGrid.Opacity = 0;
                    prevGrid.Visibility = Visibility.Collapsed;
                    currentGrid.Opacity = 1;

                    if (_currentCrossfadeGrid == currentGrid)
                    {
                        _currentCrossfadeGrid = null;
                        _prevCrossfadeGrid = null;
                    }
                    _gridStoryboard = null;
                    try { _window.MetadataDisplayService.UpdateMetadataPanel(); } catch { }
                });
            };

            _gridStoryboard.Begin();
        }

        public void StopAnimation()
        {
            _animationTimer.Stop();
        }

        public void StopCrossfade()
        {
            if (_gridStoryboard != null)
            {
                _gridStoryboard.Stop();
                _gridStoryboard = null;
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
