using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Linq;

namespace grid_image_viewer
{
    public class SlideshowManager
    {
        private MainWindow _mainWindow;
        private SettingsManager _settings;
        private DispatcherTimer _slideshowTimer;
        public bool IsSlideshowRunning { get; private set; } = false;
        public int[] SlideshowRandomIndices { get; private set; } = new int[4] { -1, -1, -1, -1 };
        private Random _random = new Random();
        private ResourceLoader _resourceLoader = new ResourceLoader();
        private bool _wasExpanded = false;

        public SlideshowManager(MainWindow mainWindow, SettingsManager settings)
        {
            _mainWindow = mainWindow;
            _settings = settings;

            _slideshowTimer = new DispatcherTimer();
            _slideshowTimer.Tick += SlideshowTimer_Tick;

            var formatter = new Windows.Globalization.NumberFormatting.DecimalFormatter();
            formatter.IntegerDigits = 1;
            formatter.FractionDigits = 2;

            var rounder = new Windows.Globalization.NumberFormatting.IncrementNumberRounder();
            rounder.Increment = 0.01;
            rounder.RoundingAlgorithm = Windows.Globalization.NumberFormatting.RoundingAlgorithm.RoundHalfUp;
            formatter.NumberRounder = rounder;

            _mainWindow.SlideshowCrossfadeDuration.NumberFormatter = formatter;
            _mainWindow.SlideshowInterval.NumberFormatter = formatter;
        }

        public async void OpenSlideshowDialogAsync()
        {
            if (IsSlideshowRunning)
            {
                StopSlideshow();
                return;
            }

            _mainWindow.SlideshowFullscreen.IsChecked = _settings.SlideshowFullscreen;
            _mainWindow.SlideshowRandom.IsChecked = _settings.SlideshowRandom;
            _mainWindow.SlideshowLoop.IsChecked = _settings.SlideshowLoop;
            _mainWindow.SlideshowNextFolder.IsChecked = _settings.SlideshowNextFolder;
            _mainWindow.SlideshowIncludeSiblings.IsChecked = _settings.SlideshowIncludeSiblings;
            _mainWindow.SlideshowUniformToFill.IsChecked = _settings.SlideshowUniformToFill;
            _mainWindow.SlideshowInterval.Value = _settings.SlideshowInterval;
            _mainWindow.SlideshowCrossfade.IsChecked = _settings.SlideshowCrossfade;
            _mainWindow.SlideshowCrossfadeDuration.Value = _settings.SlideshowCrossfadeDuration;

            SetSlideshowControlsEnabled(false);

            _mainWindow.SlideshowDialog.XamlRoot = _mainWindow.Content.XamlRoot;

            _mainWindow.SlideshowDialog.Opened -= SlideshowDialog_Opened;
            _mainWindow.SlideshowDialog.Opened += SlideshowDialog_Opened;

            _mainWindow.IsDialogOpen = true;
            await _mainWindow.SlideshowDialog.ShowAsync();
            _mainWindow.IsDialogOpen = false;
        }

        public void SlideshowDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            var restoreTimer = new DispatcherTimer();
            restoreTimer.Interval = TimeSpan.FromMilliseconds(100);
            restoreTimer.Tick += (s, e) =>
            {
                restoreTimer.Stop();
                SetSlideshowControlsEnabled(true);
            };
            restoreTimer.Start();
        }

        private void SetSlideshowControlsEnabled(bool enabled)
        {
            _mainWindow.SlideshowFullscreen.IsEnabled = enabled;
            _mainWindow.SlideshowRandom.IsEnabled = enabled;
            _mainWindow.SlideshowLoop.IsEnabled = enabled;
            _mainWindow.SlideshowNextFolder.IsEnabled = enabled;
            _mainWindow.SlideshowIncludeSiblings.IsEnabled = enabled;
            _mainWindow.SlideshowCurrentFolderOnly.IsEnabled = enabled;
            _mainWindow.SlideshowUniformToFill.IsEnabled = enabled;
            _mainWindow.SlideshowInterval.IsEnabled = enabled;
            _mainWindow.SlideshowCrossfade.IsEnabled = enabled;
            _mainWindow.SlideshowCrossfadeDuration.IsEnabled = enabled;
        }

        public void SlideshowDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            _settings.SlideshowFullscreen = _mainWindow.SlideshowFullscreen.IsChecked ?? false;
            _settings.SlideshowRandom = _mainWindow.SlideshowRandom.IsChecked ?? false;
            _settings.SlideshowLoop = _mainWindow.SlideshowLoop.IsChecked ?? false;
            _settings.SlideshowNextFolder = _mainWindow.SlideshowNextFolder.IsChecked ?? false;
            _settings.SlideshowIncludeSiblings = _mainWindow.SlideshowIncludeSiblings.IsChecked ?? false;
            _settings.SlideshowUniformToFill = _mainWindow.SlideshowUniformToFill.IsChecked ?? false;
            _settings.SlideshowInterval = _mainWindow.SlideshowInterval.Value;
            _settings.SlideshowCrossfade = _mainWindow.SlideshowCrossfade.IsChecked ?? false;
            _settings.SlideshowCrossfadeDuration = _mainWindow.SlideshowCrossfadeDuration.Value;
            _settings.SaveSlideshowSettings();

