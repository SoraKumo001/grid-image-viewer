using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Models;
using quick_image_viewer.Services;
using quick_image_viewer.ViewModels;
using quick_image_viewer.Views.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
namespace quick_image_viewer
{
    public sealed partial class MainWindow : Window, IGridHost, IAppWindowHost, INotificationHost, IAnimationHost, IFileOperationHost, IInputHost, ISlideshowHost, IPrintHost, IImageEditHost, IMetadataHost, IViewerLoaderHost, IViewerHost, IOverlayHost,
        IRecipient<FullscreenMessage>,
        IRecipient<PlaylistUpdatedMessage>,
        IRecipient<SlideshowNextRequestedMessage>,
        IRecipient<ToggleGridMessage>,
        IRecipient<ToggleMangaMessage>,
        IRecipient<ToggleReadingDirectionMessage>,
        IRecipient<ToggleStretchMessage>,
        IRecipient<CopyPathMessage>,
        IRecipient<DeleteFileMessage>,
        IRecipient<RenameFileMessage>,
        IRecipient<MoveFileMessage>,
        IRecipient<ShellActionMessage>,
        IRecipient<TogglePageIndicatorMessage>,
        IRecipient<ClearImageSourceMessage>,
        IRecipient<RefreshDisplayMessage>,
        IRecipient<BookmarksChangedMessage>,
        IRecipient<FocusRequestMessage>
    {
        public IViewerStateService State { get; private set; }
        public new Microsoft.UI.Windowing.AppWindow AppWindow
        {
            get
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            }
        }
        public MainViewModel ViewModel { get; }

        // === Proxy Properties for State ===
        public ObservableCollection<string> Playlist { get => State.Playlist; set => State.Playlist = value; }
        public int CurrentIndex { get => State.CurrentIndex; set => State.CurrentIndex = value; }
        public string CurrentDirectory { get => State.CurrentDirectory; set => State.CurrentDirectory = value; }
        public bool IsGridMode { get => State.IsGridMode; set => State.IsGridMode = value; }

        // === Components and Services ===
        internal ISettingsManager _settings;
        public ISlideshowService SlideshowService { get; private set; } = null!;
        public ISlideshowManager SlideshowManager { get; private set; } = null!;
        public IViewerCacheManager ViewerCacheManager { get; private set; } = null!;
        public IViewerManager ViewerManager { get; private set; } = null!;
        public IGridManager GridManager { get; private set; } = null!;
        public IAppWindowManager AppWindowManager { get; private set; } = null!;
        public IMenuStateManager MenuStateManager { get; private set; } = null!;
        public IBookmarkManager BookmarkManager { get; private set; } = null!;
        public IPlaylistManager PlaylistManager { get; private set; } = null!;
        public IPrintService PrintService { get; private set; } = null!;
        public IAnimationService AnimationService { get; private set; } = null!;
        public IDialogService DialogService { get; private set; } = null!;
        public INotificationService NotificationService { get; private set; } = null!;
        public IMetadataDisplayService MetadataDisplayService { get; private set; } = null!;
        public IImageEditService ImageEditService { get; private set; } = null!;
        public IInputHandler InputHandler { get; private set; } = null!;
        public IFileOperationService FileOperationService { get; private set; } = null!;

