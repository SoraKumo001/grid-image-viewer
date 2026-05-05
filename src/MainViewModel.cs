using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace grid_image_viewer
{
    public partial class MainViewModel : ObservableObject
    {
        // Commands
        public ICommand NavigateNextCommand { get; }
        public ICommand NavigatePrevCommand { get; }
        public ICommand NavigateNextFolderCommand { get; }
        public ICommand NavigatePrevFolderCommand { get; }
        public ICommand ToggleGridModeCommand { get; }
        public ICommand ToggleFullscreenCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand ZoomResetCommand { get; }
        public ICommand RotateRightCommand { get; }
        public ICommand RotateLeftCommand { get; }
        public ICommand ToggleMetadataCommand { get; }
        public ICommand OpenSlideshowCommand { get; }

        private ObservableCollection<string> _playlist;
        public ObservableCollection<string> Playlist
        {
            get => _playlist;
            set
            {
                if (SetProperty(ref _playlist, value))
                {
                    UpdatePageIndicator();
                }
            }
        }

        private int _currentIndex;
        public int CurrentIndex
        {
            get => _currentIndex;
            set
            {
                if (SetProperty(ref _currentIndex, value))
                {
                    _overrideDisplayIndex = null;
                    UpdatePageIndicator();
                }
            }
        }

        private int? _overrideDisplayIndex;
        public int? OverrideDisplayIndex
        {
            get => _overrideDisplayIndex;
            set
            {
                if (SetProperty(ref _overrideDisplayIndex, value))
                {
                    UpdatePageIndicator();
                }
            }
        }

        private string _currentDirectory;
        public string CurrentDirectory
        {
            get => _currentDirectory;
            set => SetProperty(ref _currentDirectory, value);
        }

        private bool _isSlideshowRunning;
        public bool IsSlideshowRunning
        {
            get => _isSlideshowRunning;
            set => SetProperty(ref _isSlideshowRunning, value);
        }

        private bool _slideshowFullscreen;
        public bool SlideshowFullscreen
        {
            get => _slideshowFullscreen;
            set => SetProperty(ref _slideshowFullscreen, value);
        }

        private bool _slideshowRandom;
        public bool SlideshowRandom
        {
            get => _slideshowRandom;
            set => SetProperty(ref _slideshowRandom, value);
        }

        private bool _slideshowLoop;
        public bool SlideshowLoop
        {
            get => _slideshowLoop;
            set => SetProperty(ref _slideshowLoop, value);
        }

        private bool _slideshowNextFolder;
        public bool SlideshowNextFolder
        {
            get => _slideshowNextFolder;
            set => SetProperty(ref _slideshowNextFolder, value);
        }

        private bool _slideshowIncludeSiblings;
        public bool SlideshowIncludeSiblings
        {
            get => _slideshowIncludeSiblings;
            set => SetProperty(ref _slideshowIncludeSiblings, value);
        }

        private bool _slideshowCurrentFolderOnly;
        public bool SlideshowCurrentFolderOnly
        {
            get => _slideshowCurrentFolderOnly;
            set => SetProperty(ref _slideshowCurrentFolderOnly, value);
        }

        private bool _slideshowUniformToFill;
        public bool SlideshowUniformToFill
        {
            get => _slideshowUniformToFill;
            set => SetProperty(ref _slideshowUniformToFill, value);
        }

        private bool _slideshowCrossfade;
        public bool SlideshowCrossfade
        {
            get => _slideshowCrossfade;
            set => SetProperty(ref _slideshowCrossfade, value);
        }

        private double _slideshowInterval;
        public double SlideshowInterval
        {
            get => _slideshowInterval;
            set => SetProperty(ref _slideshowInterval, value);
        }

        private double _slideshowCrossfadeDuration;
        public double SlideshowCrossfadeDuration
        {
            get => _slideshowCrossfadeDuration;
            set => SetProperty(ref _slideshowCrossfadeDuration, value);
        }

        private bool _isSearchingFolder;
        public bool IsSearchingFolder
        {
            get => _isSearchingFolder;
            set => SetProperty(ref _isSearchingFolder, value);
        }

        private bool _isGridMode;
        public bool IsGridMode
        {
            get => _isGridMode;
            set
            {
                if (SetProperty(ref _isGridMode, value))
                {
                    OnPropertyChanged(nameof(IsViewerMode));
                    _overrideDisplayIndex = null;
                    UpdatePageIndicator();
                }
            }
        }

        public bool IsViewerMode => !IsGridMode;

        private bool _isDialogOpen;
        public bool IsDialogOpen
        {
            get => _isDialogOpen;
            set => SetProperty(ref _isDialogOpen, value);
        }

        private bool _isPageIndicatorVisible;
        public bool IsPageIndicatorVisible
        {
            get => _isPageIndicatorVisible;
            set => SetProperty(ref _isPageIndicatorVisible, value);
        }

        private string _pageIndicatorText;
        public string PageIndicatorText
        {
            get => _pageIndicatorText;
            set => SetProperty(ref _pageIndicatorText, value);
        }

        private int _mangaSplitCount = 1;
        public int MangaSplitCount
        {
            get => _mangaSplitCount;
            set
            {
                if (SetProperty(ref _mangaSplitCount, value))
                {
                    UpdatePageIndicator();
                }
            }
        }

        private bool _showPageIndicator = true;
        public bool ShowPageIndicator
        {
            get => _showPageIndicator;
            set
            {
                if (SetProperty(ref _showPageIndicator, value))
                {
                    UpdatePageIndicator();
                }
            }
        }

        private int _boundaryAction = 1;
        public int BoundaryAction
        {
            get => _boundaryAction;
            set => SetProperty(ref _boundaryAction, value);
        }

        public MainViewModel()
        {
            _playlist = new ObservableCollection<string>();
            _currentIndex = -1;
            _currentDirectory = string.Empty;
            _isSlideshowRunning = false;
            _slideshowFullscreen = false;
            _slideshowRandom = false;
            _slideshowLoop = false;
            _slideshowNextFolder = false;
            _slideshowIncludeSiblings = false;
            _slideshowCurrentFolderOnly = false;
            _slideshowUniformToFill = false;
            _slideshowCrossfade = false;
            _slideshowInterval = 5.0;
            _slideshowCrossfadeDuration = 0.5;
            _isSearchingFolder = false;
            _isGridMode = false;
            _isDialogOpen = false;
            _isPageIndicatorVisible = false;
            _pageIndicatorText = string.Empty;

            NavigateNextCommand = new RelayCommand(() => Navigate(1));
            NavigatePrevCommand = new RelayCommand(() => Navigate(-1));
            NavigateNextFolderCommand = new RelayCommand(() => NavigateFolder(1));
            NavigatePrevFolderCommand = new RelayCommand(() => NavigateFolder(-1));
            ToggleGridModeCommand = new RelayCommand(() => IsGridMode = !IsGridMode);
            ToggleFullscreenCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<FullscreenMessage>());
            ZoomInCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send(new ZoomMessage(1.2f)));
            ZoomOutCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send(new ZoomMessage(1.0f / 1.2f)));
            ZoomResetCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send(new ZoomMessage(0)));
            RotateRightCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send(new EditMessage("Rotate", 90)));
            RotateLeftCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send(new EditMessage("Rotate", -90)));
            ToggleMetadataCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<ToggleMetadataMessage>());
            OpenSlideshowCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<OpenSlideshowMessage>());
        }

        private void Navigate(int offset)
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(offset));
        }

        private void NavigateFolder(int offset)
        {
            WeakReferenceMessenger.Default.Send(new FolderNavigationMessage(offset));
        }

        public void UpdatePageIndicator()
        {
            if (ShowPageIndicator && Playlist != null && Playlist.Count > 0 && CurrentIndex >= 0)
            {
                int displayIndex = OverrideDisplayIndex ?? (IsGridMode ? (CurrentIndex + 1) : System.Math.Min(CurrentIndex + MangaSplitCount, Playlist.Count));
                PageIndicatorText = $"{displayIndex} / {Playlist.Count}";
                IsPageIndicatorVisible = true;
            }
            else
            {
                IsPageIndicatorVisible = false;
            }
        }
    }

    public record PlaylistUpdatedMessage(bool ForceFullGridUpdate = false);
    public record NavigationMessage(int Offset, bool ForceSingleStep = false);
    public record FolderNavigationMessage(int Offset);
    public record FullscreenMessage();
    public record ZoomMessage(float Factor); // Factor 0 means reset
    public record EditMessage(string Type, object Value);
    public record ToggleMetadataMessage();
    public record OpenSlideshowMessage();
}
