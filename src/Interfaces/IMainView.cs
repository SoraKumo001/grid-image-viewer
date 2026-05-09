using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using quick_image_viewer.Models;
using quick_image_viewer.Services;
using quick_image_viewer.ViewModels;
using quick_image_viewer.Views.Controls;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
namespace quick_image_viewer.Interfaces
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



        void UpdatePageIndicator();
        Task UpdateDisplayAsync();
        void UpdateGridItems(bool forceFullUpdate);
        void ShowNotification(string message);
        void StopAnimation();
        void ClearCachedBitmap(string path);
        void Close();
        void SetGridLoading(bool isLoading, bool isBackground = false);

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
        IMenuStateManager MenuStateManager { get; }
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
        ComboBox SlideshowMangaSplitCount { get; }
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
