using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace grid_image_viewer.Controls
{
    public sealed partial class SettingsOverlay : UserControl
    {
        private readonly MainWindow _window;
        private readonly SettingsManager _settings;

        public SettingsOverlay(MainWindow window, SettingsManager settings)
        {
            this.InitializeComponent();
            _window = window;
            _settings = settings;

            SliderQuality.Value = _settings.JpegQuality;
            ComboBackground.SelectedIndex = _settings.BackgroundColorMode;
            CheckHighQuality.IsChecked = _settings.UseHighQualityScaling;

            BtnClose.Focus(FocusState.Programmatic);
        }

        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            await _settings.ExportSettingsAsync(hwnd);
        }

        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            if (await _settings.ImportSettingsAsync(hwnd))
            {
                // Refresh UI after import
                SliderQuality.Value = _settings.JpegQuality;
                ComboBackground.SelectedIndex = _settings.BackgroundColorMode;
                CheckHighQuality.IsChecked = _settings.UseHighQualityScaling;
                _window.ApplyBackgroundSettings();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            SaveAndClose();
        }

        private void Overlay_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                SaveAndClose();
                e.Handled = true;
            }
        }

        private void SaveAndClose()
        {
            _settings.JpegQuality = (int)SliderQuality.Value;
            _settings.BackgroundColorMode = ComboBackground.SelectedIndex;
            _settings.UseHighQualityScaling = CheckHighQuality.IsChecked ?? true;
            _settings.SaveSettings();

            _window.ApplyBackgroundSettings();

            var parent = this.Parent as Panel;
            parent?.Children.Remove(this);
            _window.IsDialogOpen = false;
        }
    }
}