            StartSlideshow();
        }

        public void StartSlideshow()
        {
            if (IsSlideshowRunning) return;

            SetSlideshowControlsEnabled(false);

            if (_settings.SlideshowFullscreen)
            {
                _mainWindow.IsFullscreen = true;
            }

            if (_settings.SlideshowIncludeSiblings)
            {
                _wasExpanded = true;
                string currentPath = _mainWindow.Playlist.ElementAtOrDefault(_mainWindow.CurrentIndex) ?? "";
                _mainWindow.LoadDirectory(_mainWindow.CurrentDirectory, currentPath, true);
            }

            _slideshowTimer.Interval = TimeSpan.FromSeconds(_settings.SlideshowInterval);
            _slideshowTimer.Start();
            IsSlideshowRunning = true;
        }

        public void StopSlideshow()
        {
            if (!IsSlideshowRunning) return;

            _slideshowTimer.Stop();
            IsSlideshowRunning = false;

            if (_settings.SlideshowFullscreen)
            {
                _mainWindow.IsFullscreen = false;
            }

            if (_wasExpanded)
            {
                _wasExpanded = false;
                string currentPath = _mainWindow.Playlist.ElementAtOrDefault(_mainWindow.CurrentIndex) ?? "";
                _mainWindow.LoadDirectory(_mainWindow.CurrentDirectory, currentPath, false);
            }

            SetSlideshowControlsEnabled(true);
        }

        private void SlideshowTimer_Tick(object? sender, object e)
        {
            var playlist = _mainWindow.Playlist;
            int currentIndex = _mainWindow.CurrentIndex;

            if (playlist == null || playlist.Count == 0) return;

            if (_settings.SlideshowRandom)
            {
                int nextIdx;
                int splits = _settings.MangaSplitCount;
                if (playlist.Count <= 1)
                {
                    nextIdx = 0;
                    for (int i = 0; i < 4; i++) SlideshowRandomIndices[i] = -1;
                }
                else
                {
                    do { nextIdx = _random.Next(playlist.Count); } while (nextIdx == currentIndex);
                    SlideshowRandomIndices[0] = nextIdx;

                    if (splits > 1 && playlist.Count >= splits)
                    {
                        for (int i = 1; i < splits; i++)
                        {
                            int r;
                            do
                            {
                                r = _random.Next(playlist.Count);
                            } while (r == nextIdx || SlideshowRandomIndices.Take(i).Contains(r) || r == currentIndex);
                            SlideshowRandomIndices[i] = r;
                        }
                    }
                    else
                    {
                        for (int i = 1; i < 4; i++) SlideshowRandomIndices[i] = -1;
                    }
                }
                _mainWindow.CurrentIndex = nextIdx;
                _ = _mainWindow.UpdateDisplayAsync();
            }
            else
            {
                if (_mainWindow.IsSearchingFolder) return;

                int increment = _settings.MangaSplitCount;
                if (currentIndex + increment >= playlist.Count)
                {
                    if (_settings.SlideshowNextFolder)
                    {
                        // Stop timer during search to prevent multiple triggers
                        _slideshowTimer.Stop();
                        _mainWindow.NavigateFolder(1);

                        // NavigateFolder will call LoadDirectory which updates display.
                        // We need to restart the timer once the new folder is loaded.
                        // But NavigateFolder is async void, so we'll poll or use a Task.
                        _ = RestartTimerAfterFolderLoad();
                    }
                    else if (_settings.SlideshowLoop)
                    {
                        _mainWindow.CurrentIndex = 0;
                        _ = _mainWindow.UpdateDisplayAsync();
                    }
                    else
                    {
                        StopSlideshow();
                    }
                }
                else
                {
                    _mainWindow.Navigate(1);
                }
            }
        }

        private async System.Threading.Tasks.Task RestartTimerAfterFolderLoad()
        {
            // Wait for searching to start
            int timeout = 0;
            while (!_mainWindow.IsSearchingFolder && timeout < 20) { await System.Threading.Tasks.Task.Delay(50); timeout++; }

            // Wait for searching to finish
            while (_mainWindow.IsSearchingFolder) { await System.Threading.Tasks.Task.Delay(100); }

            if (IsSlideshowRunning)
            {
                // If we successfully loaded a new playlist, start the timer again
                if (_mainWindow.Playlist.Count > 0)
                {
                    _slideshowTimer.Start();
                }
                else
                {
                    // If no images found in the next folder, try searching again or stop
                    if (_settings.SlideshowLoop)
                    {
                        _mainWindow.NavigateFolder(1);
                        _ = RestartTimerAfterFolderLoad();
                    }
                    else
                    {
                        StopSlideshow();
                    }
                }
            }
        }
    }
}
