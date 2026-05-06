using Microsoft.UI.Xaml.Controls;

namespace quick_image_viewer.Views.Controls
{
    public sealed partial class NotificationOverlayControl : UserControl
    {
        public NotificationOverlayControl()
        {
            this.InitializeComponent();
        }

        public TextBlock Text => NotificationText;
    }
}