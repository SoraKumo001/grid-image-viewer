using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using Microsoft.Windows.ApplicationModel.Resources;

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

        public SlideshowManager(MainWindow mainWindow, SettingsManager settings)
        {
            _mainWindow = mainWindow;
            _settings = settings;

            _slideshowTimer = new DispatcherTimer();
            _slideshowTimer.Tick += SlideshowTimer_Tick;
            
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
            _mainWindow.SlideshowUniformToFill.IsChecked = _settings.SlideshowUniformToFill;
            _mainWindow.SlideshowInterval.Value = _settings.SlideshowInterval;

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
            _mainWindow.SlideshowCurrentFolderOnly.IsEnabled = enabled;
            _mainWindow.SlideshowUniformToFill.IsEnabled = enabled;
            _mainWindow.SlideshowInterval.IsEnabled = enabled;
        }

        public void SlideshowDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            _settings.SlideshowFullscreen = _mainWindow.SlideshowFullscreen.IsChecked ?? false;
            _settings.SlideshowRandom = _mainWindow.SlideshowRandom.IsChecked ?? false;
            _settings.SlideshowLoop = _mainWindow.SlideshowLoop.IsChecked ?? false;
            _settings.SlideshowNextFolder = _mainWindow.SlideshowNextFolder.IsChecked ?? false;
            _settings.SlideshowUniformToFill = _mainWindow.SlideshowUniformToFill.IsChecked ?? false;
            _settings.SlideshowInterval = _mainWindow.SlideshowInterval.Value;
            _settings.SaveSlideshowSettings();

            StartSlideshow();
        }

        public void StartSlideshow()
        {
            IsSlideshowRunning = true;
            if (_settings.SlideshowFullscreen && !_mainWindow.AppWindow.Presenter.Kind.Equals(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen))
            {
                _mainWindow.AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
                _mainWindow.AppTitleBar.Visibility = Visibility.Collapsed;
            }

            _ = _mainWindow.UpdateDisplayAsync();
            _slideshowTimer.Interval = TimeSpan.FromSeconds(_settings.SlideshowInterval);
            _slideshowTimer.Start();
            _mainWindow.ShowNotification(_resourceLoader.GetString("Notification_SlideshowStarted"));
        }

        public void StopSlideshow()
        {
            IsSlideshowRunning = false;
            _slideshowTimer.Stop();
            for (int i = 0; i < 4; i++) SlideshowRandomIndices[i] = -1;
            _ = _mainWindow.UpdateDisplayAsync();
            _mainWindow.ShowNotification(_resourceLoader.GetString("Notification_SlideshowStopped"));
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
                        _mainWindow.NavigateFolder(1);
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
    }
}
