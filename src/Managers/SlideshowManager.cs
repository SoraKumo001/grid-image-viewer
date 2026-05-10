using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Services;
using quick_image_viewer.ViewModels;
using System;
using System.Linq;
namespace quick_image_viewer.Managers
{
    public class SlideshowManager : ISlideshowManager, IRecipient<OpenSlideshowMessage>, IRecipient<LoadDirectoryMessage>
    {
        private IMainView _mainWindow;
        private ISettingsManager _settings;
        private ISlideshowService _slideshowService;
        private IViewerStateService _state;
        private int _originalMangaSplitCount = 1;
        private int _originalStretchMode = 2;

        public bool IsSlideshowRunning { get => _state.IsSlideshowRunning; }
        public int[] SlideshowRandomIndices { get => _slideshowService.CurrentRandomIndices; }

        private bool _wasExpanded = false;
        private bool _isInternalNavigation = false;
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
            WeakReferenceMessenger.Default.Register<LoadDirectoryMessage>(this);
        }

        public void Receive(LoadDirectoryMessage message)
        {
            // 外部からのフォルダ読み込み（ブックマークなど）が発生した場合は自動再生を停止する
            // ただし、自分自身（SlideshowManager）が発行した拡張読み込みの場合は無視する
            if (!_isInternalNavigation && IsSlideshowRunning)
            {
                StopSlideshow();
            }
        }

        public void Receive(OpenSlideshowMessage message) => OpenSlideshowDialogAsync();

        public async void OpenSlideshowDialogAsync()
        {
            if (ViewModel.IsDialogOpen) return;

            if (ViewModel.IsSlideshowRunning)
            {
                StopSlideshow();
                return;
            }

            _mainWindow.ViewerManager.PauseAllVideo();

            // Load settings into ViewModel
            ViewModel.SlideshowMangaSplitCount = _settings.SlideshowMangaSplitCount;
            ViewModel.SlideshowFullscreen = _settings.SlideshowFullscreen;
            ViewModel.SlideshowRandom = _settings.SlideshowRandom;
            ViewModel.SlideshowLoop = _settings.SlideshowLoop;
            ViewModel.SlideshowNextFolder = _settings.SlideshowNextFolder;
            ViewModel.SlideshowIncludeSiblings = _settings.SlideshowIncludeSiblings;
            ViewModel.SlideshowCurrentFolderOnly = _settings.SlideshowCurrentFolderOnly;

            // Handle legacy UniformToFill setting by mapping it to Cover mode if enabled
            if (_settings.SlideshowUniformToFill)
            {
                _settings.SlideshowStretchMode = 3; // Cover
                _settings.SlideshowUniformToFill = false;
            }

            ViewModel.SlideshowStretchMode = _settings.SlideshowStretchMode;
            ViewModel.SlideshowStretchModeIndex = MapStretchModeToIndex(_settings.SlideshowStretchMode);
            ViewModel.SlideshowInterval = _settings.SlideshowInterval;
            ViewModel.SlideshowCrossfade = _settings.SlideshowCrossfade;
            ViewModel.SlideshowCrossfadeDuration = _settings.SlideshowCrossfadeDuration;

            SetSlideshowControlsEnabled(false);

            if (_mainWindow.Content?.XamlRoot != null)
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
            _mainWindow.SlideshowMangaSplitCount.IsEnabled = enabled;
            _mainWindow.SlideshowFullscreen.IsEnabled = enabled;
            _mainWindow.SlideshowRandom.IsEnabled = enabled;
            _mainWindow.SlideshowLoop.IsEnabled = enabled;
            _mainWindow.SlideshowNextFolder.IsEnabled = enabled;
            _mainWindow.SlideshowIncludeSiblings.IsEnabled = enabled;
            _mainWindow.SlideshowCurrentFolderOnly.IsEnabled = enabled;
            _mainWindow.SlideshowStretchMode.IsEnabled = enabled;
            _mainWindow.SlideshowInterval.IsEnabled = enabled;
            _mainWindow.SlideshowCrossfade.IsEnabled = enabled;
            _mainWindow.SlideshowCrossfadeDuration.IsEnabled = enabled;
        }

