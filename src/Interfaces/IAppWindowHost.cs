using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace quick_image_viewer.Interfaces
{
    public interface IAppWindowHost
    {
        bool ExtendsContentIntoTitleBar { get; set; }
        void SetTitleBar(UIElement titleBar);
        Border AppTitleBar { get; }
        Grid RootGrid { get; }
        System.IntPtr WindowHandle { get; }
        string CurrentImagePath { get; }
        string CurrentDirectory { get; set; }
    }
}
