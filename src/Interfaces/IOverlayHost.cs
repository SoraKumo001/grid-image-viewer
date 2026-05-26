using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using quick_image_viewer.ViewModels;

namespace quick_image_viewer.Interfaces
{
    public interface IOverlayHost
    {
        Grid RootGrid { get; }
        bool IsDialogOpen { get; set; }
        DispatcherQueue DispatcherQueue { get; }

        MainViewModel ViewModel { get; }
        IMenuStateManager MenuStateManager { get; }
        IAppWindowManager AppWindowManager { get; }
        IViewerManager ViewerManager { get; }
        IImageEditService ImageEditService { get; }

        string CurrentDirectory { get; set; }
        string CurrentImagePath { get; }
        System.IntPtr WindowHandle { get; }

        void UpdatePageIndicator();
    }
}
