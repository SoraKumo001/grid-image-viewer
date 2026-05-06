using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using quick_image_viewer.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
namespace quick_image_viewer.ViewModels
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
        public ICommand TogglePageIndicatorCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand ZoomResetCommand { get; }
        public ICommand RotateRightCommand { get; }
        public ICommand RotateLeftCommand { get; }
        public ICommand Rotate180Command { get; }
        public ICommand ToggleMetadataCommand { get; }
        public ICommand OpenSlideshowCommand { get; }
        public ICommand ToggleBookmarkCommand { get; }
        public ICommand ToggleBookmarkPanelCommand { get; }
        public ICommand DeleteFileCommand { get; }
        public ICommand ToggleMangaModeCommand { get; }
        public ICommand ToggleStretchModeCommand { get; }
        public ICommand ViewModeCommand { get; }
        public ICommand LayoutModeCommand { get; }
        public ICommand StretchModeCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand OverwriteCommand { get; }
        public ICommand SaveAsCommand { get; }
        public ICommand PrintCommand { get; }
        public ICommand CropCommand { get; }
        public ICommand ResizeCommand { get; }
        public ICommand ToneCommand { get; }
        public ICommand FilterCommand { get; }
        public ICommand FlipHorzCommand { get; }
        public ICommand FlipVertCommand { get; }
        public ICommand OpenExplorerCommand { get; }
        public ICommand SettingsCommand { get; }
        public ICommand KeyBindingsCommand { get; }
        public ICommand SupportCommand { get; }

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

        public string CurrentImagePath => (Playlist != null && CurrentIndex >= 0 && CurrentIndex < Playlist.Count) ? Playlist[CurrentIndex] : string.Empty;

        [ObservableProperty]
        public partial bool IsDialogOpen { get; set; }

        [ObservableProperty] public partial bool CanUndo { get; set; }
        [ObservableProperty] public partial bool CanRedo { get; set; }
        [ObservableProperty] public partial bool HasValidPath { get; set; }
        [ObservableProperty] public partial bool IsImageEditable { get; set; }

        [ObservableProperty] public partial bool IsViewSingle { get; set; }
        [ObservableProperty] public partial bool IsViewDouble { get; set; }
        [ObservableProperty] public partial bool IsViewQuad { get; set; }

        [ObservableProperty] public partial bool IsLayoutAuto { get; set; }
        [ObservableProperty] public partial bool IsLayoutHorz { get; set; }
        [ObservableProperty] public partial bool IsLayoutGrid { get; set; }

        [ObservableProperty] public partial bool IsStretchOriginal { get; set; }
        [ObservableProperty] public partial bool IsStretchContain { get; set; }
        [ObservableProperty] public partial bool IsStretchCover { get; set; }

        [ObservableProperty] public partial bool IsMetadataVisible { get; set; }
        [ObservableProperty] public partial bool IsBookmarkPanelVisible { get; set; }
        [ObservableProperty] public partial string BookmarkMenuText { get; set; } = "Bookmark this folder";
        [ObservableProperty] public partial string ContextPath { get; set; } = string.Empty;

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
                        if (e.PropertyName == nameof(State.CurrentIndex) || e.PropertyName == nameof(State.Playlist)) OnPropertyChanged(nameof(CurrentImagePath));
                    }
                };
            }

            NavigateNextCommand = new RelayCommand(() => Navigate(1));
            NavigatePrevCommand = new RelayCommand(() => Navigate(-1));
            NavigateNextFolderCommand = new RelayCommand(() => NavigateFolder(1));
            NavigatePrevFolderCommand = new RelayCommand(() => NavigateFolder(-1));
            ToggleGridModeCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<ToggleGridMessage>());
            ToggleFullscreenCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<FullscreenMessage>());
            TogglePageIndicatorCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<TogglePageIndicatorMessage>());
            ZoomInCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send(new ZoomMessage(1.2f)));
            ZoomOutCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send(new ZoomMessage(1.0f / 1.2f)));
            ZoomResetCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send(new ZoomMessage(0)));
            RotateRightCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Rotate", path ?? string.Empty, 90)));
            RotateLeftCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Rotate", path ?? string.Empty, -90)));
            Rotate180Command = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Rotate", path ?? string.Empty, 180)));
            ToggleMetadataCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<ToggleMetadataMessage>());
            OpenSlideshowCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<OpenSlideshowMessage>());
            ToggleBookmarkCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<ToggleBookmarkMessage>());
            ToggleBookmarkPanelCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<ToggleBookmarkPanelMessage>());
            DeleteFileCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new DeleteFileMessage(path ?? string.Empty)));
            ToggleMangaModeCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<ToggleMangaMessage>());
            ToggleStretchModeCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<ToggleStretchMessage>());
            ViewModeCommand = new RelayCommand<string>(val => WeakReferenceMessenger.Default.Send(new ViewActionMessage("ViewMode", int.Parse(val ?? "1"))));
            LayoutModeCommand = new RelayCommand<string>(val => WeakReferenceMessenger.Default.Send(new ViewActionMessage("LayoutMode", int.Parse(val ?? "0"))));
            StretchModeCommand = new RelayCommand<string>(val => WeakReferenceMessenger.Default.Send(new ViewActionMessage("StretchMode", int.Parse(val ?? "2"))));

            UndoCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Undo", path ?? string.Empty)));
            RedoCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Redo", path ?? string.Empty)));
            OverwriteCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Overwrite", path ?? string.Empty)));
            SaveAsCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("SaveAs", path ?? string.Empty)));
            PrintCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Print", path ?? string.Empty)));
            CropCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Crop", path ?? string.Empty)));
            ResizeCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Resize", path ?? string.Empty)));
            ToneCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Tone", path ?? string.Empty)));
            FilterCommand = new RelayCommand<string>(filter => WeakReferenceMessenger.Default.Send(new EditActionMessage("Filter", ContextPath ?? CurrentImagePath, filter)));
            FlipHorzCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Flip", path ?? string.Empty, "Horz")));
            FlipVertCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Flip", path ?? string.Empty, "Vert")));

            OpenExplorerCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new ShellActionMessage("OpenExplorer", path ?? string.Empty)));
            SettingsCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send(new ShellActionMessage("Settings")));
            KeyBindingsCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send(new ShellActionMessage("KeyBindings")));
            SupportCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send(new ShellActionMessage("Support")));

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
    public record ToggleGridMessage();
    public record ToggleMangaMessage();
    public record ToggleStretchMessage();
    public record ToggleBookmarkMessage();
    public record ToggleBookmarkPanelMessage();
    public record TogglePageIndicatorMessage();
    public record DeleteFileMessage(string Path);
    public record EditActionMessage(string Action, string Path, object? Value = null);
    public record ShellActionMessage(string Action, string Path = "");
    public record ViewActionMessage(string Action, int Value);
    public record EditFilterArgs(string Path, string Filter);
}
