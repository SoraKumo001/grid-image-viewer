using grid_image_viewer.Controls;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace grid_image_viewer
{
    public interface IMainView
    {
        DispatcherQueue DispatcherQueue { get; }
        MainViewModel ViewModel { get; }
        ViewerPanel ViewerControl { get; }
        GridView ImageGridView { get; }
        IViewerStateService State { get; }

        int CurrentIndex { get; set; }
        bool IsGridMode { get; set; }
        ObservableCollection<string> Playlist { get; }
        System.Collections.Generic.IList<ImageItem> GridItems { get; }

        UIElement NotificationOverlay { get; }
        TextBlock NotificationText { get; }

        UIElement MetadataPanel { get; }
        TextBlock TxtMetaTitle { get; }
        TextBlock TxtMetaFileName { get; }
        TextBlock TxtMetaDimensions { get; }
        TextBlock TxtMetaFileSize { get; }
        UIElement ExifDivider { get; }
        UIElement ExifGrid { get; }
        TextBlock TxtMetaCamera { get; }
        TextBlock TxtMetaLens { get; }
        TextBlock TxtMetaSettings { get; }
        TextBlock TxtMetaDate { get; }
        Grid PagesGrid { get; }
        Grid RootGrid { get; }
        bool IsDialogOpen { get; set; }


        void LoadDirectory(string path, string initialFile = "", bool includeSiblings = false, bool includeSubfolders = false, System.Collections.Generic.List<string>? preloadedPlaylist = null);
        void UpdatePageIndicator();
        Task UpdateDisplayAsync();
        void UpdateGridItems(bool forceFullUpdate);
        void ShowNotification(string message);
        void StopAnimation();
        void ClearCachedBitmap(string path);
        void Close();
        void Navigate(int direction, bool forceSingleStep = false);
        void NavigateFolder(int direction);

        Microsoft.UI.Windowing.AppWindow AppWindow { get; }
        bool IsSearchingFolder { get; set; }

        Windows.Foundation.Rect Bounds { get; }
        string CurrentDirectory { get; set; }
        ScrollViewer ImageScrollViewer { get; }

        ISlideshowManager SlideshowManager { get; }
        IImageEditService ImageEditService { get; }
        IDialogService DialogService { get; }
        IAnimationService AnimationService { get; }
        INotificationService NotificationService { get; }
        IMetadataDisplayService MetadataDisplayService { get; }
        IPlaylistManager PlaylistManager { get; }
        IViewerManager ViewerManager { get; }
        IGridManager GridManager { get; }
        IEditorManager EditorManager { get; }
        IBookmarkManager BookmarkManager { get; }
        IAppWindowManager AppWindowManager { get; }
        ListView BookmarkListView { get; }
        IPrintService PrintService { get; }

        FrameworkElement PageGrid1 { get; }
        FrameworkElement PageGrid2 { get; }
        FrameworkElement PageGrid3 { get; }
        FrameworkElement PageGrid4 { get; }

        System.Collections.Generic.List<FrameworkElement> GetPageGrids();

        // UI elements for Slideshow
        ContentDialog SlideshowDialog { get; }
        CheckBox SlideshowFullscreen { get; }
        CheckBox SlideshowRandom { get; }
        CheckBox SlideshowLoop { get; }
        CheckBox SlideshowNextFolder { get; }
        CheckBox SlideshowIncludeSiblings { get; }
        CheckBox SlideshowCurrentFolderOnly { get; }
        CheckBox SlideshowUniformToFill { get; }
        CheckBox SlideshowCrossfade { get; }
        NumberBox SlideshowInterval { get; }
        NumberBox SlideshowCrossfadeDuration { get; }

        UIElement BookmarkPanel { get; }

        MenuFlyoutItem MenuUndo { get; }
        MenuFlyoutItem MenuRedo { get; }
        MenuFlyoutSubItem MenuSaveAs { get; }
        MenuFlyoutItem MenuOverwrite { get; }
        MenuFlyoutItem MenuCrop { get; }
        MenuFlyoutItem MenuResize { get; }
        MenuFlyoutSubItem MenuRotate { get; }
        MenuFlyoutSubItem MenuFlip { get; }
        MenuFlyoutItem MenuTone { get; }
        MenuFlyoutSubItem MenuFilter { get; }
        MenuFlyoutItem MenuPrint { get; }

        ToggleMenuFlyoutItem MenuViewSingle { get; }
        ToggleMenuFlyoutItem MenuViewDouble { get; }
        ToggleMenuFlyoutItem MenuViewQuad { get; }

        ToggleMenuFlyoutItem MenuLayoutAuto { get; }
        ToggleMenuFlyoutItem MenuLayoutHorz { get; }
        ToggleMenuFlyoutItem MenuLayoutGrid { get; }

        ToggleMenuFlyoutItem MenuStretchOriginal { get; }
        ToggleMenuFlyoutItem MenuStretchContain { get; }
        ToggleMenuFlyoutItem MenuStretchCover { get; }

        ToggleMenuFlyoutItem MenuMetadata { get; }
        ToggleMenuFlyoutItem MenuPageIndicatorToggle { get; }

        MenuFlyoutItem MenuBookmark { get; }
        ToggleMenuFlyoutItem MenuBookmarksToggle { get; }
        MenuFlyoutSubItem MenuBookmarkList { get; }


        bool IsFullscreen { get; set; }
        UIElement Content { get; }

        System.IntPtr WindowHandle { get; }
        bool ExtendsContentIntoTitleBar { get; set; }
        void SetTitleBar(UIElement titleBar);
        UIElement AppTitleBar { get; }
        string CurrentImagePath { get; }
    }
}
