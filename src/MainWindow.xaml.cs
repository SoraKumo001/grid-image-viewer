using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AppControls = grid_image_viewer.Controls;

namespace grid_image_viewer
{
    public sealed partial class MainWindow : Window, IRecipient<FullscreenMessage>, IRecipient<PlaylistUpdatedMessage>
    {
        public MainViewModel ViewModel { get; } = new MainViewModel();

        // === Components and Services ===
        internal SlideshowManager SlideshowManager { get; private set; }
        internal InputHandler InputHandler { get; private set; }
        internal GridManager GridManager { get; private set; }
        internal ViewerManager ViewerManager { get; private set; }
        internal EditorManager EditorManager { get; private set; }
        internal ImageEditService ImageEditService { get; private set; }
        internal MetadataDisplayService MetadataDisplayService { get; private set; }
        internal PrintService PrintService { get; private set; }
        internal NotificationService NotificationService { get; private set; }
        internal AnimationService AnimationService { get; private set; }
        internal DialogService DialogService { get; private set; }

        internal AppControls.ViewerPanel ViewerControl => ViewerControlInternal;
        internal AppControls.GridImagePanel GridControl => GridControlInternal;

        // Backward compatibility properties - Pointing to active buffer
        internal ScrollViewer ImageScrollViewer => ViewerControlInternal.ScrollViewer;
        internal Grid PagesGrid => ViewerControlInternal.CurrentBuffer;
        internal GridView ImageGridView => GridControlInternal.GridView;

        internal Grid PageGrid1 => ViewerControlInternal.PageGridsBuffer[ViewerControlInternal.CurrentBufferIndex][0];
        internal Grid PageGrid2 => ViewerControlInternal.PageGridsBuffer[ViewerControlInternal.CurrentBufferIndex][1];
        internal Grid PageGrid3 => ViewerControlInternal.PageGridsBuffer[ViewerControlInternal.CurrentBufferIndex][2];
        internal Grid PageGrid4 => ViewerControlInternal.PageGridsBuffer[ViewerControlInternal.CurrentBufferIndex][3];

        internal Grid CurrentContainer1 => ViewerControlInternal.PageGridsBuffer[ViewerControlInternal.CurrentBufferIndex][0];
        internal Grid CurrentContainer2 => ViewerControlInternal.PageGridsBuffer[ViewerControlInternal.CurrentBufferIndex][1];
        internal Grid CurrentContainer3 => ViewerControlInternal.PageGridsBuffer[ViewerControlInternal.CurrentBufferIndex][2];
        internal Grid CurrentContainer4 => ViewerControlInternal.PageGridsBuffer[ViewerControlInternal.CurrentBufferIndex][3];

        internal Image Image1 => ViewerControlInternal.PageImagesBuffer[ViewerControlInternal.CurrentBufferIndex][0];
        internal Image Image2 => ViewerControlInternal.PageImagesBuffer[ViewerControlInternal.CurrentBufferIndex][1];
        internal Image Image3 => ViewerControlInternal.PageImagesBuffer[ViewerControlInternal.CurrentBufferIndex][2];
        internal Image Image4 => ViewerControlInternal.PageImagesBuffer[ViewerControlInternal.CurrentBufferIndex][3];

        internal SkiaSharp.Views.Windows.SKXamlCanvas Canvas1 => ViewerControlInternal.PageCanvasesBuffer[ViewerControlInternal.CurrentBufferIndex][0];
        internal SkiaSharp.Views.Windows.SKXamlCanvas Canvas2 => ViewerControlInternal.PageCanvasesBuffer[ViewerControlInternal.CurrentBufferIndex][1];
        internal SkiaSharp.Views.Windows.SKXamlCanvas Canvas3 => ViewerControlInternal.PageCanvasesBuffer[ViewerControlInternal.CurrentBufferIndex][2];
        internal SkiaSharp.Views.Windows.SKXamlCanvas Canvas4 => ViewerControlInternal.PageCanvasesBuffer[ViewerControlInternal.CurrentBufferIndex][3];

        internal ProgressRing LoadingRing1 => ViewerControlInternal.PageLoadingRingsBuffer[ViewerControlInternal.CurrentBufferIndex][0];
        internal ProgressRing LoadingRing2 => ViewerControlInternal.PageLoadingRingsBuffer[ViewerControlInternal.CurrentBufferIndex][1];
        internal ProgressRing LoadingRing3 => ViewerControlInternal.PageLoadingRingsBuffer[ViewerControlInternal.CurrentBufferIndex][2];
        internal ProgressRing LoadingRing4 => ViewerControlInternal.PageLoadingRingsBuffer[ViewerControlInternal.CurrentBufferIndex][3];

        internal Border FocusBorder1 => ViewerControlInternal.FocusBordersBuffer[ViewerControlInternal.CurrentBufferIndex][0];
        internal Border FocusBorder2 => ViewerControlInternal.FocusBordersBuffer[ViewerControlInternal.CurrentBufferIndex][1];
        internal Border FocusBorder3 => ViewerControlInternal.FocusBordersBuffer[ViewerControlInternal.CurrentBufferIndex][2];
        internal Border FocusBorder4 => ViewerControlInternal.FocusBordersBuffer[ViewerControlInternal.CurrentBufferIndex][3];

