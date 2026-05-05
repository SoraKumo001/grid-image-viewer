using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace grid_image_viewer
{
    public sealed partial class MainWindow : Window, IRecipient<FullscreenMessage>, IRecipient<PlaylistUpdatedMessage>, IRecipient<SlideshowNextRequestedMessage>
    {

        public IViewerStateService State { get; } = new ViewerStateService();
        public MainViewModel ViewModel { get; }

        // === Proxy Properties for State ===
        public ObservableCollection<string> Playlist { get => State.Playlist; set => State.Playlist = value; }
        public int CurrentIndex { get => State.CurrentIndex; set => State.CurrentIndex = value; }
        public string CurrentDirectory { get => State.CurrentDirectory; set => State.CurrentDirectory = value; }
        public bool IsGridMode { get => State.IsGridMode; set => State.IsGridMode = value; }

        // === Components and Services ===
        internal SettingsManager _settings;
        internal ISlideshowService SlideshowService { get; private set; }
        internal SlideshowManager SlideshowManager { get; private set; }
        internal ViewerCacheManager ViewerCacheManager { get; private set; }
        internal ViewerManager ViewerManager { get; private set; }
        internal GridManager GridManager { get; private set; }
        internal AppWindowManager AppWindowManager { get; private set; }
        internal EditorManager EditorManager { get; private set; }
        internal BookmarkManager BookmarkManager { get; private set; }
        internal PlaylistManager PlaylistManager { get; private set; }
        internal PrintService PrintService { get; private set; }
        internal InputHandler InputHandler { get; private set; }
        internal AnimationService AnimationService { get; private set; }
        internal DialogService DialogService { get; private set; }
        internal NotificationService NotificationService { get; private set; }
        internal MetadataDisplayService MetadataDisplayService { get; private set; }
        internal ImageEditService ImageEditService { get; private set; }

        // === UI Helpers ===
        public Controls.ViewerPanel ViewerControl => ViewerControlInternal;
        public Grid PagesGrid => ViewerControlInternal.CurrentBuffer;
        public IList<ImageItem> GridItems => (IList<ImageItem>?)GridManager?.GridItems ?? Array.Empty<ImageItem>();
        public GridView ImageGridView => GridControlInternal.GridView;
        public ScrollViewer ImageScrollViewer => ViewerControlInternal.ScrollViewer;

        public FrameworkElement PageGrid1 => ViewerControlInternal.PageControlsBuffer[ViewerControlInternal.CurrentBufferIndex][0];
        public FrameworkElement PageGrid2 => ViewerControlInternal.PageControlsBuffer[ViewerControlInternal.CurrentBufferIndex][1];
        public FrameworkElement PageGrid3 => ViewerControlInternal.PageControlsBuffer[ViewerControlInternal.CurrentBufferIndex][2];
        public FrameworkElement PageGrid4 => ViewerControlInternal.PageControlsBuffer[ViewerControlInternal.CurrentBufferIndex][3];

        private DispatcherTimer _resizeTimer;
        private bool _isDialogOpen;
        public bool IsDialogOpen { get => _isDialogOpen; set { _isDialogOpen = value; ViewModel.IsDialogOpen = value; } }
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
            this.InitializeComponent();
            ViewModel = new MainViewModel(State);
            RootGrid.DataContext = ViewModel;

            _settings = new SettingsManager();
            SlideshowService = new SlideshowService(State, _settings);
            SlideshowManager = new SlideshowManager(this, _settings, SlideshowService, State);

            ViewerCacheManager = new ViewerCacheManager(State, _settings);
            ViewerManager = new ViewerManager(this, _settings);
            GridManager = new GridManager(this, _settings);
            AppWindowManager = new AppWindowManager(this, _settings);
            EditorManager = new EditorManager(this, _settings);
            BookmarkManager = new BookmarkManager(this, _settings);
            PlaylistManager = new PlaylistManager(this, _settings);
            PrintService = new PrintService(this);

            AnimationService = new AnimationService(this, _settings);
            DialogService = new DialogService(this);
            NotificationService = new NotificationService(this);
            MetadataDisplayService = new MetadataDisplayService(this, _settings);
            ImageEditService = new ImageEditService(this);

            InputHandler = new InputHandler(this, _settings);

            _resizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _resizeTimer.Tick += (s, e) => { _resizeTimer.Stop(); _ = UpdateDisplayAsync(); };

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
            GridControlInternal.GridView.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(ImageGridView_PointerWheelChanged), true);

            WeakReferenceMessenger.Default.Register<FullscreenMessage>(this);
            WeakReferenceMessenger.Default.Register<PlaylistUpdatedMessage>(this);
            WeakReferenceMessenger.Default.Register<SlideshowNextRequestedMessage>(this);
        }


        public void Receive(FullscreenMessage message) => IsFullscreen = !IsFullscreen;

        public void Receive(PlaylistUpdatedMessage message)
        {
            UpdateGridItems(message.ForceFullGridUpdate);
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
                            NavigateFolder(1);
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

        internal void UpdateGridItems(bool forceFullUpdate) => GridManager.UpdateGridItems(forceFullUpdate);

        internal Task UpdateDisplayAsync() => ViewerManager.UpdateDisplayAsync();

        internal void UpdatePageIndicator() => ViewModel.UpdatePageIndicator();

        internal List<FrameworkElement> GetPageGrids() => new List<FrameworkElement> { PageGrid1, PageGrid2, PageGrid3, PageGrid4 };

        public Visibility BoolToVis(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
        public Visibility GetSearchingOverlayVisibility(bool isSearching, bool isSlideshowRunning)
            => (isSearching && !isSlideshowRunning) ? Visibility.Visible : Visibility.Collapsed;

        internal string CurrentImagePath => ViewModel.Playlist != null && ViewModel.CurrentIndex >= 0 && ViewModel.CurrentIndex < ViewModel.Playlist.Count ? ViewModel.Playlist[ViewModel.CurrentIndex] : string.Empty;
        internal void ShowNotification(string message) => ViewerManager.ShowNotification(message);
        internal void Navigate(int offset, bool forceSingleStep = false) => WeakReferenceMessenger.Default.Send(new NavigationMessage(offset, forceSingleStep));
        internal void NavigateFolder(int offset) => WeakReferenceMessenger.Default.Send(new FolderNavigationMessage(offset));

        private void RootGrid_PointerWheelChanged(object sender, PointerRoutedEventArgs e) => InputHandler.HandlePointerWheelChanged(sender, e);
        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Debounce resize handling to improve performance
            _resizeTimer.Stop();
            _resizeTimer.Start();
        }
        private void ImageGridView_PointerWheelChanged(object sender, PointerRoutedEventArgs e) => InputHandler.HandlePointerWheelChanged(sender, e);
        private void OverlayGrid_PointerMoved(object sender, PointerRoutedEventArgs e) => InputHandler.HandlePointerMoved(sender, e);
        private void RootGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => InputHandler.HandleDoubleTapped(sender, e);
        private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e) => InputHandler.HandleKeyDown(sender, e);

        private void EditMenuFlyout_Opening(object sender, object e) => EditorManager.EditMenuFlyout_Opening(sender, e);
        private void OverlayGrid_RightTapped(object sender, RightTappedRoutedEventArgs e) => EditorManager.UpdateTargetIndexAtPoint(e.GetPosition(PagesGrid));
        private void MenuSaveAs_Click(object sender, RoutedEventArgs e) => EditorManager.MenuSaveAs_Click(sender, e);
        private void MenuOverwrite_Click(object sender, RoutedEventArgs e) => EditorManager.MenuOverwrite_Click(sender, e);
        private void MenuCrop_Click(object sender, RoutedEventArgs e) => EditorManager.MenuCrop_Click(sender, e);
        private void MenuResize_Click(object sender, RoutedEventArgs e) => EditorManager.MenuResize_Click(sender, e);
        private void MenuRotate_Click(object sender, RoutedEventArgs e) => EditorManager.MenuRotate_Click(sender, e);
        private void MenuFlip_Click(object sender, RoutedEventArgs e) => EditorManager.MenuFlip_Click(sender, e);
        private void MenuTone_Click(object sender, RoutedEventArgs e) => EditorManager.MenuTone_Click(sender, e);
        private void MenuFilter_Click(object sender, RoutedEventArgs e) => EditorManager.MenuFilter_Click(sender, e);
        private void MenuOpenExplorer_Click(object sender, RoutedEventArgs e) => EditorManager.MenuOpenExplorer_Click(sender, e);
        private void MenuPrint_Click(object sender, RoutedEventArgs e) => EditorManager.MenuPrint_Click(sender, e);
        private void MenuViewMode_Click(object sender, RoutedEventArgs e) => EditorManager.MenuViewMode_Click(sender, e);
        private void MenuLayoutMode_Click(object sender, RoutedEventArgs e) => EditorManager.MenuLayoutMode_Click(sender, e);
        private void MenuStretchMode_Click(object sender, RoutedEventArgs e) => EditorManager.MenuStretchMode_Click(sender, e);
        private void MenuKeyBindings_Click(object sender, RoutedEventArgs e) => EditorManager.MenuKeyBindings_Click(sender, e);
        private void MenuSettings_Click(object sender, RoutedEventArgs e) => EditorManager.MenuSettings_Click(sender, e);
        private void MenuSupport_Click(object sender, RoutedEventArgs e) => EditorManager.MenuSupport_Click(sender, e);
        private void MenuUndo_Click(object sender, RoutedEventArgs e) => EditorManager.MenuUndo_Click(sender, e);
        private void MenuRedo_Click(object sender, RoutedEventArgs e) => EditorManager.MenuRedo_Click(sender, e);
        private void MenuMetadata_Click(object sender, RoutedEventArgs e) => EditorManager.MenuMetadata_Click(sender, e);
        private void MenuPageIndicatorToggle_Click(object sender, RoutedEventArgs e) => EditorManager.MenuPageIndicatorToggle_Click(sender, e);
        private void MenuSlideshow_Click(object sender, RoutedEventArgs e) => SlideshowManager.OpenSlideshowDialogAsync();
        private void MenuBookmarksToggle_Click(object sender, RoutedEventArgs e) => BookmarkManager.MenuBookmarksToggle_Click(sender, e);
        private void MenuBookmark_Click(object sender, RoutedEventArgs e) => BookmarkManager.MenuBookmark_Click(sender, e);
        private void MenuBookmarkPanel_Close_Click(object sender, RoutedEventArgs e) => BookmarkPanel.Visibility = Visibility.Collapsed;
        private void BookmarkListView_ItemClick(object sender, ItemClickEventArgs e) => BookmarkManager.BookmarkListView_ItemClick(sender, e);
        private void MenuBookmarkRemove_Click(object sender, RoutedEventArgs e) => BookmarkManager.MenuBookmarkRemove_Click(sender, e);
        private void BookmarkListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) => BookmarkManager.BookmarkListView_DragItemsCompleted(sender, args);

        private void OpenSlideshowDialogAsync() => SlideshowManager.OpenSlideshowDialogAsync();
        private void SlideshowDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args) => SlideshowManager.SlideshowDialog_Opened(sender, args);
        private void SlideshowDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args) => SlideshowManager.SlideshowDialog_PrimaryButtonClick(sender, args);
    }
}
