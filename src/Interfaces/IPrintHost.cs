using Microsoft.UI.Dispatching;

namespace quick_image_viewer.Interfaces
{
    public interface IPrintHost
    {
        System.IntPtr WindowHandle { get; }
        DispatcherQueue DispatcherQueue { get; }
        void ShowNotification(string message);
    }
}