        // Layout definitions - These should be handled per buffer in ViewerManager
        internal ColumnDefinition Col0 => ViewerControlInternal.ColsBuffer[ViewerControlInternal.CurrentBufferIndex][0];
        internal ColumnDefinition Col1 => ViewerControlInternal.ColsBuffer[ViewerControlInternal.CurrentBufferIndex][1];
        internal ColumnDefinition Col2 => ViewerControlInternal.ColsBuffer[ViewerControlInternal.CurrentBufferIndex][2];
        internal ColumnDefinition Col3 => ViewerControlInternal.ColsBuffer[ViewerControlInternal.CurrentBufferIndex][3];

        internal RowDefinition Row0 => ViewerControlInternal.RowsBuffer[ViewerControlInternal.CurrentBufferIndex][0];
        internal RowDefinition Row1 => ViewerControlInternal.RowsBuffer[ViewerControlInternal.CurrentBufferIndex][1];

        internal bool IsGridMode { get => ViewModel.IsGridMode; set => ViewModel.IsGridMode = value; }
        internal ObservableCollection<ImageItem> GridItems => _gridItems;
        internal bool IsDialogOpen { get => ViewModel.IsDialogOpen; set => ViewModel.IsDialogOpen = value; }
        internal IList<string> Playlist => ViewModel.Playlist;
        internal int CurrentIndex { get => ViewModel.CurrentIndex; set => ViewModel.CurrentIndex = value; }
        internal string CurrentDirectory { get => ViewModel.CurrentDirectory; set => ViewModel.CurrentDirectory = value; }
        internal bool IsSearchingFolder { get => ViewModel.IsSearchingFolder; set => ViewModel.IsSearchingFolder = value; }

        internal bool IsFullscreen
        {
            get => AppWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen;
            set
            {
                if (value)
                {
                    AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
                    AppTitleBar.Visibility = Visibility.Collapsed;
                }
                else
                {
                    AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
                    AppTitleBar.Visibility = Visibility.Visible;
                }
            }
        }

        private SettingsManager _settings;
        private ObservableCollection<ImageItem> _gridItems = new ObservableCollection<ImageItem>();
        private Random _random = new Random();

        public MainWindow()
        {
            this.InitializeComponent();
            _settings = new SettingsManager();

            // Initialize Services
            ImageEditService = new ImageEditService(this);
            MetadataDisplayService = new MetadataDisplayService(this, _settings);
            NotificationService = new NotificationService(this);
            AnimationService = new AnimationService(this, _settings);
            DialogService = new DialogService(this);
            PrintService = new PrintService(this);

            // Initialize Managers
            PlaylistManager = new PlaylistManager(this, _settings);
            ViewerManager = new ViewerManager(this, _settings);
            GridManager = new GridManager(this, _settings);
            EditorManager = new EditorManager(this, _settings);
            SlideshowManager = new SlideshowManager(this, _settings);
            InputHandler = new InputHandler(this, _settings);

            // Setup Controls
            ViewerControlInternal.PaintSurfaceRequested += (s, e) => ViewerManager.PaintCanvas(e.bufferIndex, e.pageIndex, e.args);
            GridControlInternal.GridView.ItemClick += ImageGridView_ItemClick;
            GridControlInternal.GridView.SizeChanged += ImageGridView_SizeChanged;

            // Sync ViewModel with Settings
            ViewModel.MangaSplitCount = _settings.MangaSplitCount;
            ViewModel.ShowPageIndicator = _settings.ShowPageIndicator;

            _gridItems = new ObservableCollection<ImageItem>();
            ImageGridView.ItemsSource = _gridItems;

            this.Closed += MainWindow_Closed;

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            ApplyBackgroundSettings();

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            _settings.LoadWindowState(appWindow);
            appWindow.Changed += (s, e) =>
            {
                if (e.DidPositionChange || e.DidSizeChange)
                {
                    _settings.UpdateNormalWindowState(appWindow);
                }
            };
            try
            {
                var iconPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "AppIcon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    appWindow.SetIcon(iconPath);
                }
                else
                {
                    var fallbackPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "..", "Assets", "AppIcon.ico");
                    if (!System.IO.File.Exists(fallbackPath))
                    {
                        fallbackPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Assets", "AppIcon.ico");
                    }
                    if (System.IO.File.Exists(fallbackPath)) appWindow.SetIcon(fallbackPath);
                }
            }
            catch { }

