using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Managers;
using quick_image_viewer.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace quick_image_viewer.Views.Controls
{
    public class ExtensionItem
    {
        public string Name { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }

    public sealed partial class SettingsOverlay : UserControl
    {
        private readonly IMainView _window;
        private readonly ISettingsManager _settings;
        private ObservableCollection<ExtensionItem> _extensionsList = new();

        public SettingsOverlay(IMainView window, ISettingsManager settings)
        {
            this.InitializeComponent();
            _window = window;
            _settings = settings;

            SliderQuality.Value = _settings.JpegQuality;
            ComboBackground.SelectedIndex = _settings.BackgroundColorMode;
            CheckHighQuality.IsChecked = _settings.UseHighQualityScaling;
            CheckShowPageIndicator.IsChecked = _settings.ShowPageIndicator;
            ComboBoundary.SelectedIndex = _settings.BoundaryAction;

            InitializeExtensionsList();

            BtnClose.Focus(FocusState.Programmatic);
        }

        private void InitializeExtensionsList()
        {
            var all = FolderDiscoveryService.SupportedExtensions.Concat(ArchiveManager.ArchiveExtensions).Distinct().OrderBy(e => e);
            _extensionsList.Clear();
            foreach (var ext in all)
            {
                _extensionsList.Add(new ExtensionItem
                {
                    Name = ext,
                    IsEnabled = _settings.EnabledExtensions.Contains(ext)
                });
            }
            ItemsExtensions.ItemsSource = _extensionsList;
        }

        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var hwnd = _window.WindowHandle;
            await _settings.ExportSettingsAsync(hwnd);
        }

        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var hwnd = _window.WindowHandle;
            if (await _settings.ImportSettingsAsync(hwnd))
            {
                // Refresh UI after import
                SliderQuality.Value = _settings.JpegQuality;
                ComboBackground.SelectedIndex = _settings.BackgroundColorMode;
                CheckHighQuality.IsChecked = _settings.UseHighQualityScaling;
                CheckShowPageIndicator.IsChecked = _settings.ShowPageIndicator;
                ComboBoundary.SelectedIndex = _settings.BoundaryAction;
                InitializeExtensionsList();
                _window.AppWindowManager.ApplyBackgroundSettings();
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
            _settings.ShowPageIndicator = CheckShowPageIndicator.IsChecked ?? true;
            _settings.BoundaryAction = ComboBoundary.SelectedIndex;

            _settings.EnabledExtensions = _extensionsList.Where(i => i.IsEnabled).Select(i => i.Name).ToList();

            _settings.SaveSettings();

            _window.ViewModel.BoundaryAction = _settings.BoundaryAction;

            _window.AppWindowManager.ApplyBackgroundSettings();
            _window.UpdatePageIndicator();

            var parent = this.Parent as Panel;
            parent?.Children.Remove(this);
            _window.IsDialogOpen = false;
        }
    }
}
