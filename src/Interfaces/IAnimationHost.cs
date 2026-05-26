using Microsoft.UI.Dispatching;

namespace quick_image_viewer.Interfaces
{
    public interface IAnimationHost
    {
        DispatcherQueue DispatcherQueue { get; }
        IViewerManager ViewerManager { get; }
        IMetadataDisplayService MetadataDisplayService { get; }
        bool IsGridMode { get; set; }
        System.Collections.ObjectModel.ObservableCollection<string> Playlist { get; }
    }
}
