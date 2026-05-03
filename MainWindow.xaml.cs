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

        // === Properties exposed for SlideshowManager / InputHandler ===
        internal SlideshowManager SlideshowManager => _slideshowManager;
        internal InputHandler InputHandler => _inputHandler;
        internal GridManager GridManager => _gridManager;
        internal ViewerManager ViewerManager => _viewerManager;
        internal bool IsGridMode { get => _isGridMode; set => _isGridMode = value; }
        internal ObservableCollection<ImageItem> GridItems => _gridItems;
        internal bool IsDialogOpen { get => _isDialogOpen; set => _isDialogOpen = value; }
        internal List<string> Playlist => _playlist;
        internal int CurrentIndex { get => _currentIndex; set => _currentIndex = value; }
        internal string CurrentDirectory { get => _currentDirectory; set => _currentDirectory = value; }
        internal bool IsSearchingFolder { get => _isSearchingFolder; set => _isSearchingFolder = value; }
        internal EditorManager EditorManager => _editorManager;
        internal ImageEditService ImageEditService => _imageEditService;

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

        private SettingsManager _settings = new SettingsManager();

        private ObservableCollection<ImageItem> _gridItems = new ObservableCollection<ImageItem>();
        private bool _isGridMode = false;

        private SlideshowManager _slideshowManager;
        private InputHandler _inputHandler;
        private GridManager _gridManager;
        private ViewerManager _viewerManager;
        internal EditorManager _editorManager;
        private ImageEditService _imageEditService;
        private bool _isDialogOpen = false;
        private Random _random = new Random();
        private bool _isSearchingFolder = false;

        public MainWindow()
        {
            InitializeComponent();
            _gridItems = new ObservableCollection<ImageItem>();
            ImageGridView.ItemsSource = _gridItems;

            this.Closed += MainWindow_Closed;

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            _settings.LoadWindowState(appWindow);
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
                    var fallbackPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "Assets", "AppIcon.ico");
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

            _slideshowManager = new SlideshowManager(this, _settings);
            _inputHandler = new InputHandler(this, _settings);
            _gridManager = new GridManager(this, _settings);
            _imageEditService = new ImageEditService(this);
            _viewerManager = new ViewerManager(this, _settings);
            _editorManager = new EditorManager(this, _settings);

            ImageGridView.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(ImageGridView_PointerWheelChanged), true);
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            _viewerManager.Dispose();
            _gridManager.Dispose();
            foreach (var item in _gridItems) item.DisposeCodec();

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            _settings.SaveWindowState(appWindow, CurrentImagePath);
        }

        private void ImageGridView_ItemClick(object sender, ItemClickEventArgs e) => _gridManager.ImageGridView_ItemClick(sender, e);
        private void ImageGridView_SizeChanged(object sender, SizeChangedEventArgs e) => _gridManager.ImageGridView_SizeChanged(sender, e);


        private void RootGrid_DragOver(object sender, DragEventArgs e) => _inputHandler.HandleDragOver(sender, e);
        private void RootGrid_Drop(object sender, DragEventArgs e) => _inputHandler.HandleDrop(sender, e);
        public void LoadDirectory(string path, string initialFile = "", bool includeSiblings = false, bool includeSubfolders = false)
        {
            _currentDirectory = path;
            try
            {
                var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".avif", ".avis", ".heic", ".heif", ".jxl", ".tif", ".tiff", ".svg", ".psd", ".ico" };

                List<string> fileList = new List<string>();
                List<string> targetDirs = new List<string>();

                if (includeSiblings)
                {
                    string cleanPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string? parent = Path.GetDirectoryName(cleanPath);
                    if (!string.IsNullOrEmpty(parent))
                    {
                        if (includeSubfolders)
                        {
                            // If both are true, we just need to search from the parent recursively once.
                            targetDirs.Add(parent);
                        }
                        else
                        {
                            // Include parent and all immediate siblings
                            targetDirs.Add(parent);
                            try
                            {
                                foreach (var d in Directory.EnumerateDirectories(parent))
                                {
                                    targetDirs.Add(d);
                                }
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        targetDirs.Add(path);
                    }
                }
                else
                {
                    targetDirs.Add(path);
                }

                // Gather files from all target directories robustly
                foreach (var dir in targetDirs)
                {
                    try
                    {
                        var files = includeSubfolders ? Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories) : Directory.EnumerateFiles(dir);
                        foreach (var f in files)
                        {
                            if (extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                            {
                                fileList.Add(f);
                            }
                        }
                    }
                    catch { }
                }

                _playlist = fileList.Distinct().OrderBy(f => f, new NaturalStringComparer()).ToList();

                if (_playlist.Count > 0)
                {
                    _currentIndex = string.IsNullOrEmpty(initialFile) ? 0 : _playlist.IndexOf(initialFile);
                    if (_currentIndex == -1) _currentIndex = 0;

                    _gridItems.Clear();
                    foreach (var f in _playlist)
                    {
                        _gridItems.Add(new ImageItem { FilePath = f, IsLoading = true });
                    }
                    _gridManager.RefreshThumbnails();
                    _ = UpdateDisplayAsync();
                }
            }
            catch (Exception)
            {
                // Ignore access errors
            }
        }


        internal Task UpdateDisplayAsync() => _viewerManager.UpdateDisplayAsync();
        private void Canvas1_PaintSurface(object sender, SKPaintSurfaceEventArgs e) => _viewerManager.PaintCanvas(0, e);
        private void Canvas2_PaintSurface(object sender, SKPaintSurfaceEventArgs e) => _viewerManager.PaintCanvas(1, e);
        private void Canvas3_PaintSurface(object sender, SKPaintSurfaceEventArgs e) => _viewerManager.PaintCanvas(2, e);
        private void Canvas4_PaintSurface(object sender, SKPaintSurfaceEventArgs e) => _viewerManager.PaintCanvas(3, e);

        internal string CurrentImagePath => _playlist != null && _currentIndex >= 0 && _currentIndex < _playlist.Count ? _playlist[_currentIndex] : string.Empty;
        internal void ShowNotification(string message) => _viewerManager.ShowNotification(message);
        internal void Navigate(int offset, bool forceSingleStep = false) => _viewerManager.Navigate(offset, forceSingleStep);
        internal void NavigateFolder(int offset) => _viewerManager.NavigateFolder(offset);

        private void RootGrid_PointerWheelChanged(object sender, PointerRoutedEventArgs e) => _inputHandler.HandlePointerWheelChanged(sender, e);
        private void ImageGridView_PointerWheelChanged(object sender, PointerRoutedEventArgs e) => _inputHandler.HandlePointerWheelChanged(sender, e);
        private void RootGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => _inputHandler.HandleDoubleTapped(sender, e);
        private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e) => _inputHandler.HandleKeyDown(sender, e);

        private void PagesGrid_PointerPressed(object sender, PointerRoutedEventArgs e) => _editorManager.PagesGrid_PointerPressed(sender, e);
        private void PagesGrid_PointerMoved(object sender, PointerRoutedEventArgs e) => _editorManager.PagesGrid_PointerMoved(sender, e);
        private void PagesGrid_PointerReleased(object sender, PointerRoutedEventArgs e) => _editorManager.PagesGrid_PointerReleased(sender, e);
        private void EditMenuFlyout_Opening(object sender, object e) => _editorManager.EditMenuFlyout_Opening(sender, e);
        private void MenuSaveAs_Click(object sender, RoutedEventArgs e) => _editorManager.MenuSaveAs_Click(sender, e);
        private void MenuOverwrite_Click(object sender, RoutedEventArgs e) => _editorManager.MenuOverwrite_Click(sender, e);
        private void MenuCrop_Click(object sender, RoutedEventArgs e) => _editorManager.MenuCrop_Click(sender, e);
        private void MenuResize_Click(object sender, RoutedEventArgs e) => _editorManager.MenuResize_Click(sender, e);
        private void MenuRotate_Click(object sender, RoutedEventArgs e) => _editorManager.MenuRotate_Click(sender, e);
        private void MenuFlip_Click(object sender, RoutedEventArgs e) => _editorManager.MenuFlip_Click(sender, e);
        private void MenuTone_Click(object sender, RoutedEventArgs e) => _editorManager.MenuTone_Click(sender, e);
        private void MenuFilter_Click(object sender, RoutedEventArgs e) => _editorManager.MenuFilter_Click(sender, e);
        private void MenuOpenExplorer_Click(object sender, RoutedEventArgs e) => _editorManager.MenuOpenExplorer_Click(sender, e);
        private void MenuViewMode_Click(object sender, RoutedEventArgs e) => _editorManager.MenuViewMode_Click(sender, e);
        private void MenuLayoutMode_Click(object sender, RoutedEventArgs e) => _editorManager.MenuLayoutMode_Click(sender, e);
        private void MenuStretchMode_Click(object sender, RoutedEventArgs e) => _editorManager.MenuStretchMode_Click(sender, e);
        private void MenuKeyBindings_Click(object sender, RoutedEventArgs e) => _editorManager.MenuKeyBindings_Click(sender, e);
        private void MenuSettings_Click(object sender, RoutedEventArgs e) => _editorManager.MenuSettings_Click(sender, e);
        private void MenuUndo_Click(object sender, RoutedEventArgs e) => _editorManager.MenuUndo_Click(sender, e);
        private void MenuRedo_Click(object sender, RoutedEventArgs e) => _editorManager.MenuRedo_Click(sender, e);
        private void MenuMetadata_Click(object sender, RoutedEventArgs e) => _editorManager.MenuMetadata_Click(sender, e);

        private void OpenSlideshowDialogAsync() => _slideshowManager.OpenSlideshowDialogAsync();
        private void SlideshowDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args) => _slideshowManager.SlideshowDialog_Opened(sender, args);
        private void SlideshowDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args) => _slideshowManager.SlideshowDialog_PrimaryButtonClick(sender, args);
    }
}