        Border IAppWindowHost.AppTitleBar => AppTitleBar;
        Grid IAppWindowHost.RootGrid => RootGrid;
        IntPtr IAppWindowHost.WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(this);
        UIElement INotificationHost.NotificationOverlay => NotificationOverlay;
        TextBlock INotificationHost.NotificationText => NotificationOverlay.Text;
        Microsoft.UI.Dispatching.DispatcherQueue IAnimationHost.DispatcherQueue => DispatcherQueue;
        IViewerManager IAnimationHost.ViewerManager => ViewerManager;
        IMetadataDisplayService IAnimationHost.MetadataDisplayService => MetadataDisplayService;
        bool IAnimationHost.IsGridMode { get => IsGridMode; set => IsGridMode = value; }
        System.Collections.ObjectModel.ObservableCollection<string> IAnimationHost.Playlist => Playlist;
        UIElement IFileOperationHost.Content => this.Content;
        bool IFileOperationHost.IsDialogOpen { get => IsDialogOpen; set => IsDialogOpen = value; }
        IntPtr IFileOperationHost.WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(this);
        IPlaylistManager IFileOperationHost.PlaylistManager => PlaylistManager;
        IImageEditService IFileOperationHost.ImageEditService => ImageEditService;
        IViewerManager IFileOperationHost.ViewerManager => ViewerManager;
        void IFileOperationHost.ShowNotification(string message) => ShowNotification(message);
        Grid IInputHost.RootGrid => RootGrid;
        Grid IInputHost.PagesGrid => PagesGrid;
        ScrollViewer IInputHost.ImageScrollViewer => ImageScrollViewer;
        IViewerManager IInputHost.ViewerManager => ViewerManager;
        IMetadataDisplayService IInputHost.MetadataDisplayService => MetadataDisplayService;
        IImageEditService IInputHost.ImageEditService => ImageEditService;
        ISlideshowManager IInputHost.SlideshowManager => SlideshowManager;
        bool IInputHost.IsGridMode { get => IsGridMode; set => IsGridMode = value; }
        bool IInputHost.IsDialogOpen { get => IsDialogOpen; set => IsDialogOpen = value; }
        bool IInputHost.IsFullscreen { get => IsFullscreen; set => IsFullscreen = value; }
        string IInputHost.CurrentImagePath => CurrentImagePath;
        int IInputHost.CurrentIndex { get => CurrentIndex; set => CurrentIndex = value; }
        System.Collections.ObjectModel.ObservableCollection<string> IInputHost.Playlist => Playlist;
        UIElement ISlideshowHost.Content => this.Content;
        IViewerManager ISlideshowHost.ViewerManager => ViewerManager;
        ContentDialog ISlideshowHost.SlideshowDialog => SlideshowDialog;
        ComboBox ISlideshowHost.SlideshowMangaSplitCount => SlideshowMangaSplitCount;
        CheckBox ISlideshowHost.SlideshowFullscreen => SlideshowFullscreen;
        CheckBox ISlideshowHost.SlideshowRandom => SlideshowRandom;
        CheckBox ISlideshowHost.SlideshowLoop => SlideshowLoop;
        CheckBox ISlideshowHost.SlideshowNextFolder => SlideshowNextFolder;
        CheckBox ISlideshowHost.SlideshowIncludeSiblings => SlideshowIncludeSiblings;
        CheckBox ISlideshowHost.SlideshowCurrentFolderOnly => SlideshowCurrentFolderOnly;
        ComboBox ISlideshowHost.SlideshowStretchMode => SlideshowStretchMode;
        CheckBox ISlideshowHost.SlideshowCrossfade => SlideshowCrossfade;
        NumberBox ISlideshowHost.SlideshowInterval => SlideshowInterval;
        NumberBox ISlideshowHost.SlideshowCrossfadeDuration => SlideshowCrossfadeDuration;
        Task ISlideshowHost.UpdateDisplayAsync() => UpdateDisplayAsync();
        Microsoft.UI.Dispatching.DispatcherQueue IPrintHost.DispatcherQueue => DispatcherQueue;
        IntPtr IPrintHost.WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(this);
        void IPrintHost.ShowNotification(string message) => ShowNotification(message);
        Microsoft.UI.Dispatching.DispatcherQueue IImageEditHost.DispatcherQueue => DispatcherQueue;
        IViewerManager IImageEditHost.ViewerManager => ViewerManager;
        Grid IMetadataHost.PagesGrid => PagesGrid;
        ViewerPanel IMetadataHost.ViewerControl => ViewerControl;
        TextBlock IMetadataHost.TxtMetaTitle => TxtMetaTitle;
        TextBlock IMetadataHost.TxtMetaFileName => TxtMetaFileName;
        TextBlock IMetadataHost.TxtMetaDimensions => TxtMetaDimensions;
        TextBlock IMetadataHost.TxtMetaFileSize => TxtMetaFileSize;
        UIElement IMetadataHost.ExifDivider => ExifDivider;
        UIElement IMetadataHost.ExifGrid => ExifGrid;
        TextBlock IMetadataHost.TxtMetaCamera => TxtMetaCamera;
        TextBlock IMetadataHost.TxtMetaLens => TxtMetaLens;
        TextBlock IMetadataHost.TxtMetaSettings => TxtMetaSettings;
        TextBlock IMetadataHost.TxtMetaDate => TxtMetaDate;
        Microsoft.UI.Dispatching.DispatcherQueue IViewerLoaderHost.DispatcherQueue => DispatcherQueue;
        ISlideshowManager IViewerLoaderHost.SlideshowManager => SlideshowManager;
        IImageEditService IViewerLoaderHost.ImageEditService => ImageEditService;
        void IViewerLoaderHost.ShowNotification(string message) => ShowNotification(message);
        Grid IOverlayHost.RootGrid => RootGrid;
        bool IOverlayHost.IsDialogOpen { get => IsDialogOpen; set => IsDialogOpen = value; }
        Microsoft.UI.Dispatching.DispatcherQueue IOverlayHost.DispatcherQueue => DispatcherQueue;
        IntPtr IOverlayHost.WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(this);

