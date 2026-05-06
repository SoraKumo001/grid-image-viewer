using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
namespace quick_image_viewer.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public IViewerStateService State { get; }
        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;
        private readonly IMenuStateManager _menuStateManager;

        // Commands
        public ICommand NavigateNextCommand { get; }
        public ICommand NavigatePrevCommand { get; }
        public ICommand NavigateNextFolderCommand { get; }
        public ICommand NavigatePrevFolderCommand { get; }
        public ICommand TogglePageIndicatorCommand { get; }
        public ICommand OpenSlideshowCommand { get; }
        public ICommand ToggleBookmarkCommand { get; }
        public ICommand ToggleBookmarkPanelCommand { get; }
        public ICommand SettingsCommand { get; }
        public ICommand KeyBindingsCommand { get; }
        public ICommand SupportCommand { get; }

        public EditorViewModel Editor { get; } = new EditorViewModel();
        public ViewerViewModel Viewer { get; } = new ViewerViewModel();

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
        public bool HasImages => Playlist != null && Playlist.Count > 0;

        public string CurrentImagePath => (Playlist != null && CurrentIndex >= 0 && CurrentIndex < Playlist.Count) ? Playlist[CurrentIndex] : string.Empty;

        [ObservableProperty]
        public partial bool IsDialogOpen { get; set; }



        [ObservableProperty] public partial bool IsBookmarkPanelVisible { get; set; }
        [ObservableProperty] public partial string BookmarkMenuText { get; set; } = "Bookmark this folder";

        private ObservableCollection<quick_image_viewer.Managers.BookmarkItem> _bookmarks = new();
        public ObservableCollection<quick_image_viewer.Managers.BookmarkItem> Bookmarks
        {
            get => _bookmarks;
            set => SetProperty(ref _bookmarks, value);
        }

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

        public MainViewModel(IViewerStateService stateService, IMenuStateManager menuStateManager)
        {
            State = stateService;
            _menuStateManager = menuStateManager;
            _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            // Subscribe to state changes to update UI
            if (State is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    OnPropertyChanged(e.PropertyName);
                    if (e.PropertyName == nameof(State.Playlist) ||
                        e.PropertyName == nameof(State.CurrentIndex) ||
                        e.PropertyName == nameof(State.IsGridMode) ||
                        e.PropertyName == nameof(State.CurrentDirectory))
                    {
                        if (e.PropertyName == nameof(State.CurrentIndex)) _overrideDisplayIndex = null;
                        UpdatePageIndicator();
                        if (e.PropertyName == nameof(State.IsGridMode)) OnPropertyChanged(nameof(IsViewerMode));
                        if (e.PropertyName == nameof(State.CurrentIndex) || e.PropertyName == nameof(State.Playlist))
                        {
                            OnPropertyChanged(nameof(CurrentImagePath));
                            OnPropertyChanged(nameof(HasImages));
                        }
                        if (e.PropertyName == nameof(State.CurrentDirectory)) UpdateBookmarkMenuText();
                    }
                };
            }

            // Initialize Bookmarks
            var initialSettings = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ISettingsManager>(((App)Application.Current).Services);
            Bookmarks = new ObservableCollection<quick_image_viewer.Managers.BookmarkItem>(initialSettings.Bookmarks);
            UpdateBookmarkMenuText();

            NavigateNextCommand = new RelayCommand(() => Navigate(1));
            NavigatePrevCommand = new RelayCommand(() => Navigate(-1));
            NavigateNextFolderCommand = new RelayCommand(() => NavigateFolder(1));
            NavigatePrevFolderCommand = new RelayCommand(() => NavigateFolder(-1));
            TogglePageIndicatorCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<TogglePageIndicatorMessage>());
            OpenSlideshowCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<OpenSlideshowMessage>());
            ToggleBookmarkCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<ToggleBookmarkMessage>());
            ToggleBookmarkPanelCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<ToggleBookmarkPanelMessage>());

            SettingsCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send(new ShellActionMessage("Settings")));
            KeyBindingsCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send(new ShellActionMessage("KeyBindings")));
            SupportCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send(new ShellActionMessage("Support")));

            WeakReferenceMessenger.Default.Register<BookmarksChangedMessage>(this, (r, m) =>
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    var settings = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ISettingsManager>(((App)Application.Current).Services);
                    Bookmarks = new ObservableCollection<quick_image_viewer.Managers.BookmarkItem>(settings.Bookmarks);
                    UpdateBookmarkMenuText();
                });
            });

            WeakReferenceMessenger.Default.Register<ToggleBookmarkPanelMessage>(this, (r, m) =>
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    IsBookmarkPanelVisible = !IsBookmarkPanelVisible;
                });
            });

            WeakReferenceMessenger.Default.Register<UpdateMenuStatesMessage>(this, (r, m) =>
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    Editor.CanUndo = m.CanUndo;
                    Editor.CanRedo = m.CanRedo;
                    Editor.HasValidPath = m.HasValidPath;
                    Editor.IsImageEditable = m.IsImageEditable;

                    var settings = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ISettingsManager>(((App)Application.Current).Services);
                    int splitCount = settings.MangaSplitCount;
                    Viewer.IsViewSingle = (splitCount == 1);
                    Viewer.IsViewDouble = (splitCount == 2);
                    Viewer.IsViewQuad = (splitCount == 4);

                    int layoutMode = settings.QuadLayoutMode;
                    Viewer.IsLayoutAuto = (layoutMode == 0);
                    Viewer.IsLayoutHorz = (layoutMode == 1);
                    Viewer.IsLayoutGrid = (layoutMode == 2);

                    int stretchMode = settings.ImageStretchMode;
                    Viewer.IsStretchOriginal = (stretchMode == 0);
                    Viewer.IsStretchContain = (stretchMode == 2);
                    Viewer.IsStretchCover = (stretchMode == 3);

                    ShowPageIndicator = settings.ShowPageIndicator;
                    UpdateBookmarkMenuText();
                });
            });

            // Default values
            SlideshowInterval = 5.0;
            SlideshowCrossfadeDuration = 0.5;
        }

        private void UpdateBookmarkMenuText()
        {
            var settings = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ISettingsManager>(((App)Application.Current).Services);
            string dir = CurrentDirectory;
            if (!string.IsNullOrEmpty(dir))
            {
                bool isBookmarked = settings.Bookmarks.Exists(b => b.Path == dir);
                BookmarkMenuText = isBookmarked ? _menuStateManager.GetString("MenuBookmark_Remove") : _menuStateManager.GetString("MenuBookmark_Add");
            }
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
    public record CopyPathMessage(string Path);
    public record DeleteFileMessage(string Path);
    public record EditActionMessage(string Action, string Path, object? Value = null);
    public record ShellActionMessage(string Action, string Path = "");
    public record ViewActionMessage(string Action, int Value);
    public record EditFilterArgs(string Path, string Filter);
    public record BookmarksChangedMessage();
    public record UpdateMenuStatesMessage(string Path, bool CanUndo, bool CanRedo, bool HasValidPath, bool IsImageEditable);
    public record ClearImageSourceMessage(string Path);
    public record RefreshDisplayMessage();
    public record FocusRequestMessage();
    public record LoadDirectoryMessage(string Path, string InitialFile = "", bool IncludeSiblings = false, bool IncludeSubfolders = false, System.Collections.Generic.List<string>? PreloadedPlaylist = null);
}