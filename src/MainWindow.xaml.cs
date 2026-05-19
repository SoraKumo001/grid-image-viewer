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
    public sealed partial class MainWindow : Window, IMainView,
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
        public ISlideshowService SlideshowService { get; private set; }
        public ISlideshowManager SlideshowManager { get; private set; }
        public IViewerCacheManager ViewerCacheManager { get; private set; }
        public IViewerManager ViewerManager { get; private set; }
        public IGridManager GridManager { get; private set; }
        public IAppWindowManager AppWindowManager { get; private set; }
        public IMenuStateManager MenuStateManager { get; private set; }
        public IBookmarkManager BookmarkManager { get; private set; }
        public IPlaylistManager PlaylistManager { get; private set; }
        public IPrintService PrintService { get; private set; }
        public IAnimationService AnimationService { get; private set; }
        public IDialogService DialogService { get; private set; }
        public INotificationService NotificationService { get; private set; }
        public IMetadataDisplayService MetadataDisplayService { get; private set; }
        public IImageEditService ImageEditService { get; private set; }
        public IInputHandler InputHandler { get; private set; }
        public IFileOperationService FileOperationService { get; private set; }

        IMetadataDisplayService IMainView.MetadataDisplayService => MetadataDisplayService;
        IImageEditService IMainView.ImageEditService => ImageEditService;
        IDialogService IMainView.DialogService => DialogService;
        IAnimationService IMainView.AnimationService => AnimationService;
        INotificationService IMainView.NotificationService => NotificationService;
        IPlaylistManager IMainView.PlaylistManager => PlaylistManager;
        IViewerManager IMainView.ViewerManager => ViewerManager;
        IGridManager IMainView.GridManager => GridManager;
        IMenuStateManager IMainView.MenuStateManager => MenuStateManager;
        ListView IMainView.BookmarkListView => BookmarkPanel.ListView;
        IPrintService IMainView.PrintService => PrintService;
        IBookmarkManager IMainView.BookmarkManager => BookmarkManager;
        IAppWindowManager IMainView.AppWindowManager => AppWindowManager;
        System.Collections.Generic.List<FrameworkElement> IMainView.GetPageGrids() => GetPageGrids();
        FrameworkElement IMainView.PageGrid1 => PageGrid1;
        FrameworkElement IMainView.PageGrid2 => PageGrid2;
        FrameworkElement IMainView.PageGrid3 => PageGrid3;
        FrameworkElement IMainView.PageGrid4 => PageGrid4;



        MenuFlyoutSubItem IMainView.MenuBookmarkList => MenuBookmarkList;

        ContentDialog IMainView.SlideshowDialog => SlideshowDialog;
        ComboBox IMainView.SlideshowMangaSplitCount => SlideshowMangaSplitCount;
        CheckBox IMainView.SlideshowFullscreen => SlideshowFullscreen;
        CheckBox IMainView.SlideshowRandom => SlideshowRandom;
        CheckBox IMainView.SlideshowLoop => SlideshowLoop;
        CheckBox IMainView.SlideshowNextFolder => SlideshowNextFolder;
        CheckBox IMainView.SlideshowIncludeSiblings => SlideshowIncludeSiblings;
        CheckBox IMainView.SlideshowCurrentFolderOnly => SlideshowCurrentFolderOnly;
        ComboBox IMainView.SlideshowStretchMode => SlideshowStretchMode;
        CheckBox IMainView.SlideshowCrossfade => SlideshowCrossfade;
        NumberBox IMainView.SlideshowInterval => SlideshowInterval;
        NumberBox IMainView.SlideshowCrossfadeDuration => SlideshowCrossfadeDuration;
        UIElement IMainView.Content => this.Content;
        GridView IMainView.ImageGridView => GridControlInternal.GridView;
        void IMainView.ShowNotification(string message) => ShowNotification(message);
        void IMainView.StopAnimation() => ViewerManager?.StopAnimation();
        void IMainView.ClearCachedBitmap(string path)
        {
            if (ViewerManager != null)
            {
                foreach (var ctrl in ViewerManager.PageControls) ctrl.PageImage.Source = null;
                for (int pi = 0; pi < ViewerManager.Pages.Length; pi++)
                {
                    if (ViewerManager.Pages[pi].CurrentFilePath == path)
                    {
                        ViewerManager.Pages[pi].EditedBitmap = null;
                        ViewerManager.Pages[pi].CurrentFilePath = null; // Force reload
                    }
                }
            }
        }

        void IMainView.Close() => Close();
        void IMainView.SetGridLoading(bool isLoading, bool isBackground)
        {
            if (GridControlInternal != null)
            {
                if (isBackground) GridControlInternal.SetBackgroundLoading(isLoading);
                else GridControlInternal.SetLoading(isLoading);
            }
        }
        Microsoft.UI.Windowing.AppWindow IMainView.AppWindow => AppWindow;
        System.Collections.ObjectModel.ObservableCollection<string> IMainView.Playlist => ViewModel.Playlist;
        int IMainView.CurrentIndex { get => ViewModel.CurrentIndex; set => ViewModel.CurrentIndex = value; }
        bool IMainView.IsSearchingFolder { get => IsSearchingFolder; set => IsSearchingFolder = value; }
        Microsoft.UI.Dispatching.DispatcherQueue IMainView.DispatcherQueue => DispatcherQueue;
        void IMainView.UpdateGridItems(bool force) => UpdateGridItems(force);
        Windows.Foundation.Rect IMainView.Bounds => Bounds;
        string IMainView.CurrentDirectory { get => CurrentDirectory; set => CurrentDirectory = value; }
        ScrollViewer IMainView.ImageScrollViewer => ImageScrollViewer;
        UIElement IMainView.NotificationOverlay => NotificationOverlay;
        TextBlock IMainView.NotificationText => NotificationOverlay.Text;
        TextBlock IMainView.TxtMetaTitle => TxtMetaTitle;
        TextBlock IMainView.TxtMetaFileName => TxtMetaFileName;
        TextBlock IMainView.TxtMetaDimensions => TxtMetaDimensions;
        TextBlock IMainView.TxtMetaFileSize => TxtMetaFileSize;
        UIElement IMainView.ExifDivider => ExifDivider;
        UIElement IMainView.ExifGrid => ExifGrid;
        TextBlock IMainView.TxtMetaCamera => TxtMetaCamera;
        TextBlock IMainView.TxtMetaLens => TxtMetaLens;
        TextBlock IMainView.TxtMetaSettings => TxtMetaSettings;
        TextBlock IMainView.TxtMetaDate => TxtMetaDate;
        Grid IMainView.PagesGrid => ViewerControlInternal.RootPagesContainer;
        Grid IMainView.RootGrid => RootGrid;
        bool IMainView.IsDialogOpen { get => IsDialogOpen; set => IsDialogOpen = value; }
        IntPtr IMainView.WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(this);
        bool IMainView.ExtendsContentIntoTitleBar { get => ExtendsContentIntoTitleBar; set => ExtendsContentIntoTitleBar = value; }
        void IMainView.SetTitleBar(UIElement tb) => SetTitleBar(tb);
        Border IMainView.AppTitleBar => AppTitleBar;
        string IMainView.CurrentImagePath => CurrentImagePath;
        void IMainView.UpdateContextFlyout()
        {
            RootGrid.ContextFlyout = IsGridMode ? GridMenuFlyout : EditMenuFlyout;
        }

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
                        ((IMainView)this).UpdateContextFlyout();
                    }
                };
            }
            _settings = services.GetRequiredService<ISettingsManager>();
            ViewModel = services.GetRequiredService<MainViewModel>();

            this.InitializeComponent();

            RootGrid.DataContext = ViewModel;

            SlideshowService = services.GetRequiredService<ISlideshowService>();
            SlideshowManager = services.GetRequiredService<ISlideshowManager>();

            ViewerCacheManager = services.GetRequiredService<IViewerCacheManager>();
            ViewerManager = services.GetRequiredService<IViewerManager>();
            GridManager = services.GetRequiredService<IGridManager>();
            AppWindowManager = services.GetRequiredService<IAppWindowManager>();
            MenuStateManager = services.GetRequiredService<IMenuStateManager>();
            BookmarkManager = services.GetRequiredService<IBookmarkManager>();
            PlaylistManager = services.GetRequiredService<IPlaylistManager>();
            this.PrintService = services.GetRequiredService<IPrintService>();

            this.AnimationService = services.GetRequiredService<IAnimationService>();
            this.DialogService = services.GetRequiredService<IDialogService>();
            this.NotificationService = services.GetRequiredService<INotificationService>();
            this.MetadataDisplayService = services.GetRequiredService<IMetadataDisplayService>();
            ImageEditService = services.GetRequiredService<IImageEditService>();
            FileOperationService = services.GetRequiredService<IFileOperationService>();

            InputHandler = services.GetRequiredService<IInputHandler>();

            _resizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _resizeTimer.Tick += (s, e) => { _resizeTimer.Stop(); _ = UpdateDisplayAsync(); };

            _topHoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _topHoverTimer.Tick += (s, e) => { _topHoverTimer.Stop(); ViewModel.IsTopPanelVisible = false; };

            _leftHoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _leftHoverTimer.Tick += (s, e) =>
            {
                _leftHoverTimer.Stop();
                _leftHoverTimer.Interval = TimeSpan.FromMilliseconds(300); // 1秒ディレイなどから復帰させる
                ViewModel.IsBookmarkPanelHovered = false;
            };

            // Setup Controls
            ViewerControlInternal.PaintSurfaceRequested += (s, e) => ViewerManager.PaintCanvas(e.bufferIndex, e.pageIndex, e.args);
            GridControlInternal.GridView.ItemClick += ImageGridView_ItemClick;
            GridControlInternal.GridView.SizeChanged += ImageGridView_SizeChanged;

            // Sync ViewModel with Settings
            ViewModel.MangaSplitCount = _settings.MangaSplitCount;
            ViewModel.ShowPageIndicator = _settings.ShowPageIndicator;
            ViewModel.BoundaryAction = _settings.BoundaryAction;

            this.Closed += MainWindow_Closed;
            AppWindowManager.InitializeWindow();
            ((IMainView)this).UpdateContextFlyout();

            RootGrid.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(RootGrid_PointerPressed), true);
            RootGrid.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(RootGrid_PointerMoved), true);
            RootGrid.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(RootGrid_PointerReleased), true);
            RootGrid.AddHandler(UIElement.PointerEnteredEvent, new PointerEventHandler(RootGrid_PointerMoved), true);
            RootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(RootGrid_KeyDown), true);

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

            _ = UpdateDisplayAsync();
        }

        public void Receive(SlideshowNextRequestedMessage message)
        {
            if (ViewModel.IsSlideshowRunning)
            {
                if (_settings.SlideshowRandom)
                {
                    _ = UpdateDisplayAsync();
                }
                else
                {
                    int increment = _settings.MangaSplitCount;
                    if (CurrentIndex + increment >= Playlist.Count)
                    {
                        if (_settings.SlideshowNextFolder)
                        {
                            WeakReferenceMessenger.Default.Send(new FolderNavigationMessage(1));
                        }
                        else if (_settings.SlideshowLoop)
                        {
                            CurrentIndex = 0;
                            _ = UpdateDisplayAsync();
                        }
                    }
                    else
                    {
                        _ = UpdateDisplayAsync();
                    }
                }
            }
        }

        public void Receive(ToggleGridMessage message)
        {
            IsGridMode = !IsGridMode;
            ShowNotification(_settings.GetString(IsGridMode ? "Notification_GridModeOn" : "Notification_GridModeOff"));
            _ = UpdateDisplayAsync();
        }

        public void Receive(ToggleReadingDirectionMessage message)
        {
            _settings.IsRightToLeft = !_settings.IsRightToLeft;
            _settings.SaveMangaMode();
            ViewModel.Viewer.IsRightToLeft = _settings.IsRightToLeft;
            if (_settings.MangaSplitCount > 1) WeakReferenceMessenger.Default.Send(new RefreshDisplayMessage());
            MenuStateManager.UpdateMenuStates();

            var loader = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader();
            string direction = _settings.IsRightToLeft ? "RTL" : "LTR";
            try
            {
                string locName = loader.GetString("MenuReadingDirection/Text");
                ShowNotification($"{locName}: {direction}");
            }
            catch
            {
                ShowNotification($"Reading Direction: {direction}");
            }
        }

        public void Receive(ToggleMangaMessage message)
        {
            int count = _settings.MangaSplitCount == 1 ? 2 : (_settings.MangaSplitCount == 2 ? 4 : 1);
            _settings.MangaSplitCount = count;
            _settings.SaveMangaMode();
            ViewModel.MangaSplitCount = count;
            _ = UpdateDisplayAsync();

            var loader = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader();
            string modeKey = count == 1 ? "MenuViewMode_Single/Text" : (count == 2 ? "MenuViewMode_Double/Text" : "MenuViewMode_Quad/Text");
            ShowNotification(loader.GetString(modeKey));
        }

        public void Receive(ToggleStretchMessage message)
        {
            int current = _settings.ImageStretchMode;
            int next = current == 2 ? 3 : (current == 3 ? 0 : 2);
            _settings.ImageStretchMode = next;
            _settings.SaveSettings();
            ViewerManager.UpdateStretch(true);

            var loader = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader();
            string modeKey = next == 2 ? "MenuStretchContain/Text" : (next == 3 ? "MenuStretchCover/Text" : "MenuStretchOriginal/Text");
            ShowNotification(loader.GetString(modeKey));
        }

        public void Receive(CopyPathMessage message)
        {
            if (string.IsNullOrEmpty(message.Path)) return;
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(message.Path);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            ShowNotification(_settings.GetString("Notification_PathCopied"));
        }

        public void Receive(DeleteFileMessage message)
        {
            _ = HandleDeleteFileAsync(message.Path);
        }

        public async Task HandleDeleteFileAsync(string path)
        {
            await FileOperationService.HandleDeleteFileAsync(path);
        }

        public void Receive(RenameFileMessage message)
        {
            _ = FileOperationService.HandleRenameFileAsync(message.Path);
        }

        public void Receive(MoveFileMessage message)
        {
            _ = FileOperationService.HandleMoveFileAsync(message.Path);
        }

        public void Receive(ShellActionMessage message)
        {
            switch (message.Action)
            {
                case "OpenExplorer":
                    MenuStateManager.ContextTargetPath = message.Path;
                    MenuStateManager.MenuOpenExplorer_Click(null!, null!);
                    break;
                case "Settings":
                    MenuStateManager.MenuSettings_Click(null!, null!);
                    break;
                case "KeyBindings":
                    MenuStateManager.MenuKeyBindings_Click(null!, null!);
                    break;
                case "Support":
                    MenuStateManager.MenuSupport_Click(null!, null!);
                    break;
            }
        }

        public void Receive(TogglePageIndicatorMessage message)
        {
            MenuStateManager.MenuPageIndicatorToggle_Click(null!, null!);
        }

        public void Receive(ClearImageSourceMessage message)
        {
            ((IMainView)this).ClearCachedBitmap(message.Path);
        }

        public void Receive(RefreshDisplayMessage message)
        {
            _ = UpdateDisplayAsync();
        }

        public void Receive(BookmarksChangedMessage message)
        {
            UpdateBookmarkMenu();
        }

        public void Receive(FocusRequestMessage message)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (IsGridMode)
                {
                    ImageGridView.Focus(FocusState.Programmatic);
                }
                else
                {
                    RootGrid.Focus(FocusState.Programmatic);
                }
            });
        }

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
                menu.Items.Add(new MenuFlyoutItem { Text = _settings.GetString("Bookmark_Empty.Text"), IsEnabled = false });
            }
            else
            {
                foreach (var bm in _settings.Bookmarks)
                {
                    var bmItem = new MenuFlyoutItem { Text = bm.Name, Tag = bm.Path };
                    bmItem.Click += (s, e) => LoadDirectory(bm.Path);
                    menu.Items.Add(bmItem);
                }
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            PrintService.UnregisterForPrinting();
            ViewerManager.Dispose();
            GridManager.Dispose();
            SlideshowManager.Dispose();
            foreach (var item in GridItems) item.DisposeCodec();

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

        public void UpdateGridItems(bool forceFullUpdate) => GridManager.UpdateGridItems(forceFullUpdate);

        public Task UpdateDisplayAsync() => ViewerManager.UpdateDisplayAsync();

        public void UpdatePageIndicator() => ViewModel.UpdatePageIndicator();

        public List<FrameworkElement> GetPageGrids() => [PageGrid1, PageGrid2, PageGrid3, PageGrid4];

        public Visibility BoolToVis(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
        public Visibility BoolToVisInverse(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
        public Visibility GetSearchingOverlayVisibility(bool isSearching, bool isSlideshowRunning)
            => (isSearching && !isSlideshowRunning) ? Visibility.Visible : Visibility.Collapsed;

        public string CurrentImagePath => ViewModel.Playlist != null && ViewModel.CurrentIndex >= 0 && ViewModel.CurrentIndex < ViewModel.Playlist.Count ? ViewModel.Playlist[ViewModel.CurrentIndex] : string.Empty;
        public void ShowNotification(string message) => ViewerManager.ShowNotification(message);



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
            bool isTopEdge = point.Y <= 60;
            bool isLeftEdge = point.X <= 60;

            // パネル自体にマウスが乗っている場合も表示を維持する
            bool isOverTopPanel = point.Y <= 80 && ViewModel.IsTopPanelVisible;
            bool isOverBookmarkPanel = point.X <= 280 && ViewModel.IsBookmarkPanelHovered;

            if (isTopEdge || isOverTopPanel)
            {
                ViewModel.IsTopPanelVisible = true;
                _topHoverTimer.Stop(); // 非表示タイマーをリセット
            }
            else
            {
                if (ViewModel.IsTopPanelVisible) _topHoverTimer.Start();
            }

            if (isLeftEdge || isOverBookmarkPanel)
            {
                // 動画の再生パネル（トランスポートコントロール）の上にマウスがある場合は、
                // ブックマークリストを表示する判定をスキップする。
                if (ViewModel.IsVideoTransportHovered && !isOverBookmarkPanel)
                {
                    if (ViewModel.IsBookmarkPanelHovered) _leftHoverTimer.Start();
                    return;
                }

                // クリック直後（1秒ディレイ中）は再表示を抑制する
                if (_leftHoverTimer.IsEnabled && _leftHoverTimer.Interval >= TimeSpan.FromMilliseconds(500))
                {
                    return;
                }

                ViewModel.IsBookmarkPanelHovered = true;
                _leftHoverTimer.Stop(); // 非表示タイマーをリセット
            }
            else
            {
                if (ViewModel.IsBookmarkPanelHovered) _leftHoverTimer.Start();
            }
        }

        private void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e) => InputHandler.HandlePointerReleased(sender, e);

        private void RootGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => InputHandler.HandleDoubleTapped(sender, e);
        private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e) => InputHandler.HandleKeyDown(sender, e);

        private void EditMenuFlyout_Opening(object sender, object e) => MenuStateManager.EditMenuFlyout_Opening(sender, e);
        private void RootGrid_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (ViewModel.IsGridMode)
            {
                if (e.OriginalSource is FrameworkElement fe && fe.DataContext is quick_image_viewer.Models.ImageItem imageItem)
                {
                    MenuStateManager.ContextTargetPath = imageItem.FilePath;
                    ViewModel.Editor.ContextPath = imageItem.FilePath;
                }
                else
                {
                    var selected = GridControlInternal.GridView.SelectedItem as quick_image_viewer.Models.ImageItem;
                    if (selected != null)
                    {
                        MenuStateManager.ContextTargetPath = selected.FilePath;
                        ViewModel.Editor.ContextPath = selected.FilePath;
                    }
                }
            }
            else
            {
                var point = e.GetPosition(PagesGrid);
                MenuStateManager.UpdateTargetIndexAtPoint(point);
                ViewModel.Editor.ContextPath = MenuStateManager.ContextTargetPath;
            }
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

            // ブックマーク選択後はホバー状態を解除する。
            // 読み込みが速い場合、この直後のマウスポインター判定で再度パネルが開いてしまうのを防ぐため、
            // 少し長めのディレイ（1秒）を設定して閉じる。
            _leftHoverTimer.Stop();
            _leftHoverTimer.Interval = TimeSpan.FromSeconds(1);
            _leftHoverTimer.Start();

            // 重要：タイマーの Interval を元に戻すため、一回限りのリセットフラグや
            // Tick内でのリセット処理を検討するが、ここでは単純に閉じる。
            ViewModel.IsBookmarkPanelHovered = false;
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
    }
}
