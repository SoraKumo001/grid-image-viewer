using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace grid_image_viewer.Controls
{
    public sealed partial class ResizeOverlay : UserControl
    {
        private readonly IMainView _window;
        private readonly string _sourcePath;

        public ResizeOverlay(IMainView window, string sourcePath, int initialWidth, int initialHeight)
        {
            this.InitializeComponent();
            _window = window;
            _sourcePath = sourcePath;

            BoxWidth.Value = initialWidth;
            BoxHeight.Value = initialHeight;

            // Focus on width box
            BoxWidth.Focus(FocusState.Programmatic);
        }

        private async void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            int newWidth = (int)BoxWidth.Value;
            int newHeight = (int)BoxHeight.Value;

            if (newWidth <= 0 || newHeight <= 0) return;

            await _window.ImageEditService.ApplyTransformationAsync(_sourcePath,
                (current) => ImageProcessor.GetResizedBitmap(_sourcePath, newWidth, newHeight, current));

            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

        private void Overlay_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Enter)
            {
                BtnApply_Click(null!, null!);
                e.Handled = true;
            }
        }

        private void Close()
        {
            var parent = this.Parent as Panel;
            parent?.Children.Remove(this);
            _window.IsDialogOpen = false;
        }
    }
}
