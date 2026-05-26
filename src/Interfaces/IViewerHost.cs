using quick_image_viewer.Services;
using quick_image_viewer.ViewModels;
using quick_image_viewer.Views.Controls;
using System.Collections.ObjectModel;

namespace quick_image_viewer.Interfaces
{
    public interface IViewerHost : IViewerLoaderHost
    {
        IViewerStateService State { get; }
        ViewerPanel ViewerControl { get; }
        MainViewModel ViewModel { get; }
        ObservableCollection<string> Playlist { get; }

        int CurrentIndex { get; set; }
        string CurrentDirectory { get; set; }
        bool IsGridMode { get; set; }

        Microsoft.UI.Xaml.Controls.Grid PagesGrid { get; }
        Microsoft.UI.Xaml.Controls.GridView ImageGridView { get; }
        Microsoft.UI.Xaml.Controls.ScrollViewer ImageScrollViewer { get; }
        Windows.Foundation.Rect Bounds { get; }

        IAnimationService AnimationService { get; }
        IMetadataDisplayService MetadataDisplayService { get; }
        IPlaylistManager PlaylistManager { get; }
        IGridManager GridManager { get; }
        INotificationService NotificationService { get; }

        void UpdateGridItems(bool forceFullUpdate);
        void UpdatePageIndicator();
    }
}