        private int MapStretchModeToIndex(int mode)
        {
            return mode switch
            {
                2 => 1, // Contain
                3 => 2, // Cover
                0 => 3, // Original
                _ => 0  // Keep (-1)
            };
        }

        private int MapIndexToStretchMode(int index)
        {
            return index switch
            {
                1 => 2, // Contain
                2 => 3, // Cover
                3 => 0, // Original
                _ => -1 // Keep
            };
        }

        public void SlideshowDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Sync ViewModel to Settings
            _settings.SlideshowMangaSplitCount = ViewModel.SlideshowMangaSplitCount;
            _settings.SlideshowFullscreen = ViewModel.SlideshowFullscreen;
            _settings.SlideshowRandom = ViewModel.SlideshowRandom;
            _settings.SlideshowLoop = ViewModel.SlideshowLoop;
            _settings.SlideshowNextFolder = ViewModel.SlideshowNextFolder;
            _settings.SlideshowIncludeSiblings = ViewModel.SlideshowIncludeSiblings;
            _settings.SlideshowCurrentFolderOnly = ViewModel.SlideshowCurrentFolderOnly;
            ViewModel.SlideshowStretchMode = MapIndexToStretchMode(ViewModel.SlideshowStretchModeIndex);
            _settings.SlideshowStretchMode = ViewModel.SlideshowStretchMode;
            _settings.SlideshowInterval = ViewModel.SlideshowInterval;
            _settings.SlideshowCrossfade = ViewModel.SlideshowCrossfade;
            _settings.SlideshowCrossfadeDuration = ViewModel.SlideshowCrossfadeDuration;
            _settings.SaveSlideshowSettings();

            StartSlideshow();
        }

        public void StartSlideshow()
        {
            if (IsSlideshowRunning) return;

            _mainWindow.ViewerManager.PauseAllVideo();

            SetSlideshowControlsEnabled(false);

            _originalMangaSplitCount = _settings.MangaSplitCount;
            if (_settings.SlideshowMangaSplitCount > 0)
            {
                int split = _settings.SlideshowMangaSplitCount;
                if (split == 3) split = 4;
                _settings.MangaSplitCount = split;
                ViewModel.MangaSplitCount = split;
            }

            _originalStretchMode = _settings.ImageStretchMode;
            if (_settings.SlideshowStretchMode >= 0)
            {
                _settings.ImageStretchMode = _settings.SlideshowStretchMode;
            }

            if (_settings.SlideshowFullscreen)
            {
                _mainWindow.IsFullscreen = true;
            }

            _mainWindow.ViewerManager.ShowNotification(_settings.GetString("Notification_SlideshowStarted"));

            bool isExpanding = _settings.SlideshowIncludeSiblings || _settings.SlideshowCurrentFolderOnly;
            if (isExpanding)
            {
                _wasExpanded = true;
                _isInternalNavigation = true;
                try
                {
                    string currentPath = _state.Playlist.ElementAtOrDefault(_state.CurrentIndex) ?? "";
                    WeakReferenceMessenger.Default.Send(new LoadDirectoryMessage(_state.CurrentDirectory, currentPath, _settings.SlideshowIncludeSiblings, _settings.SlideshowCurrentFolderOnly));
                }
                finally
                {
                    _isInternalNavigation = false;
                }
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
            _mainWindow.ViewerManager.ShowNotification(_settings.GetString("Notification_SlideshowStopped"));

            if (_settings.SlideshowFullscreen)
            {
                _mainWindow.IsFullscreen = false;
            }

            if (_settings.SlideshowMangaSplitCount > 0)
            {
                _settings.MangaSplitCount = _originalMangaSplitCount;
                ViewModel.MangaSplitCount = _originalMangaSplitCount;
            }

            if (_settings.SlideshowStretchMode >= 0)
            {
                _settings.ImageStretchMode = _originalStretchMode;
            }

            if (_wasExpanded)
            {
                _wasExpanded = false;
                _isInternalNavigation = true;
                try
                {
                    string currentPath = _state.Playlist.ElementAtOrDefault(_state.CurrentIndex) ?? "";
                    WeakReferenceMessenger.Default.Send(new LoadDirectoryMessage(_state.CurrentDirectory, currentPath, false, false));
                }
                finally
                {
                    _isInternalNavigation = false;
                }
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
