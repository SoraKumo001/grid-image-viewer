using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace quick_image_viewer.Interfaces
{
    public interface INotificationHost
    {
        UIElement NotificationOverlay { get; }
        TextBlock NotificationText { get; }
    }
}
