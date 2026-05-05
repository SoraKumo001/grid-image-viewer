using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace grid_image_viewer
{
    public partial class MainViewModel : ObservableObject
    {
        public IViewerStateService State { get; }

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

        public ObservableCollection<string> Playlist
        {
            get => State.Playlist;
            set => State.Playlist = value;
        }

        public int CurrentIndex
        {
            get => State.CurrentIndex;
            set => State.CurrentIndex = value;
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

        public string CurrentDirectory
        {
            get => State.CurrentDirectory;
            set => State.CurrentDirectory = value;
        }

        public bool IsSlideshowRunning
        {
            get => State.IsSlideshowRunning;
            set => State.IsSlideshowRunning = value;
        }

        [ObservableProperty]
        public partial bool SlideshowFullscreen { get; set; }

        [ObservableProperty]
        public partial bool SlideshowRandom { get; set; }

        [ObservableProperty]
        public partial bool SlideshowLoop { get; set; }

        [ObservableProperty]
        public partial bool SlideshowNextFolder { get; set; }

        [ObservableProperty]
        public partial bool SlideshowIncludeSiblings { get; set; }

        [ObservableProperty]
        public partial bool SlideshowCurrentFolderOnly { get; set; }

        [ObservableProperty]
        public partial bool SlideshowUniformToFill { get; set; }

        [ObservableProperty]
        public partial bool SlideshowCrossfade { get; set; }

        [ObservableProperty]
        public partial double SlideshowInterval { get; set; }

        [ObservableProperty]
        public partial double SlideshowCrossfadeDuration { get; set; }

        [ObservableProperty]
        public partial bool IsSearchingFolder { get; set; }

        public bool IsGridMode
        {
            get => State.IsGridMode;
            set => State.IsGridMode = value;
        }

        public bool IsViewerMode => !IsGridMode;

        [ObservableProperty]
        public partial bool IsDialogOpen { get; set; }

        [ObservableProperty]
        public partial bool IsPageIndicatorVisible { get; set; }

        [ObservableProperty]
        public partial string PageIndicatorText { get; set; } = string.Empty;

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

        [ObservableProperty]
        public partial bool ShowPageIndicator { get; set; } = true;

        [ObservableProperty]
        public partial int BoundaryAction { get; set; } = 1;

        public MainViewModel(IViewerStateService stateService)
        {
            State = stateService;

            // Subscribe to state changes to update UI
            if (State is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    OnPropertyChanged(e.PropertyName);
                    if (e.PropertyName == nameof(State.Playlist) ||
                        e.PropertyName == nameof(State.CurrentIndex) ||
                        e.PropertyName == nameof(State.IsGridMode))
                    {
                        if (e.PropertyName == nameof(State.CurrentIndex)) _overrideDisplayIndex = null;
                        UpdatePageIndicator();
                        if (e.PropertyName == nameof(State.IsGridMode)) OnPropertyChanged(nameof(IsViewerMode));
                    }
                };
            }

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

            // Default values
            SlideshowInterval = 5.0;
            SlideshowCrossfadeDuration = 0.5;
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
                PageIndicatorText = string.Empty;
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
