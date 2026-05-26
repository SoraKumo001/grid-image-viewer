using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using quick_image_viewer.ViewModels;
using System.Collections.ObjectModel;

namespace quick_image_viewer.Interfaces
{
    public interface IInputHost
    {
        bool IsGridMode { get; set; }
        bool IsDialogOpen { get; set; }
        bool IsFullscreen { get; set; }

        string CurrentImagePath { get; }
        int CurrentIndex { get; set; }
        ObservableCollection<string> Playlist { get; }

        MainViewModel ViewModel { get; }
        Grid RootGrid { get; }
        Grid PagesGrid { get; }
        ScrollViewer ImageScrollViewer { get; }

        IViewerManager ViewerManager { get; }
        IMetadataDisplayService MetadataDisplayService { get; }
        IImageEditService ImageEditService { get; }
        ISlideshowManager SlideshowManager { get; }

        void ShowNotification(string message);
        void Close();
        System.Collections.Generic.List<FrameworkElement> GetPageGrids();
    }
}
