using Microsoft.UI.Xaml;

namespace quick_image_viewer.Interfaces
{
    public interface IFileOperationHost
    {
        UIElement Content { get; }
        bool IsDialogOpen { get; set; }
        System.IntPtr WindowHandle { get; }

        IPlaylistManager PlaylistManager { get; }
        IImageEditService ImageEditService { get; }
        IViewerManager ViewerManager { get; }

        void ShowNotification(string message);
    }
}
