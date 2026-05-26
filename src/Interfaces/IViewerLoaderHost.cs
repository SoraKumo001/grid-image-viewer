using Microsoft.UI.Dispatching;

namespace quick_image_viewer.Interfaces
{
    public interface IViewerLoaderHost
    {
        DispatcherQueue DispatcherQueue { get; }
        ISlideshowManager SlideshowManager { get; }
        IImageEditService ImageEditService { get; }
        void ShowNotification(string message);
    }
}
