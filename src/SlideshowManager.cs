using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Linq;

namespace grid_image_viewer
{
    public class SlideshowManager : IDisposable, IRecipient<OpenSlideshowMessage>
    {
        private MainWindow _mainWindow;
        private SettingsManager _settings;
        private DispatcherTimer _slideshowTimer;
        public bool IsSlideshowRunning { get => ViewModel.IsSlideshowRunning; private set => ViewModel.IsSlideshowRunning = value; }
        public int[] SlideshowRandomIndices { get; private set; } = new int[4] { -1, -1, -1, -1 };
        private Random _random = new Random();
        private ResourceLoader _resourceLoader = new ResourceLoader();
        private bool _wasExpanded = false;
        private MainViewModel ViewModel => _mainWindow.ViewModel;

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

            WeakReferenceMessenger.Default.Register<OpenSlideshowMessage>(this);
        }

        public void Receive(OpenSlideshowMessage message) => OpenSlideshowDialogAsync();

        public async void OpenSlideshowDialogAsync()
        {
            if (ViewModel.IsSlideshowRunning)
            {
                StopSlideshow();
                return;
            }

            // Load settings into ViewModel
            ViewModel.SlideshowFullscreen = _settings.SlideshowFullscreen;
            ViewModel.SlideshowRandom = _settings.SlideshowRandom;
            ViewModel.SlideshowLoop = _settings.SlideshowLoop;
            ViewModel.SlideshowNextFolder = _settings.SlideshowNextFolder;
            ViewModel.SlideshowIncludeSiblings = _settings.SlideshowIncludeSiblings;
            ViewModel.SlideshowCurrentFolderOnly = _settings.SlideshowCurrentFolderOnly;
            ViewModel.SlideshowUniformToFill = _settings.SlideshowUniformToFill;
            ViewModel.SlideshowInterval = _settings.SlideshowInterval;
            ViewModel.SlideshowCrossfade = _settings.SlideshowCrossfade;
            ViewModel.SlideshowCrossfadeDuration = _settings.SlideshowCrossfadeDuration;

            SetSlideshowControlsEnabled(false);

            _mainWindow.SlideshowDialog.XamlRoot = _mainWindow.Content.XamlRoot;

            _mainWindow.SlideshowDialog.Opened -= SlideshowDialog_Opened;
            _mainWindow.SlideshowDialog.Opened += SlideshowDialog_Opened;

            ViewModel.IsDialogOpen = true;
            await _mainWindow.SlideshowDialog.ShowAsync();
            ViewModel.IsDialogOpen = false;
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
            // Sync ViewModel to Settings
            _settings.SlideshowFullscreen = ViewModel.SlideshowFullscreen;
            _settings.SlideshowRandom = ViewModel.SlideshowRandom;
            _settings.SlideshowLoop = ViewModel.SlideshowLoop;
            _settings.SlideshowNextFolder = ViewModel.SlideshowNextFolder;
            _settings.SlideshowIncludeSiblings = ViewModel.SlideshowIncludeSiblings;
            _settings.SlideshowCurrentFolderOnly = ViewModel.SlideshowCurrentFolderOnly;
            _settings.SlideshowUniformToFill = ViewModel.SlideshowUniformToFill;
            _settings.SlideshowInterval = ViewModel.SlideshowInterval;
            _settings.SlideshowCrossfade = ViewModel.SlideshowCrossfade;
            _settings.SlideshowCrossfadeDuration = ViewModel.SlideshowCrossfadeDuration;
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

            _mainWindow.ViewerManager.ShowNotification(_resourceLoader.GetString("Notification_SlideshowStarted"));

            bool isExpanding = _settings.SlideshowIncludeSiblings || _settings.SlideshowCurrentFolderOnly;
            if (isExpanding)
            {
                _wasExpanded = true;
                string currentPath = _mainWindow.Playlist.ElementAtOrDefault(_mainWindow.CurrentIndex) ?? "";
                _mainWindow.LoadDirectory(_mainWindow.CurrentDirectory, currentPath, _settings.SlideshowIncludeSiblings, _settings.SlideshowCurrentFolderOnly);
            }

            _slideshowTimer.Interval = TimeSpan.FromSeconds(_settings.SlideshowInterval);
            _slideshowTimer.Start();
            IsSlideshowRunning = true;

            // Immediately show the first image or jump if random
            if (_settings.SlideshowRandom && _mainWindow.Playlist.Count > 1)
            {
                SlideshowTimer_Tick(null, EventArgs.Empty);
            }
            else if (_mainWindow.Playlist.Count > 0)
            {
                _ = _mainWindow.UpdateDisplayAsync();
            }

            // If we are searching and have no files yet, wait for initial population then trigger start
            if (isExpanding && _mainWindow.Playlist.Count == 0)
            {
                _ = InitialStartAsync();
            }
        }

        private async System.Threading.Tasks.Task InitialStartAsync()
        {
            int timeout = 0;
            while (_mainWindow.Playlist.Count == 0 && timeout < 100)
            {
                if (!IsSlideshowRunning) return;
                await System.Threading.Tasks.Task.Delay(100);
                timeout++;
            }

            if (IsSlideshowRunning && _mainWindow.Playlist.Count > 0)
            {
                if (_settings.SlideshowRandom && _mainWindow.Playlist.Count > 1)
                {
                    SlideshowTimer_Tick(null, EventArgs.Empty);
                }
                else
                {
                    _ = _mainWindow.UpdateDisplayAsync();
                }
            }
        }

        public void StopSlideshow()
        {
            if (!IsSlideshowRunning) return;

            _slideshowTimer.Stop();
            IsSlideshowRunning = false;
            for (int i = 0; i < 4; i++) SlideshowRandomIndices[i] = -1;
            _mainWindow.ViewerManager.ShowNotification(_resourceLoader.GetString("Notification_SlideshowStopped"));

            if (_settings.SlideshowFullscreen)
            {
                _mainWindow.IsFullscreen = false;
            }

            if (_wasExpanded)
            {
                _wasExpanded = false;
                string currentPath = _mainWindow.Playlist.ElementAtOrDefault(_mainWindow.CurrentIndex) ?? "";
                _mainWindow.LoadDirectory(_mainWindow.CurrentDirectory, currentPath, false, false);
            }
            else
            {
                _ = _mainWindow.UpdateDisplayAsync();
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
            // Wait until we have at least one image in the playlist
            int timeout = 0;
            while (_mainWindow.Playlist.Count == 0 && timeout < 100)
            {
                if (!IsSlideshowRunning) return;
                await System.Threading.Tasks.Task.Delay(100);
                timeout++;
            }

            if (IsSlideshowRunning)
            {
                if (_mainWindow.Playlist.Count > 0)
                {
                    _slideshowTimer.Start();
                }
                else
                {
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
        public void Dispose()
        {
            _slideshowTimer?.Stop();
        }
    }
}
