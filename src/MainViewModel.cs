using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace grid_image_viewer
{
    public partial class MainViewModel : ObservableObject
    {
        private ObservableCollection<string> _playlist;
        public ObservableCollection<string> Playlist
        {
            get => _playlist;
            set => SetProperty(ref _playlist, value);
        }

        private int _currentIndex;
        public int CurrentIndex
        {
            get => _currentIndex;
            set => SetProperty(ref _currentIndex, value);
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
            set => SetProperty(ref _isGridMode, value);
        }

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
        }

        public void UpdatePageIndicator()
        {
            if (Playlist.Count == 0)
            {
                PageIndicatorText = "0 / 0";
            }
            else
            {
                PageIndicatorText = $"{CurrentIndex + 1} / {Playlist.Count}";
            }
        }
    }
}