            if (Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = appWindow.TitleBar;
                titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 255, 255, 255);
                titleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(51, 255, 255, 255);
                titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 128, 128, 128);
                titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            }

            ImageGridView.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(ImageGridView_PointerWheelChanged), true);

            WeakReferenceMessenger.Default.Register<FullscreenMessage>(this);
            WeakReferenceMessenger.Default.Register<PlaylistUpdatedMessage>(this);
        }

        public void Receive(FullscreenMessage message) => IsFullscreen = !IsFullscreen;

        public void Receive(PlaylistUpdatedMessage message)
        {
            UpdateGridItems(message.ForceFullGridUpdate);
            _ = UpdateDisplayAsync();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            PrintService.UnregisterForPrinting();
            ViewerManager.Dispose();
            GridManager.Dispose();
            SlideshowManager.Dispose();
            foreach (var item in _gridItems) item.DisposeCodec();

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            _settings.SaveWindowState(appWindow, CurrentImagePath, CurrentDirectory);
        }

        public void ApplyBackgroundSettings()
        {
            int mode = _settings.BackgroundColorMode;
            if (mode == 1) RootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));
            else if (mode == 2) RootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
            else RootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        }

        private void ImageGridView_ItemClick(object sender, ItemClickEventArgs e) => GridManager.ImageGridView_ItemClick(sender, e);
        private void ImageGridView_SizeChanged(object sender, SizeChangedEventArgs e) => GridManager.ImageGridView_SizeChanged(sender, e);
        private void MenuThumbnailRefresh_Click(object sender, RoutedEventArgs e) => GridManager.RefreshThumbnails();

        private void RootGrid_DragOver(object sender, DragEventArgs e) => InputHandler.HandleDragOver(sender, e);
        private void RootGrid_Drop(object sender, DragEventArgs e) => InputHandler.HandleDrop(sender, e);
        internal PlaylistManager PlaylistManager { get; private set; }

        public void LoadDirectory(string path, string initialFile = "", bool includeSiblings = false, bool includeSubfolders = false, List<string>? preloadedPlaylist = null)
        {
            PlaylistManager.LoadDirectory(path, initialFile, includeSiblings, includeSubfolders, preloadedPlaylist);
        }

        internal void UpdateGridItems(bool forceFullUpdate)
        {
            if (!IsGridMode && !forceFullUpdate && ViewModel.Playlist.Count > 500)
            {
                _gridItems.Clear();
                if (ViewModel.CurrentIndex >= 0 && ViewModel.CurrentIndex < ViewModel.Playlist.Count)
                {
                    _gridItems.Add(new ImageItem { FilePath = ViewModel.Playlist[ViewModel.CurrentIndex], IsLoading = true });
                }
                return;
            }

            if (_gridItems.Count == ViewModel.Playlist.Count) return;

            var newList = new ObservableCollection<ImageItem>();
            foreach (var f in ViewModel.Playlist) newList.Add(new ImageItem { FilePath = f, IsLoading = true });

            _gridItems = newList;
            ImageGridView.ItemsSource = _gridItems;
            GridManager.RefreshThumbnails();
        }

        internal Task UpdateDisplayAsync() => ViewerManager.UpdateDisplayAsync();

        internal void UpdatePageIndicator() => ViewModel.UpdatePageIndicator();

        internal List<Grid> GetPageGrids() => new List<Grid> { PageGrid1, PageGrid2, PageGrid3, PageGrid4 };

        public Visibility BoolToVis(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

        internal string CurrentImagePath => ViewModel.Playlist != null && ViewModel.CurrentIndex >= 0 && ViewModel.CurrentIndex < ViewModel.Playlist.Count ? ViewModel.Playlist[ViewModel.CurrentIndex] : string.Empty;
        internal void ShowNotification(string message) => ViewerManager.ShowNotification(message);
        internal void Navigate(int offset, bool forceSingleStep = false) => WeakReferenceMessenger.Default.Send(new NavigationMessage(offset, forceSingleStep));
        internal void NavigateFolder(int offset) => WeakReferenceMessenger.Default.Send(new FolderNavigationMessage(offset));

        private void RootGrid_PointerWheelChanged(object sender, PointerRoutedEventArgs e) => InputHandler.HandlePointerWheelChanged(sender, e);
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
        private void MenuBookmarksToggle_Click(object sender, RoutedEventArgs e) => EditorManager.MenuBookmarksToggle_Click(sender, e);
        private void MenuBookmark_Click(object sender, RoutedEventArgs e) => EditorManager.MenuBookmark_Click(sender, e);
        private void MenuBookmarkPanel_Close_Click(object sender, RoutedEventArgs e) => BookmarkPanel.Visibility = Visibility.Collapsed;
        private void BookmarkListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is BookmarkItem item)
            {
                LoadDirectory(item.Path);
                BookmarkPanel.Visibility = Visibility.Collapsed;
            }
        }
        private void MenuBookmarkRemove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path)
            {
                _settings.ToggleBookmark(path, true);
                EditorManager.UpdateBookmarkList();
            }
        }
        private void BookmarkListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            _settings.SaveSettings();
            EditorManager.UpdateMenuStates();
        }

        private void OpenSlideshowDialogAsync() => SlideshowManager.OpenSlideshowDialogAsync();
        private void SlideshowDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args) => SlideshowManager.SlideshowDialog_Opened(sender, args);
        private void SlideshowDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args) => SlideshowManager.SlideshowDialog_PrimaryButtonClick(sender, args);
    }
}
