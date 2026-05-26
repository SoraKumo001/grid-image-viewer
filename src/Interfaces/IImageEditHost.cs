using Microsoft.UI.Dispatching;

namespace quick_image_viewer.Interfaces
{
    public interface IImageEditHost
    {
        DispatcherQueue DispatcherQueue { get; }
        IViewerManager ViewerManager { get; }
    }
}
