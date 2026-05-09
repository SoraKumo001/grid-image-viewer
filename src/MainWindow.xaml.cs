using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Managers;
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
        CheckBox IMainView.SlideshowUniformToFill => SlideshowUniformToFill;
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
                        ViewerManager.Pages[pi].EditedBitmap = null;
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
        public IList<ImageItem> GridItems => GridManager?.GridItems ?? Array.Empty<ImageItem>();
        public GridView ImageGridView => GridControlInternal.GridView;
        public ScrollViewer ImageScrollViewer => ViewerControlInternal.ScrollViewer;

        public FrameworkElement PageGrid1 => ViewerControlInternal.PageControlsBuffer[ViewerControlInternal.CurrentBufferIndex][0];
        public FrameworkElement PageGrid2 => ViewerControlInternal.PageControlsBuffer[ViewerControlInternal.CurrentBufferIndex][1];
        public FrameworkElement PageGrid3 => ViewerControlInternal.PageControlsBuffer[ViewerControlInternal.CurrentBufferIndex][2];
        public FrameworkElement PageGrid4 => ViewerControlInternal.PageControlsBuffer[ViewerControlInternal.CurrentBufferIndex][3];

        private readonly DispatcherTimer _resizeTimer;
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

            RootGrid.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(RootGrid_PointerMoved), true);
            RootGrid.AddHandler(UIElement.PointerEnteredEvent, new PointerEventHandler(RootGrid_PointerMoved), true);
            RootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(RootGrid_KeyDown), true);

            WeakReferenceMessenger.Default.Register<FullscreenMessage>(this);
            WeakReferenceMessenger.Default.Register<PlaylistUpdatedMessage>(this);
            WeakReferenceMessenger.Default.Register<SlideshowNextRequestedMessage>(this);
            WeakReferenceMessenger.Default.Register<ToggleGridMessage>(this);
            WeakReferenceMessenger.Default.Register<ToggleMangaMessage>(this);
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
            ViewerManager.UpdateStretch();

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
            if (string.IsNullOrEmpty(path) || ArchiveManager.IsArchivePath(path)) return;

            var dialog = new ContentDialog
            {
                Title = _settings.GetString("DeleteDialog_Title"),
                Content = string.Format(_settings.GetString("DeleteDialog_Content"), System.IO.Path.GetFileName(path)),
                PrimaryButtonText = _settings.GetString("DeleteDialog_Primary"),
                CloseButtonText = _settings.GetString("DeleteDialog_Close"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.Content?.XamlRoot
            };

            IsDialogOpen = true;
            var result = await dialog.ShowAsync();
            IsDialogOpen = false;

            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.Delete(path);
                        PlaylistManager.RemoveFromPlaylist(path);
                        ShowNotification(_settings.GetString("Notification_FileDeleted"));
                    }
                }
                catch (Exception ex)
                {
                    ShowNotification("Error: " + ex.Message);
                }
            }
        }

        public void Receive(RenameFileMessage message)
        {
            _ = HandleRenameFileAsync(message.Path);
        }

        private async Task HandleRenameFileAsync(string path)
        {
            if (string.IsNullOrEmpty(path) || ArchiveManager.IsArchivePath(path)) return;

            var textBox = new TextBox
            {
                Text = System.IO.Path.GetFileNameWithoutExtension(path),
                Header = _settings.GetString("RenameDialog_Label"),
                SelectionStart = 0,
                SelectionLength = System.IO.Path.GetFileNameWithoutExtension(path).Length
            };

            var dialog = new ContentDialog
            {
                Title = _settings.GetString("RenameDialog_Title"),
                Content = textBox,
                PrimaryButtonText = _settings.GetString("RenameDialog_PrimaryButton"),
                CloseButtonText = _settings.GetString("RenameDialog_CloseButton"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content?.XamlRoot
            };

            IsDialogOpen = true;
            var result = await dialog.ShowAsync();
            IsDialogOpen = false;

            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                try
                {
                    string oldName = System.IO.Path.GetFileName(path);
                    string newName = textBox.Text + System.IO.Path.GetExtension(path);
                    if (oldName == newName) return;

                    string directory = System.IO.Path.GetDirectoryName(path)!;
                    string newPath = System.IO.Path.Combine(directory, newName);

                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.Move(path, newPath);
                        ImageEditService.RenameSession(path, newPath);
                        ViewerManager.ReplacePath(path, newPath);
                        PlaylistManager.ReplaceInPlaylist(path, newPath);
                        ShowNotification(_settings.GetString("Notification_Renamed"));
                    }
                }
                catch (Exception ex)
                {
                    ShowNotification("Error: " + ex.Message);
                }
            }
        }

        public void Receive(MoveFileMessage message)
        {
            _ = HandleMoveFileAsync(message.Path);
        }

        private async Task HandleMoveFileAsync(string path)
        {
            if (string.IsNullOrEmpty(path) || ArchiveManager.IsArchivePath(path)) return;

            var picker = new Windows.Storage.Pickers.FolderPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add("*");
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                try
                {
                    string fileName = System.IO.Path.GetFileName(path);
                    string destPath = System.IO.Path.Combine(folder.Path, fileName);

                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.Move(path, destPath);
                        PlaylistManager.RemoveFromPlaylist(path);
                        ShowNotification(_settings.GetString("Notification_Moved"));
                    }
                }
                catch (Exception ex)
                {
                    ShowNotification("Error: " + ex.Message);
                }
            }
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
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
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
            MenuBookmarkList.Items.Clear();
            if (_settings.Bookmarks.Count == 0)
            {
                MenuBookmarkList.Items.Add(new MenuFlyoutItem { Text = _settings.GetString("Bookmark_Empty"), IsEnabled = false });
            }
            else
            {
                foreach (var bm in _settings.Bookmarks)
                {
                    var bmItem = new MenuFlyoutItem { Text = bm.Name, Tag = bm.Path };
                    bmItem.Click += (s, e) => LoadDirectory(bm.Path);
                    MenuBookmarkList.Items.Add(bmItem);
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
            // Debounce resize handling to improve performance
            _resizeTimer.Stop();
            _resizeTimer.Start();
        }

        private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e) => InputHandler.HandlePointerMoved(sender, e);
        private void RootGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => InputHandler.HandleDoubleTapped(sender, e);
        private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e) => InputHandler.HandleKeyDown(sender, e);

        private void EditMenuFlyout_Opening(object sender, object e) => MenuStateManager.EditMenuFlyout_Opening(sender, e);
        private void RootGrid_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var point = e.GetPosition(PagesGrid);
            MenuStateManager.UpdateTargetIndexAtPoint(point);
            ViewModel.Editor.ContextPath = MenuStateManager.ContextTargetPath;
        }






        private void MenuBookmarksToggle_Click(object sender, RoutedEventArgs e) => BookmarkManager.MenuBookmarksToggle_Click(sender, e);
        private void MenuMetadata_Click(object sender, RoutedEventArgs e) => MenuStateManager.MenuMetadata_Click(sender, e);
        private void MenuPageIndicatorToggle_Click(object sender, RoutedEventArgs e) => MenuStateManager.MenuPageIndicatorToggle_Click(sender, e);
        private void MenuViewMode_Click(object sender, RoutedEventArgs e) => MenuStateManager.MenuViewMode_Click(sender, e);
        private void MenuLayoutMode_Click(object sender, RoutedEventArgs e) => MenuStateManager.MenuLayoutMode_Click(sender, e);
        private void MenuStretchMode_Click(object sender, RoutedEventArgs e) => MenuStateManager.MenuStretchMode_Click(sender, e);
        private void BookmarkListView_ItemClick(object sender, ItemClickEventArgs e) => BookmarkManager.BookmarkListView_ItemClick(sender, e);
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
    }
}
