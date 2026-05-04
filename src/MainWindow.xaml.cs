using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SkiaSharp.Views.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace grid_image_viewer
{
    public sealed partial class MainWindow : Window
    {
        private List<string> _playlist = new List<string>();

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

        internal bool IsGridMode { get => _isGridMode; set => _isGridMode = value; }
        internal ObservableCollection<ImageItem> GridItems => _gridItems;
        internal bool IsDialogOpen { get => _isDialogOpen; set => _isDialogOpen = value; }
        internal List<string> Playlist => _playlist;
        internal int CurrentIndex { get => _currentIndex; set => _currentIndex = value; }
        internal string CurrentDirectory { get => _currentDirectory; set => _currentDirectory = value; }
        internal bool IsSearchingFolder { get => _isSearchingFolder; set => _isSearchingFolder = value; }

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

        private int _currentIndex = -1;
        private string _currentDirectory = string.Empty;

        public static readonly string[] SupportedExtensions =
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".avif", ".avis", ".heic", ".heif", ".jxl", ".tif", ".tiff", ".svg", ".psd", ".ico",
            ".dng", ".nef", ".cr2", ".arw", ".tga", ".pcx"
        };

        public static bool IsSupportedExtension(string extension)
        {
            string ext = extension.ToLowerInvariant();
            return SupportedExtensions.Contains(ext) || ArchiveManager.ArchiveExtensions.Contains(ext);
        }
        private SettingsManager _settings;
        private ObservableCollection<ImageItem> _gridItems = new ObservableCollection<ImageItem>();
        private bool _isGridMode = false;
        private bool _isDialogOpen = false;
        private Random _random = new Random();
        private bool _isSearchingFolder = false;

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
            ViewerManager = new ViewerManager(this, _settings);
            GridManager = new GridManager(this, _settings);
            EditorManager = new EditorManager(this, _settings);
            SlideshowManager = new SlideshowManager(this, _settings);
            InputHandler = new InputHandler(this, _settings);

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
                    // Fallback to project root if running from source/debug differently
                    var fallbackPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "..", "Assets", "AppIcon.ico");
                    if (!System.IO.File.Exists(fallbackPath))
                    {
                        // One more level up if needed (depends on bin depth)
                        fallbackPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Assets", "AppIcon.ico");
                    }
                    if (System.IO.File.Exists(fallbackPath)) appWindow.SetIcon(fallbackPath);
                }
            }
            catch { /* Ignore icon errors to prevent crash */ }

            if (Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = appWindow.TitleBar;

                // Set active window colors
                titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 255, 255, 255);
                titleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(51, 255, 255, 255);

                // Set inactive window colors
                titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 128, 128, 128);
                titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            }

            ImageGridView.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(ImageGridView_PointerWheelChanged), true);
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            PrintService.UnregisterForPrinting();
            ViewerManager.Dispose();
            GridManager.Dispose();
            foreach (var item in _gridItems) item.DisposeCodec();

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            _settings.SaveWindowState(appWindow, CurrentImagePath);
        }

        public void ApplyBackgroundSettings()
        {
            int mode = _settings.BackgroundColorMode;
            if (mode == 1) // Black
            {
                RootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));
            }
            else if (mode == 2) // White
            {
                RootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
            }
            else // System (Mica)
            {
                RootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            }
        }

        private void ImageGridView_ItemClick(object sender, ItemClickEventArgs e) => GridManager.ImageGridView_ItemClick(sender, e);
        private void ImageGridView_SizeChanged(object sender, SizeChangedEventArgs e) => GridManager.ImageGridView_SizeChanged(sender, e);
        private void MenuThumbnailRefresh_Click(object sender, RoutedEventArgs e) => GridManager.RefreshThumbnails();

        private void RootGrid_DragOver(object sender, DragEventArgs e) => InputHandler.HandleDragOver(sender, e);
        private void RootGrid_Drop(object sender, DragEventArgs e) => InputHandler.HandleDrop(sender, e);
        private System.Threading.CancellationTokenSource? _loadCts;

        public void LoadDirectory(string path, string initialFile = "", bool includeSiblings = false, bool includeSubfolders = false)
        {
            _loadCts?.Cancel();
            _loadCts = new System.Threading.CancellationTokenSource();
            var token = _loadCts.Token;

            _currentDirectory = path;
            FolderSearchingOverlay.Visibility = Visibility.Visible;
            IsSearchingFolder = true;

            _ = Task.Run(() =>
            {
                try
                {
                    List<string> initialFiles;
                    if (ArchiveManager.IsArchive(path))
                    {
                        initialFiles = ArchiveManager.GetArchiveImages(path);
                    }
                    else
                    {
                        initialFiles = GetFilesFromDirectory(path, false);
                    }

                    var sorted = initialFiles.Distinct().OrderBy(f => f, new NaturalStringComparer()).ToList();

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        UpdatePlaylist(sorted, initialFile, true);
                        OnInitialFilesLoaded(path, initialFile, includeSiblings, includeSubfolders, token);
                    });
                }
                catch
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        FolderSearchingOverlay.Visibility = Visibility.Collapsed;
                        IsSearchingFolder = false;
                    });
                }
            });
        }

        private void OnInitialFilesLoaded(string path, string initialFile, bool includeSiblings, bool includeSubfolders, System.Threading.CancellationToken token)
        {
            if (_playlist.Count > 0)
            {
                UpdateGridItems(false);
                _ = UpdateDisplayAsync();

                if (!ArchiveManager.IsArchive(path) && (includeSiblings || includeSubfolders))
                {
                    _ = Task.Run(() => LoadAdditionalFilesAsync(path, initialFile, includeSiblings, includeSubfolders, token));
                }
                else
                {
                    FolderSearchingOverlay.Visibility = Visibility.Collapsed;
                    IsSearchingFolder = false;
                }
            }
            else if (includeSiblings || includeSubfolders)
            {
                _ = Task.Run(() => LoadAdditionalFilesAsync(path, initialFile, includeSiblings, includeSubfolders, token));
            }
            else
            {
                FolderSearchingOverlay.Visibility = Visibility.Collapsed;
                IsSearchingFolder = false;
            }
        }

        internal void UpdateGridItems(bool forceFullUpdate)
        {
            // If we are in viewer mode and NOT switching to grid mode, 
            // we can defer populating thousands of grid items to avoid UI lag.
            if (!IsGridMode && !forceFullUpdate && _playlist.Count > 500)
            {
                _gridItems.Clear();
                // Add at least current image so grid isn't totally empty if user peeks
                if (_currentIndex >= 0 && _currentIndex < _playlist.Count)
                {
                    _gridItems.Add(new ImageItem { FilePath = _playlist[_currentIndex], IsLoading = true });
                }
                return;
            }

            // Efficiently update grid items
            if (_gridItems.Count == _playlist.Count) return;

            // Replacing the collection or clearing/adding many items can be slow.
            // Using a new collection and setting ItemsSource is often faster in WinUI for large lists.
            var newList = new ObservableCollection<ImageItem>();
            foreach (var f in _playlist) newList.Add(new ImageItem { FilePath = f, IsLoading = true });

            _gridItems = newList;
            ImageGridView.ItemsSource = _gridItems;
            GridManager.RefreshThumbnails();
        }

        private List<string> GetFilesFromDirectory(string dir, bool recursive)
        {
            List<string> files = new List<string>();
            try
            {
                var options = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (var f in Directory.EnumerateFiles(dir, "*", options))
                {
                    if (IsSupportedExtension(Path.GetExtension(f)))
                    {
                        files.Add(f);
                    }
                }
            }
            catch { }
            return files;
        }

        private void UpdatePlaylist(List<string> files, string targetPath, bool alreadySorted)
        {
            string currentPath = !string.IsNullOrEmpty(targetPath) ? targetPath : CurrentImagePath;
            if (alreadySorted)
            {
                _playlist = files;
            }
            else
            {
                _playlist = files.Distinct().OrderBy(f => f, new NaturalStringComparer()).ToList();
            }

            if (_playlist.Count > 0)
            {
                int idx = _playlist.IndexOf(currentPath);
                _currentIndex = idx >= 0 ? idx : 0;
            }
            else
            {
                _currentIndex = -1;
            }
        }

        private async Task LoadAdditionalFilesAsync(string path, string initialFile, bool includeSiblings, bool includeSubfolders, System.Threading.CancellationToken token)
        {
            try
            {
                List<string> accumulatedFiles = new List<string>(_playlist);
                List<string> targetDirs = new List<string>();

                if (includeSiblings)
                {
                    string cleanPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string? parent = Path.GetDirectoryName(cleanPath);
                    if (!string.IsNullOrEmpty(parent))
                    {
                        try
                        {
                            foreach (var d in Directory.EnumerateDirectories(parent))
                            {
                                if (token.IsCancellationRequested) return;
                                if (d.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) != cleanPath)
                                {
                                    targetDirs.Add(d);
                                }
                            }
                        }
                        catch { }
                    }
                }

                var comparer = new NaturalStringComparer();

                foreach (var dir in targetDirs)
                {
                    if (token.IsCancellationRequested) return;

                    var folderFiles = await Task.Run(() => GetFilesFromDirectory(dir, includeSubfolders));
                    if (folderFiles.Count > 0)
                    {
                        accumulatedFiles.AddRange(folderFiles);
                        var sortedBatch = accumulatedFiles.Distinct().OrderBy(f => f, comparer).ToList();
                        accumulatedFiles = sortedBatch;

                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (token.IsCancellationRequested) return;
                            string currentPath = CurrentImagePath;
                            UpdatePlaylist(sortedBatch, currentPath, true);
                            UpdateGridItems(false);
                            UpdatePageIndicator();
                        });
                    }
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    FolderSearchingOverlay.Visibility = Visibility.Collapsed;
                    IsSearchingFolder = false;
                });
            }
            catch
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    FolderSearchingOverlay.Visibility = Visibility.Collapsed;
                    IsSearchingFolder = false;
                });
            }
        }


        internal Task UpdateDisplayAsync() => ViewerManager.UpdateDisplayAsync();
        internal void UpdatePageIndicator()
        {
            if (_settings.ShowPageIndicator && _playlist.Count > 0 && _currentIndex >= 0)
            {
                int displayIndex = IsGridMode ? (_currentIndex + 1) : Math.Min(_currentIndex + _settings.MangaSplitCount, _playlist.Count);
                PageIndicator.Text = $"{displayIndex} / {_playlist.Count}";
                PageIndicatorContainer.Visibility = Visibility.Visible;
            }
            else
            {
                PageIndicatorContainer.Visibility = Visibility.Collapsed;
            }
        }
        internal List<Grid> GetPageGrids() => new List<Grid> { PageGrid1, PageGrid2, PageGrid3, PageGrid4 };
        private void Canvas1_PaintSurface(object sender, SKPaintSurfaceEventArgs e) => ViewerManager.PaintCanvas(0, e);
        private void Canvas2_PaintSurface(object sender, SKPaintSurfaceEventArgs e) => ViewerManager.PaintCanvas(1, e);
        private void Canvas3_PaintSurface(object sender, SKPaintSurfaceEventArgs e) => ViewerManager.PaintCanvas(2, e);
        private void Canvas4_PaintSurface(object sender, SKPaintSurfaceEventArgs e) => ViewerManager.PaintCanvas(3, e);

        internal string CurrentImagePath => _playlist != null && _currentIndex >= 0 && _currentIndex < _playlist.Count ? _playlist[_currentIndex] : string.Empty;
        internal void ShowNotification(string message) => ViewerManager.ShowNotification(message);
        internal void Navigate(int offset, bool forceSingleStep = false) => ViewerManager.Navigate(offset, forceSingleStep);
        internal void NavigateFolder(int offset) => ViewerManager.NavigateFolder(offset);

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
