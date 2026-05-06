using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Services;
using quick_image_viewer.ViewModels;
using System;
using System.Linq;
namespace quick_image_viewer.Managers
{
    public class SlideshowManager : ISlideshowManager, IRecipient<OpenSlideshowMessage>
    {
        private IMainView _mainWindow;
        private ISettingsManager _settings;
        private ISlideshowService _slideshowService;
        private IViewerStateService _state;

        public bool IsSlideshowRunning { get => _state.IsSlideshowRunning; }
        public int[] SlideshowRandomIndices { get => _slideshowService.CurrentRandomIndices; }

        private ResourceLoader _resourceLoader = new ResourceLoader();
        private bool _wasExpanded = false;
        private MainViewModel ViewModel => _mainWindow.ViewModel;

        public SlideshowManager(IMainView mainWindow, ISettingsManager settings, ISlideshowService slideshowService, IViewerStateService state)
        {
            _mainWindow = mainWindow;
            _settings = settings;
            _slideshowService = slideshowService;
            _state = state;

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
                string currentPath = _state.Playlist.ElementAtOrDefault(_state.CurrentIndex) ?? "";
                WeakReferenceMessenger.Default.Send(new LoadDirectoryMessage(_state.CurrentDirectory, currentPath, _settings.SlideshowIncludeSiblings, _settings.SlideshowCurrentFolderOnly));
            }

            _slideshowService.Start();

            // Immediately show the first image or jump if random
            if (_settings.SlideshowRandom && _state.Playlist.Count > 1)
            {
                // Trigger first next immediately through message
                WeakReferenceMessenger.Default.Send(new SlideshowNextRequestedMessage());
            }
            else if (_state.Playlist.Count > 0)
            {
                _ = _mainWindow.UpdateDisplayAsync();
            }

            // If we are searching and have no files yet, wait for initial population then trigger start
            if (isExpanding && _state.Playlist.Count == 0)
            {
                _ = InitialStartAsync();
            }
        }

        private async System.Threading.Tasks.Task InitialStartAsync()
        {
            int timeout = 0;
            while (_state.Playlist.Count == 0 && timeout < 100)
            {
                if (!IsSlideshowRunning) return;
                await System.Threading.Tasks.Task.Delay(100);
                timeout++;
            }

            if (IsSlideshowRunning && _state.Playlist.Count > 0)
            {
                _ = _mainWindow.UpdateDisplayAsync();
            }
        }

        public void StopSlideshow()
        {
            if (!IsSlideshowRunning) return;

            _slideshowService.Stop();
            _mainWindow.ViewerManager.ShowNotification(_resourceLoader.GetString("Notification_SlideshowStopped"));

            if (_settings.SlideshowFullscreen)
            {
                _mainWindow.IsFullscreen = false;
            }

            if (_wasExpanded)
            {
                _wasExpanded = false;
                string currentPath = _state.Playlist.ElementAtOrDefault(_state.CurrentIndex) ?? "";
                WeakReferenceMessenger.Default.Send(new LoadDirectoryMessage(_state.CurrentDirectory, currentPath, false, false));
            }
            else
            {
                _ = _mainWindow.UpdateDisplayAsync();
            }

            SetSlideshowControlsEnabled(true);
        }

        public void Dispose()
        {
            // SlideshowService takes care of its timer
        }

    }
}
