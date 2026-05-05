using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Linq;

namespace grid_image_viewer
{
    internal class DialogService : IDialogService
    {
        private readonly IMainView _window;

        public DialogService(IMainView window)
        {
            _window = window;
        }

        public void Show(FrameworkElement overlay, int rowSpan = 2)
        {
            if (!string.IsNullOrEmpty(overlay.Name))
            {
                Close(overlay.Name);
            }

            Grid.SetRow(overlay, 0);
            Grid.SetRowSpan(overlay, rowSpan);
            overlay.HorizontalAlignment = HorizontalAlignment.Stretch;
            overlay.VerticalAlignment = VerticalAlignment.Stretch;
            _window.RootGrid.Children.Add(overlay);
            _window.IsDialogOpen = true;
            overlay.Focus(FocusState.Programmatic);
        }

        public void Close(string name)
        {
            var existing = _window.RootGrid.Children.FirstOrDefault(c => c is FrameworkElement fe && fe.Name == name);
            if (existing != null)
            {
                _window.RootGrid.Children.Remove(existing);
            }
            UpdateDialogOpenState();
        }

        public void Close(FrameworkElement overlay)
        {
            if (_window.RootGrid.Children.Contains(overlay))
            {
                _window.RootGrid.Children.Remove(overlay);
            }
            UpdateDialogOpenState();
        }

        public void UpdateDialogOpenState()
        {
            // Check for any of our standard overlays
            bool anyOpen = _window.RootGrid.Children.Any(c =>
                c is FrameworkElement fe && (
                    fe.Name == "SettingsOverlay" ||
                    fe.Name == "KeyBindingsOverlay" ||
                    fe.Name == "ResizeOverlay" ||
                    fe.Name == "ToneAdjustmentOverlay" ||
                    fe.Name == "CropOverlay"
                ));

            _window.IsDialogOpen = anyOpen;
        }
    }
}