        // === UI Helpers ===
        public ViewerPanel ViewerControl => ViewerControlInternal;
        public Grid PagesGrid => ViewerControlInternal.CurrentBuffer;
        public IList<ImageItem> GridItems => GridManager?.GridItems ?? Array.Empty<ImageItem>();
        public GridView ImageGridView => GridControlInternal.GridView;
        public ScrollViewer ImageScrollViewer => ViewerControlInternal.ScrollViewer;

        public FrameworkElement PageGrid1 => ViewerControlInternal.PageControlsBuffer[ViewerControlInternal.CurrentBufferIndex][0];
        public FrameworkElement PageGrid2 => ViewerControlInternal.PageControlsBuffer[ViewerControlInternal.CurrentBufferIndex][1];
        public FrameworkElement PageGrid3 => ViewerControlInternal.PageControlsBuffer[ViewerControlInternal.CurrentBufferIndex][2];
        public FrameworkElement PageGrid4 => ViewerControlInternal.PageControlsBuffer[ViewerControlInternal.CurrentBufferIndex][3];

        private readonly DispatcherTimer _resizeTimer;
        private readonly DispatcherTimer _topHoverTimer;
        private readonly DispatcherTimer _leftHoverTimer;
        private bool _isDialogOpen;
        public bool IsDialogOpen { get => _isDialogOpen; set { _isDialogOpen = value; ViewModel.IsDialogOpen = value; } }
        private bool _isFirstLoad = true;
        public bool IsSearchingFolder { get => ViewModel.IsSearchingFolder; set => ViewModel.IsSearchingFolder = value; }

