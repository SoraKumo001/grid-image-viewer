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
    public sealed partial class MainWindow : Window, IMainView, IRecipient<FullscreenMessage>, IRecipient<PlaylistUpdatedMessage>, IRecipient<SlideshowNextRequestedMessage>
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
        public IEditorManager EditorManager { get; private set; }
        public IBookmarkManager BookmarkManager { get; private set; }
        public IPlaylistManager PlaylistManager { get; private set; }
        public IPrintService PrintService { get; private set; }
        public IAnimationService AnimationService { get; private set; }
        public IDialogService DialogService { get; private set; }
        public INotificationService NotificationService { get; private set; }
        public IMetadataDisplayService MetadataDisplayService { get; private set; }
        public IImageEditService ImageEditService { get; private set; }
        public IInputHandler InputHandler { get; private set; }

        IMetadataDisplayService IMainView.MetadataDisplayService => MetadataDisplayService;
        IImageEditService IMainView.ImageEditService => ImageEditService;
        IDialogService IMainView.DialogService => DialogService;
        IAnimationService IMainView.AnimationService => AnimationService;
        INotificationService IMainView.NotificationService => NotificationService;
        IPlaylistManager IMainView.PlaylistManager => PlaylistManager;
        IViewerManager IMainView.ViewerManager => ViewerManager;
        IGridManager IMainView.GridManager => GridManager;
        IEditorManager IMainView.EditorManager => EditorManager;
        ListView IMainView.BookmarkListView => BookmarkListView;
        IPrintService IMainView.PrintService => PrintService;
        IBookmarkManager IMainView.BookmarkManager => BookmarkManager;
        IAppWindowManager IMainView.AppWindowManager => AppWindowManager;
        System.Collections.Generic.List<FrameworkElement> IMainView.GetPageGrids() => GetPageGrids();
        FrameworkElement IMainView.PageGrid1 => PageGrid1;
        FrameworkElement IMainView.PageGrid2 => PageGrid2;
        FrameworkElement IMainView.PageGrid3 => PageGrid3;
        FrameworkElement IMainView.PageGrid4 => PageGrid4;

        UIElement IMainView.BookmarkPanel => BookmarkPanel;
        MenuFlyoutItem IMainView.MenuUndo => MenuUndo;
        MenuFlyoutItem IMainView.MenuRedo => MenuRedo;
        MenuFlyoutSubItem IMainView.MenuSaveAs => MenuSaveAs;
        MenuFlyoutItem IMainView.MenuOverwrite => MenuOverwrite;
        MenuFlyoutItem IMainView.MenuCrop => MenuCrop;
        MenuFlyoutItem IMainView.MenuResize => MenuResize;
        MenuFlyoutSubItem IMainView.MenuRotate => MenuRotate;
        MenuFlyoutSubItem IMainView.MenuFlip => MenuFlip;
        MenuFlyoutItem IMainView.MenuTone => MenuTone;
        MenuFlyoutSubItem IMainView.MenuFilter => MenuFilter;
        MenuFlyoutItem IMainView.MenuPrint => MenuPrint;

        ToggleMenuFlyoutItem IMainView.MenuViewSingle => MenuViewSingle;
        ToggleMenuFlyoutItem IMainView.MenuViewDouble => MenuViewDouble;
        ToggleMenuFlyoutItem IMainView.MenuViewQuad => MenuViewQuad;

        ToggleMenuFlyoutItem IMainView.MenuLayoutAuto => MenuLayoutAuto;
        ToggleMenuFlyoutItem IMainView.MenuLayoutHorz => MenuLayoutHorz;
        ToggleMenuFlyoutItem IMainView.MenuLayoutGrid => MenuLayoutGrid;

        ToggleMenuFlyoutItem IMainView.MenuStretchOriginal => MenuStretchOriginal;
        ToggleMenuFlyoutItem IMainView.MenuStretchContain => MenuStretchContain;
        ToggleMenuFlyoutItem IMainView.MenuStretchCover => MenuStretchCover;

        ToggleMenuFlyoutItem IMainView.MenuMetadata => MenuMetadata;
        ToggleMenuFlyoutItem IMainView.MenuPageIndicatorToggle => MenuPageIndicatorToggle;

        MenuFlyoutItem IMainView.MenuBookmark => MenuBookmark;
        ToggleMenuFlyoutItem IMainView.MenuBookmarksToggle => MenuBookmarksToggle;
        MenuFlyoutSubItem IMainView.MenuBookmarkList => MenuBookmarkList;

        ContentDialog IMainView.SlideshowDialog => SlideshowDialog;
        CheckBox IMainView.SlideshowFullscreen => SlideshowFullscreen;
        CheckBox IMainView.SlideshowRandom => SlideshowRandom;
        CheckBox IMainView.SlideshowLoop => SlideshowLoop;
        CheckBox IMainView.SlideshowNextFolder => SlideshowNextFolder;
        CheckBox IMainView.SlideshowIncludeSiblings => SlideshowIncludeSiblings;
        CheckBox IMainView.SlideshowCurrentFolderOnly => SlideshowCurrentFolderOnly;
        CheckBox IMainView.SlideshowUniformToFill => SlideshowUniformToFill;
        CheckBox IMainView.SlideshowCrossfade => SlideshowCrossfade;
        NumberBox IMainView.SlideshowInterval => SlideshowInterval;
        NumberBox IMainView.SlideshowCrossfadeDuration => SlideshowCrossfadeDuration;
        UIElement IMainView.Content => (UIElement)this.Content;
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
                        ViewerManager.Pages[pi].EditedBitmap = null;
                }
            }
        }
        void IMainView.LoadDirectory(string path, string initialFile, bool includeSiblings, bool includeSubfolders, List<string>? preloadedPlaylist)
            => LoadDirectory(path, initialFile, includeSiblings, includeSubfolders, preloadedPlaylist);
        void IMainView.Close() => Close();
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
        TextBlock IMainView.NotificationText => NotificationText;
        UIElement IMainView.MetadataPanel => MetadataPanel;
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
        Grid IMainView.PagesGrid => ViewerControlInternal.CurrentBuffer;
        Grid IMainView.RootGrid => RootGrid;
        bool IMainView.IsDialogOpen { get => IsDialogOpen; set => IsDialogOpen = value; }
        IntPtr IMainView.WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(this);
        bool IMainView.ExtendsContentIntoTitleBar { get => ExtendsContentIntoTitleBar; set => ExtendsContentIntoTitleBar = value; }
        void IMainView.SetTitleBar(UIElement tb) => SetTitleBar(tb);
        UIElement IMainView.AppTitleBar => AppTitleBar;
        string IMainView.CurrentImagePath => CurrentImagePath;

        // === UI Helpers ===
        public ViewerPanel ViewerControl => ViewerControlInternal;
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
            var services = ((App)Application.Current).Services;
            ((App)Application.Current).SetMainView(this);

            State = services.GetRequiredService<IViewerStateService>();
            _settings = services.GetRequiredService<ISettingsManager>();
            ViewModel = services.GetRequiredService<MainViewModel>();
            RootGrid.DataContext = ViewModel;

            SlideshowService = services.GetRequiredService<ISlideshowService>();
            SlideshowManager = services.GetRequiredService<ISlideshowManager>();

            ViewerCacheManager = services.GetRequiredService<IViewerCacheManager>();
            ViewerManager = services.GetRequiredService<IViewerManager>();
            GridManager = services.GetRequiredService<IGridManager>();
            AppWindowManager = services.GetRequiredService<IAppWindowManager>();
            EditorManager = services.GetRequiredService<IEditorManager>();
            BookmarkManager = services.GetRequiredService<IBookmarkManager>();
            PlaylistManager = services.GetRequiredService<IPlaylistManager>();
            this.PrintService = services.GetRequiredService<IPrintService>();

            this.AnimationService = services.GetRequiredService<IAnimationService>();
            this.DialogService = services.GetRequiredService<IDialogService>();
            this.NotificationService = services.GetRequiredService<INotificationService>();
            this.MetadataDisplayService = services.GetRequiredService<IMetadataDisplayService>();
            ImageEditService = services.GetRequiredService<IImageEditService>();

            InputHandler = services.GetRequiredService<IInputHandler>();

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

        public void UpdateGridItems(bool forceFullUpdate) => GridManager.UpdateGridItems(forceFullUpdate);

        public Task UpdateDisplayAsync() => ViewerManager.UpdateDisplayAsync();

        public void UpdatePageIndicator() => ViewModel.UpdatePageIndicator();

        public List<FrameworkElement> GetPageGrids() => new List<FrameworkElement> { PageGrid1, PageGrid2, PageGrid3, PageGrid4 };

        public Visibility BoolToVis(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
        public Visibility GetSearchingOverlayVisibility(bool isSearching, bool isSlideshowRunning)
            => (isSearching && !isSlideshowRunning) ? Visibility.Visible : Visibility.Collapsed;

        public string CurrentImagePath => ViewModel.Playlist != null && ViewModel.CurrentIndex >= 0 && ViewModel.CurrentIndex < ViewModel.Playlist.Count ? ViewModel.Playlist[ViewModel.CurrentIndex] : string.Empty;
        public void ShowNotification(string message) => ViewerManager.ShowNotification(message);
        public void Navigate(int offset, bool forceSingleStep = false) => WeakReferenceMessenger.Default.Send(new NavigationMessage(offset, forceSingleStep));
        public void NavigateFolder(int offset) => WeakReferenceMessenger.Default.Send(new FolderNavigationMessage(offset));

        void IMainView.Navigate(int direction, bool forceSingleStep) => Navigate(direction, forceSingleStep);
        void IMainView.NavigateFolder(int direction) => NavigateFolder(direction);


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