        private bool _isFullscreen;
        public bool IsFullscreen
        {
            get => _isFullscreen;
            set
            {
                if (_isFullscreen != value)
                {
                    _isFullscreen = value;

                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                    var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                    if (_isFullscreen)
                    {
                        appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
                        AppTitleBar.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
                        AppTitleBar.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        public MainWindow()
        {
            var services = ((App)Application.Current).Services;
            ((App)Application.Current).SetMainView(this);

            State = services.GetRequiredService<IViewerStateService>();
            if (State is System.ComponentModel.INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IViewerStateService.IsGridMode))
                    {
                        UpdateContextFlyout();
                    }
                };
            }
            _settings = services.GetRequiredService<ISettingsManager>();
            ViewModel = services.GetRequiredService<MainViewModel>();

            this.InitializeComponent();

            RootGrid.DataContext = ViewModel;
            ResolveServices(services);

            var timers = CreateTimers();
            _resizeTimer = timers.ResizeTimer;
            _topHoverTimer = timers.TopHoverTimer;
            _leftHoverTimer = timers.LeftHoverTimer;
            WireControlEvents();

            // Sync ViewModel with Settings
            ViewModel.MangaSplitCount = _settings.MangaSplitCount;
            ViewModel.ShowPageIndicator = _settings.ShowPageIndicator;
            ViewModel.BoundaryAction = _settings.BoundaryAction;

            this.Closed += MainWindow_Closed;
            AppWindowManager!.InitializeWindow();
            UpdateContextFlyout();
            WireRootEvents();

            RegisterMessages();
        }

        private void ResolveServices(IServiceProvider services)
        {
            SlideshowService = services.GetRequiredService<ISlideshowService>();
            SlideshowManager = services.GetRequiredService<ISlideshowManager>();
            ViewerCacheManager = services.GetRequiredService<IViewerCacheManager>();
            ViewerManager = services.GetRequiredService<IViewerManager>();
            GridManager = services.GetRequiredService<IGridManager>();
            AppWindowManager = services.GetRequiredService<IAppWindowManager>();
            MenuStateManager = services.GetRequiredService<IMenuStateManager>();
            BookmarkManager = services.GetRequiredService<IBookmarkManager>();
            PlaylistManager = services.GetRequiredService<IPlaylistManager>();
            PrintService = services.GetRequiredService<IPrintService>();
            AnimationService = services.GetRequiredService<IAnimationService>();
            DialogService = services.GetRequiredService<IDialogService>();
            NotificationService = services.GetRequiredService<INotificationService>();
            MetadataDisplayService = services.GetRequiredService<IMetadataDisplayService>();
            ImageEditService = services.GetRequiredService<IImageEditService>();
            FileOperationService = services.GetRequiredService<IFileOperationService>();
            InputHandler = services.GetRequiredService<IInputHandler>();
        }

        private void RegisterMessages()
        {
            WeakReferenceMessenger.Default.Register<FullscreenMessage>(this);
            WeakReferenceMessenger.Default.Register<PlaylistUpdatedMessage>(this);
            WeakReferenceMessenger.Default.Register<SlideshowNextRequestedMessage>(this);
            WeakReferenceMessenger.Default.Register<ToggleGridMessage>(this);
            WeakReferenceMessenger.Default.Register<ToggleMangaMessage>(this);
            WeakReferenceMessenger.Default.Register<ToggleReadingDirectionMessage>(this);
            WeakReferenceMessenger.Default.Register<ToggleStretchMessage>(this);
            WeakReferenceMessenger.Default.Register<CopyPathMessage>(this);
            WeakReferenceMessenger.Default.Register<DeleteFileMessage>(this);
            WeakReferenceMessenger.Default.Register<RenameFileMessage>(this);
            WeakReferenceMessenger.Default.Register<MoveFileMessage>(this);
            WeakReferenceMessenger.Default.Register<ShellActionMessage>(this);
            WeakReferenceMessenger.Default.Register<TogglePageIndicatorMessage>(this);
            WeakReferenceMessenger.Default.Register<ClearImageSourceMessage>(this);
            WeakReferenceMessenger.Default.Register<RefreshDisplayMessage>(this);
            WeakReferenceMessenger.Default.Register<BookmarksChangedMessage>(this);
            WeakReferenceMessenger.Default.Register<FocusRequestMessage>(this);
        }

        private (DispatcherTimer ResizeTimer, DispatcherTimer TopHoverTimer, DispatcherTimer LeftHoverTimer) CreateTimers()
        {
            var resizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            resizeTimer.Tick += (s, e) => { resizeTimer.Stop(); RequestDisplayUpdate(); };

            var topHoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            topHoverTimer.Tick += (s, e) => { topHoverTimer.Stop(); ViewModel.IsTopPanelVisible = false; };

            var leftHoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            leftHoverTimer.Tick += (s, e) =>
            {
                leftHoverTimer.Stop();
                leftHoverTimer.Interval = TimeSpan.FromMilliseconds(300);
                ViewModel.IsBookmarkPanelHovered = false;
            };
            return (resizeTimer, topHoverTimer, leftHoverTimer);
        }

        private void WireControlEvents()
        {
            ViewerControlInternal.PaintSurfaceRequested += (s, e) => ViewerManager!.PaintCanvas(e.bufferIndex, e.pageIndex, e.args);
            GridControlInternal.GridView.ItemClick += ImageGridView_ItemClick;
            GridControlInternal.GridView.SizeChanged += ImageGridView_SizeChanged;
        }

        private void WireRootEvents()
        {
            RootGrid.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(RootGrid_PointerPressed), true);
            RootGrid.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(RootGrid_PointerMoved), true);
            RootGrid.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(RootGrid_PointerReleased), true);
            RootGrid.AddHandler(UIElement.PointerEnteredEvent, new PointerEventHandler(RootGrid_PointerMoved), true);
            RootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(RootGrid_KeyDown), true);
        }


        public void Receive(FullscreenMessage message)
        {
            IsFullscreen = !IsFullscreen;
            ShowNotification(_settings.GetString(IsFullscreen ? "Notification_FullscreenOn" : "Notification_FullscreenOff"));
        }

        public async void Receive(PlaylistUpdatedMessage message)
        {
            UpdateGridItems(message.ForceFullGridUpdate);

            if (_isFirstLoad)
            {
                _isFirstLoad = false;
                // 起動直後はUIのレイアウトが完了するまで少し待機する
                await Task.Delay(200);
            }

            RequestDisplayUpdate();
        }

        public void Receive(SlideshowNextRequestedMessage message)
        {
            HandleSlideshowNextRequest();
        }

        public void Receive(ToggleGridMessage message)
        {
            IsGridMode = !IsGridMode;
            ShowNotification(_settings.GetString(IsGridMode ? "Notification_GridModeOn" : "Notification_GridModeOff"));
            RequestDisplayUpdate();
        }

        public void Receive(ToggleReadingDirectionMessage message)
        {
            _settings.IsRightToLeft = !_settings.IsRightToLeft;
            _settings.SaveMangaMode();
            ViewModel.Viewer.IsRightToLeft = _settings.IsRightToLeft;
            if (_settings.MangaSplitCount > 1) WeakReferenceMessenger.Default.Send(new RefreshDisplayMessage());
            MenuStateManager.UpdateMenuStates();
            ShowReadingDirectionNotification();
        }

        public void Receive(ToggleMangaMessage message)
        {
            int count = _settings.MangaSplitCount == 1 ? 2 : (_settings.MangaSplitCount == 2 ? 4 : 1);
            _settings.MangaSplitCount = count;
            _settings.SaveMangaMode();
            ViewModel.MangaSplitCount = count;
            RequestDisplayUpdate();
            ShowLocalizedNotification(GetMangaModeResourceKey(count));
        }

        public void Receive(ToggleStretchMessage message)
        {
            int current = _settings.ImageStretchMode;
            int next = current == 2 ? 3 : (current == 3 ? 0 : 2);
            _settings.ImageStretchMode = next;
            _settings.SaveSettings();
            ViewerManager.UpdateStretch(true);
            ShowLocalizedNotification(GetStretchModeResourceKey(next));
        }

        public void Receive(CopyPathMessage message)
        {
            CopyPathToClipboard(message.Path);
        }

        public void Receive(DeleteFileMessage message)
        {
            _ = DeleteFileAsync(message.Path);
        }

        public async Task DeleteFileAsync(string path)
        {
            await FileOperationService.HandleDeleteFileAsync(path);
        }

        public void Receive(RenameFileMessage message)
        {
            RenameFile(message.Path);
        }

        public void Receive(MoveFileMessage message)
        {
            MoveFile(message.Path);
        }

        public void Receive(ShellActionMessage message)
        {
            HandleShellAction(message.Action, message.Path);
        }

        public void Receive(TogglePageIndicatorMessage message)
        {
            MenuStateManager.MenuPageIndicatorToggle_Click(null!, null!);
        }

        public void Receive(ClearImageSourceMessage message)
        {
            ClearCachedBitmap(message.Path);
        }

        public void Receive(RefreshDisplayMessage message)
        {
            RequestDisplayUpdate();
        }

        public void Receive(BookmarksChangedMessage message)
        {
            UpdateBookmarkMenu();
        }

        public void Receive(FocusRequestMessage message)
        {
            FocusActiveSurface();
        }

        private void HandleSlideshowNextRequest()
        {
            if (!ViewModel.IsSlideshowRunning) return;
            if (_settings.SlideshowRandom)
            {
                RequestDisplayUpdate();
                return;
            }

            if (!IsAtSlideshowBoundary())
            {
                RequestDisplayUpdate();
                return;
            }

            if (_settings.SlideshowNextFolder)
            {
                WeakReferenceMessenger.Default.Send(new FolderNavigationMessage(1));
                return;
            }

            if (_settings.SlideshowLoop)
            {
                CurrentIndex = 0;
                RequestDisplayUpdate();
            }
        }

        private bool IsAtSlideshowBoundary()
        {
            int increment = _settings.MangaSplitCount;
            return CurrentIndex + increment >= Playlist.Count;
        }

        private void CopyPathToClipboard(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(path);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            ShowNotification(_settings.GetString("Notification_PathCopied"));
        }

        private void RenameFile(string path)
        {
            _ = FileOperationService.HandleRenameFileAsync(path);
        }

        private void MoveFile(string path)
        {
            _ = FileOperationService.HandleMoveFileAsync(path);
        }

        private void FocusActiveSurface()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (IsGridMode) ImageGridView.Focus(FocusState.Programmatic);
                else RootGrid.Focus(FocusState.Programmatic);
            });
        }

        private void HandleShellAction(string action, string path)
        {
            switch (action)
            {
                case "OpenExplorer":
                    OpenInExplorer(path);
                    break;
                case "Settings":
                    OpenSettingsDialog();
                    break;
                case "KeyBindings":
                    OpenKeyBindingsDialog();
                    break;
                case "Support":
                    OpenSupportPage();
                    break;
            }
        }

        private void OpenInExplorer(string path)
        {
            MenuStateManager.ContextTargetPath = path;
            MenuStateManager.MenuOpenExplorer_Click(null!, null!);
        }

        private void OpenSettingsDialog() => MenuStateManager.MenuSettings_Click(null!, null!);
        private void OpenKeyBindingsDialog() => MenuStateManager.MenuKeyBindings_Click(null!, null!);
        private void OpenSupportPage() => MenuStateManager.MenuSupport_Click(null!, null!);

        private void ShowReadingDirectionNotification()
        {
            string direction = _settings.IsRightToLeft ? "RTL" : "LTR";
            try
            {
                string label = GetResourceLoader().GetString("MenuReadingDirection/Text");
                ShowNotification($"{label}: {direction}");
            }
            catch
            {
                ShowNotification($"Reading Direction: {direction}");
            }
        }

        private void ShowLocalizedNotification(string resourceKey)
        {
            ShowNotification(GetResourceLoader().GetString(resourceKey));
        }

        private static string GetMangaModeResourceKey(int mangaSplitCount)
        {
            return mangaSplitCount == 1
                ? "MenuViewMode_Single/Text"
                : (mangaSplitCount == 2 ? "MenuViewMode_Double/Text" : "MenuViewMode_Quad/Text");
        }

        private static string GetStretchModeResourceKey(int stretchMode)
        {
            return stretchMode == 2
                ? "MenuStretchContain/Text"
                : (stretchMode == 3 ? "MenuStretchCover/Text" : "MenuStretchOriginal/Text");
        }

        private static Microsoft.Windows.ApplicationModel.Resources.ResourceLoader GetResourceLoader()
            => new();

        private void UpdateBookmarkMenu()
        {
            // Update both viewer and grid bookmark menus
            UpdateBookmarkMenuList(MenuBookmarkList);
            UpdateBookmarkMenuList(GridMenuBookmarkList);
        }

        private void UpdateBookmarkMenuList(MenuFlyoutSubItem menu)
        {
            if (menu == null) return;
            menu.Items.Clear();
            if (_settings.Bookmarks.Count == 0)
            {
                menu.Items.Add(CreateEmptyBookmarkMenuItem());
            }
            else
            {
                foreach (var bm in _settings.Bookmarks)
                {
                    menu.Items.Add(CreateBookmarkMenuItem(bm.Name, bm.Path));
                }
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            DisposeManagedResources();
            AppWindowManager.SaveWindowState();
        }



        private void ImageGridView_ItemClick(object sender, ItemClickEventArgs e) => GridManager.ImageGridView_ItemClick(sender, e);
        private void ImageGridView_SizeChanged(object sender, SizeChangedEventArgs e) => GridManager.ImageGridView_SizeChanged(sender, e);
        private void MenuThumbnailRefresh_Click(object sender, RoutedEventArgs e) => GridManager.RefreshThumbnails();

        private void RootGrid_DragOver(object sender, DragEventArgs e) => InputHandler.HandleDragOver(sender, e);
        private void RootGrid_Drop(object sender, DragEventArgs e) => InputHandler.HandleDrop(sender, e);

        public void LoadDirectory(string path, string initialFile = "", bool includeSiblings = false, bool includeSubfolders = false, List<string>? preloadedPlaylist = null)
        {
            PlaylistManager.LoadDirectory(path, initialFile, includeSiblings, includeSubfolders, preloadedPlaylist);
        }

        public void SetGridLoading(bool isLoading, bool isBackground = false)
        {
            if (GridControlInternal != null)
            {
                if (isBackground) GridControlInternal.SetBackgroundLoading(isLoading);
                else GridControlInternal.SetLoading(isLoading);
            }
        }
        public void UpdateGridItems(bool forceFullUpdate) => GridManager.UpdateGridItems(forceFullUpdate);

        public Task UpdateDisplayAsync() => ViewerManager.UpdateDisplayAsync();

        public void UpdatePageIndicator() => ViewModel.UpdatePageIndicator();

        public List<FrameworkElement> GetPageGrids() => [PageGrid1, PageGrid2, PageGrid3, PageGrid4];

        public Visibility BoolToVis(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
        public Visibility BoolToVisInverse(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
        public Visibility GetSearchingOverlayVisibility(bool isSearching, bool isSlideshowRunning)
            => (isSearching && !isSlideshowRunning) ? Visibility.Visible : Visibility.Collapsed;

        public IntPtr WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(this);
        public string CurrentImagePath => ViewModel.Playlist != null && ViewModel.CurrentIndex >= 0 && ViewModel.CurrentIndex < ViewModel.Playlist.Count ? ViewModel.Playlist[ViewModel.CurrentIndex] : string.Empty;
        public void ShowNotification(string message) => ViewerManager.ShowNotification(message);
        public void UpdateContextFlyout()
        {
            RootGrid.ContextFlyout = IsGridMode ? GridMenuFlyout : EditMenuFlyout;
        }
        public void ClearCachedBitmap(string path)
        {
            if (ViewerManager != null)
            {
                foreach (var ctrl in ViewerManager.PageControls) ctrl.PageImage.Source = null;
                for (int pi = 0; pi < ViewerManager.Pages.Length; pi++)
                {
                    if (ViewerManager.Pages[pi].CurrentFilePath == path)
                    {
                        ViewerManager.Pages[pi].EditedBitmap = null;
                        ViewerManager.Pages[pi].CurrentFilePath = null;
                    }
                }
            }
        }



        private void RootGrid_PointerWheelChanged(object sender, PointerRoutedEventArgs e) => InputHandler.HandlePointerWheelChanged(sender, e);
        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ViewerManager.HandleWindowSizeChanged(e.NewSize.Width, e.NewSize.Height);
        }

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e) => InputHandler.HandlePointerPressed(sender, e);

        private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            InputHandler.HandlePointerMoved(sender, e);
            if (e.Handled) return;

            var point = e.GetCurrentPoint(RootGrid).Position;
            UpdateTopPanelHoverState(point);
            UpdateBookmarkPanelHoverState(point);
        }

        private void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e) => InputHandler.HandlePointerReleased(sender, e);

        private void RootGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => InputHandler.HandleDoubleTapped(sender, e);
        private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e) => InputHandler.HandleKeyDown(sender, e);

        private void EditMenuFlyout_Opening(object sender, object e) => MenuStateManager.EditMenuFlyout_Opening(sender, e);
        private void RootGrid_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (ViewModel.IsGridMode)
            {
                UpdateGridContextTargetFromRightTap(e);
            }
            else
            {
                var point = e.GetPosition(PagesGrid);
                MenuStateManager.UpdateTargetIndexAtPoint(point);
                ViewModel.Editor.ContextPath = MenuStateManager.ContextTargetPath;
            }
        }

        private void UpdateTopPanelHoverState(Windows.Foundation.Point point)
        {
            bool isTopEdge = point.Y <= 60;
            bool isOverTopPanel = point.Y <= 80 && ViewModel.IsTopPanelVisible;

            if (isTopEdge || isOverTopPanel)
            {
                ViewModel.IsTopPanelVisible = true;
                _topHoverTimer.Stop();
                return;
            }

            if (ViewModel.IsTopPanelVisible) _topHoverTimer.Start();
        }

        private void UpdateBookmarkPanelHoverState(Windows.Foundation.Point point)
        {
            bool isLeftEdge = point.X <= 60;
            bool isOverBookmarkPanel = point.X <= 280 && ViewModel.IsBookmarkPanelHovered;
            if (!isLeftEdge && !isOverBookmarkPanel)
            {
                if (ViewModel.IsBookmarkPanelHovered) _leftHoverTimer.Start();
                return;
            }

            if (ShouldSuppressBookmarkPanelHover(isOverBookmarkPanel))
            {
                return;
            }

            ViewModel.IsBookmarkPanelHovered = true;
            _leftHoverTimer.Stop();
        }

        private bool ShouldSuppressBookmarkPanelHover(bool isOverBookmarkPanel)
        {
            if (ViewModel.IsVideoTransportHovered && !isOverBookmarkPanel)
            {
                if (ViewModel.IsBookmarkPanelHovered) _leftHoverTimer.Start();
                return true;
            }

            if (_leftHoverTimer.IsEnabled && _leftHoverTimer.Interval >= TimeSpan.FromMilliseconds(500))
            {
                return true;
            }

            return false;
        }

        private void UpdateGridContextTargetFromRightTap(RightTappedRoutedEventArgs e)
        {
            if (TrySetContextTargetFromOriginalSource(e)) return;

            var selected = GridControlInternal.GridView.SelectedItem as ImageItem;
            if (selected == null) return;
            SetContextTargetPath(selected.FilePath);
        }

        private bool TrySetContextTargetFromOriginalSource(RightTappedRoutedEventArgs e)
        {
            if (e.OriginalSource is not FrameworkElement fe) return false;
            if (fe.DataContext is not ImageItem imageItem) return false;
            SetContextTargetPath(imageItem.FilePath);
            return true;
        }

        private void SetContextTargetPath(string filePath)
        {
            MenuStateManager.ContextTargetPath = filePath;
            ViewModel.Editor.ContextPath = filePath;
        }

        private MenuFlyoutItem CreateEmptyBookmarkMenuItem()
            => new() { Text = _settings.GetString("Bookmark_Empty.Text"), IsEnabled = false };

        private MenuFlyoutItem CreateBookmarkMenuItem(string name, string path)
        {
            var item = new MenuFlyoutItem { Text = name, Tag = path };
            item.Click += (s, e) => LoadDirectory(path);
            return item;
        }






        private void MenuBookmarksToggle_Click(object sender, RoutedEventArgs e) => BookmarkManager.MenuBookmarksToggle_Click(sender, e);
        private void MenuMetadata_Click(object sender, RoutedEventArgs e) => MenuStateManager.MenuMetadata_Click(sender, e);
        private void MenuPageIndicatorToggle_Click(object sender, RoutedEventArgs e) => MenuStateManager.MenuPageIndicatorToggle_Click(sender, e);
        private void MenuViewMode_Click(object sender, RoutedEventArgs e) => MenuStateManager.MenuViewMode_Click(sender, e);
        private void MenuLayoutMode_Click(object sender, RoutedEventArgs e) => MenuStateManager.MenuLayoutMode_Click(sender, e);
        private void MenuDirection_Click(object sender, RoutedEventArgs e) => MenuStateManager.MenuDirection_Click(sender, e);
        private void MenuStretchMode_Click(object sender, RoutedEventArgs e) => MenuStateManager.MenuStretchMode_Click(sender, e);
        private void MenuSort_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string sortType)
            {
                // TODO: Implement sorting logic in PlaylistManager
                ShowNotification(_settings.GetString("MenuSort_" + sortType + ".Text") + " (Not Implemented)");
            }
        }
        private void BookmarkListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            BookmarkManager.BookmarkListView_ItemClick(sender, e);
            CollapseBookmarkPanelTemporarily();
        }
        private void BookmarkListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) => BookmarkManager.BookmarkListView_DragItemsCompleted(sender, args);
        private void MenuBookmarkRemove_Click(object sender, RoutedEventArgs e) => BookmarkManager.MenuBookmarkRemove_Click(sender, e);
        private void SlideshowDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args) => SlideshowManager.SlideshowDialog_Opened(sender, args);
        private void SlideshowDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args) => SlideshowManager.SlideshowDialog_PrimaryButtonClick(sender, args);

        private async void OpenFolderButton_Click(object sender, object e)
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                LoadDirectory(folder.Path);
            }
        }

        private void TopPanel_PanelHoverStarted(object? sender, EventArgs e) => _topHoverTimer.Stop();
        private void TopPanel_PanelHoverEnded(object? sender, EventArgs e) => _topHoverTimer.Start();
        private void BookmarkPanel_PanelHoverStarted(object? sender, EventArgs e) => _leftHoverTimer.Stop();
        private void BookmarkPanel_PanelHoverEnded(object? sender, EventArgs e) => _leftHoverTimer.Start();

        private void RequestDisplayUpdate() => _ = UpdateDisplayAsync();

        private void DisposeManagedResources()
        {
            PrintService.UnregisterForPrinting();
            ViewerManager.Dispose();
            GridManager.Dispose();
            SlideshowManager.Dispose();
            foreach (var item in GridItems) item.DisposeCodec();
        }

        private void CollapseBookmarkPanelTemporarily()
        {
            _leftHoverTimer.Stop();
            _leftHoverTimer.Interval = TimeSpan.FromSeconds(1);
            _leftHoverTimer.Start();
            ViewModel.IsBookmarkPanelHovered = false;
        }
    }
}
