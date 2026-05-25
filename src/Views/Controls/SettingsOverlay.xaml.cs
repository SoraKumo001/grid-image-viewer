using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Managers;
using quick_image_viewer.Models;
using quick_image_viewer.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace quick_image_viewer.Views.Controls
{
    public sealed partial class SettingsOverlay : UserControl
    {
        private readonly IMainView _window;
        private readonly ISettingsManager _settings;
        private ObservableCollection<ExtensionItem> _extensionsImages = new();
        private ObservableCollection<ExtensionItem> _extensionsVideos = new();
        private ObservableCollection<ExtensionItem> _extensionsArchives = new();
        private ObservableCollection<ExtensionItem> _extensionsDocuments = new();
        private bool _isInitializing = true;

        private class KeyBindingDefinition
        {
            public string PropertyName { get; }
            public string ResourceKey { get; }
            public string CategoryResourceKey { get; }

            public KeyBindingDefinition(string propertyName, string resourceKey, string categoryResourceKey)
            {
                PropertyName = propertyName;
                ResourceKey = resourceKey;
                CategoryResourceKey = categoryResourceKey;
            }
        }

        private static readonly List<KeyBindingDefinition> KeyBindingDefinitions = new()
        {
            // Navigation
            new(nameof(ISettingsManager.KeyNextImage), "KeyBinding_NextImage", "KeyBinding_Category_Navigation"),
            new(nameof(ISettingsManager.KeyPrevImage), "KeyBinding_PrevImage", "KeyBinding_Category_Navigation"),
            new(nameof(ISettingsManager.KeyNextFolder), "KeyBinding_NextFolder", "KeyBinding_Category_Navigation"),
            new(nameof(ISettingsManager.KeyPrevFolder), "KeyBinding_PrevFolder", "KeyBinding_Category_Navigation"),

            // View Modes
            new(nameof(ISettingsManager.KeyToggleManga), "KeyBinding_ToggleManga", "KeyBinding_Category_View"),
            new(nameof(ISettingsManager.KeyToggleReadingDirection), "KeyBinding_ToggleReadingDirection", "KeyBinding_Category_View"),
            new(nameof(ISettingsManager.KeyToggleGrid), "KeyBinding_ToggleGrid", "KeyBinding_Category_View"),
            new(nameof(ISettingsManager.KeySlideshow), "KeyBinding_Slideshow", "KeyBinding_Category_View"),
            new(nameof(ISettingsManager.KeyMetadata), "KeyBinding_Metadata", "KeyBinding_Category_View"),
            new(nameof(ISettingsManager.KeyToggleFullscreen), "KeyBinding_ToggleFullscreen", "KeyBinding_Category_View"),
            new(nameof(ISettingsManager.KeyToggleStretchMode), "KeyBinding_ToggleStretchMode", "KeyBinding_Category_View"),

            // Bookmarks
            new(nameof(ISettingsManager.KeyToggleBookmarks), "KeyBinding_ToggleBookmarks", "KeyBinding_Category_Bookmarks"),
            new(nameof(ISettingsManager.KeyAddBookmark), "KeyBinding_AddBookmark", "KeyBinding_Category_Bookmarks"),

            // Zoom
            new(nameof(ISettingsManager.KeyZoomIn), "KeyBinding_ZoomIn", "KeyBinding_Category_Zoom"),
            new(nameof(ISettingsManager.KeyZoomOut), "KeyBinding_ZoomOut", "KeyBinding_Category_Zoom"),
            new(nameof(ISettingsManager.KeyZoomReset), "KeyBinding_ZoomReset", "KeyBinding_Category_Zoom"),
            new(nameof(ISettingsManager.KeyZoom100), "KeyBinding_Zoom100", "KeyBinding_Category_Zoom"),

            // Actions
            new(nameof(ISettingsManager.KeyRotateRight), "KeyBinding_RotateRight", "KeyBinding_Category_Action"),
            new(nameof(ISettingsManager.KeyRotateLeft), "KeyBinding_RotateLeft", "KeyBinding_Category_Action"),
            new(nameof(ISettingsManager.KeyFlipHorizontal), "KeyBinding_FlipHorizontal", "KeyBinding_Category_Action"),
            new(nameof(ISettingsManager.KeyCopyPath), "KeyBinding_CopyPath", "KeyBinding_Category_Action"),
            new(nameof(ISettingsManager.KeyDeleteFile), "KeyBinding_DeleteFile", "KeyBinding_Category_Action"),
            new(nameof(ISettingsManager.KeyRenameFile), "KeyBinding_RenameFile", "KeyBinding_Category_Action"),
            new(nameof(ISettingsManager.KeyMoveFile), "KeyBinding_MoveFile", "KeyBinding_Category_Action"),

            // Application
            new(nameof(ISettingsManager.KeyExit), "KeyBinding_Exit", "KeyBinding_Category_App")
        };

        private readonly Dictionary<string, KeyBindingData> _tempBindings = new();

        public SettingsOverlay(IMainView window, ISettingsManager settings, int initialTabIndex = 0)
        {
            this.InitializeComponent();
            _window = window;
            _settings = settings;

            _isInitializing = true;
            SliderQuality.Value = _settings.JpegQuality;
            ComboBackground.SelectedIndex = _settings.BackgroundColorMode >= 0 ? _settings.BackgroundColorMode : 0;
            CheckHighQuality.IsChecked = _settings.UseHighQualityScaling;
            CheckShowPageIndicator.IsChecked = _settings.ShowPageIndicator;
            CheckPanAnimation.IsChecked = _settings.EnablePanAnimation;
            SliderPanSpeed.IsEnabled = _settings.EnablePanAnimation;
            SliderPanSpeed.Value = _settings.PanAnimationSpeed;
            TxtPanSpeed.Text = $"{_settings.PanAnimationSpeed:F1}x";
            SliderVideoVolume.Value = (int)(_settings.VideoVolume * 100);
            ComboBoundary.SelectedIndex = _settings.BoundaryAction >= 0 ? _settings.BoundaryAction : 1;

            InitializeKeyBindingData();
            InitializeExtensionsList();
            InitializeKeyBindingsList();

            if (initialTabIndex >= 0 && initialTabIndex < SettingsPivot.Items.Count)
            {
                SettingsPivot.SelectedIndex = initialTabIndex;
            }

            _isInitializing = false;

            BtnClose.Focus(FocusState.Programmatic);
        }

        private void InitializeKeyBindingData()
        {
            _tempBindings.Clear();
            var type = typeof(ISettingsManager);
            foreach (var def in KeyBindingDefinitions)
            {
                var prop = type.GetProperty(def.PropertyName);
                if (prop != null && prop.GetValue(_settings) is KeyBindingData binding)
                {
                    _tempBindings[def.PropertyName] = binding.Clone();
                }
            }
        }

        private void SettingControl_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settings == null || _window == null) return;
            if (ReferenceEquals(sender, CheckPanAnimation))
            {
                SliderPanSpeed.IsEnabled = CheckPanAnimation.IsChecked ?? true;
            }
            ApplyTemporarySettings();
        }

        private void SliderVideoVolume_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_isInitializing || _settings == null || _window == null) return;
            ApplyTemporarySettings();
        }

        private void SliderPanSpeed_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (TxtPanSpeed != null)
            {
                TxtPanSpeed.Text = $"{e.NewValue:F1}x";
            }
            if (_isInitializing || _settings == null || _window == null) return;
            ApplyTemporarySettings();
        }

        private void ApplyTemporarySettings()
        {
            if (ComboBackground.SelectedIndex >= 0)
                _settings.BackgroundColorMode = ComboBackground.SelectedIndex;

            _settings.UseHighQualityScaling = CheckHighQuality.IsChecked ?? true;
            _settings.ShowPageIndicator = CheckShowPageIndicator.IsChecked ?? true;
            _settings.EnablePanAnimation = CheckPanAnimation.IsChecked ?? true;
            _settings.PanAnimationSpeed = SliderPanSpeed.Value;

            if (ComboBoundary.SelectedIndex >= 0)
                _settings.BoundaryAction = ComboBoundary.SelectedIndex;

            _settings.VideoVolume = SliderVideoVolume.Value / 100.0;

            _window.ViewModel.BoundaryAction = _settings.BoundaryAction;
            _window.ViewModel.ShowPageIndicator = _settings.ShowPageIndicator;
            _window.AppWindowManager.ApplyBackgroundSettings();
            _window.UpdatePageIndicator();
            _window.ViewerManager.UpdateStretch();
            _window.ViewerManager.UpdateVolume();
        }


        private void BtnGroupFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                var parts = tag.Split('_');
                if (parts.Length != 2) return;

                string group = parts[0];
                string action = parts[1];

                ObservableCollection<ExtensionItem>? targetList = group switch
                {
                    "Images" => _extensionsImages,
                    "Videos" => _extensionsVideos,
                    "Archives" => _extensionsArchives,
                    "Documents" => _extensionsDocuments,
                    _ => null
                };

                if (targetList != null)
                {
                    foreach (var item in targetList)
                    {
                        item.IsEnabled = (action == "All");
                    }
                    RefreshExtensionsUI();
                }
            }
        }

        private void RefreshExtensionsUI()
        {
            ItemsExtensionsImages.ItemsSource = null;
            ItemsExtensionsImages.ItemsSource = _extensionsImages;
            ItemsExtensionsVideos.ItemsSource = null;
            ItemsExtensionsVideos.ItemsSource = _extensionsVideos;
            ItemsExtensionsArchives.ItemsSource = null;
            ItemsExtensionsArchives.ItemsSource = _extensionsArchives;
            ItemsExtensionsDocuments.ItemsSource = _extensionsDocuments;
            ItemsExtensionsDocuments.ItemsSource = null;
            ItemsExtensionsDocuments.ItemsSource = _extensionsDocuments;
        }

        private void InitializeExtensionsList()
        {
            string[] videoExts = { ".webm", ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".flv" };

            _extensionsImages.Clear();
            _extensionsVideos.Clear();
            _extensionsArchives.Clear();
            _extensionsDocuments.Clear();

            var all = FolderDiscoveryService.SupportedExtensions.Concat(ArchiveManager.ArchiveExtensions).Append(".pdf").Distinct().OrderBy(e => e);

            foreach (var ext in all)
            {
                var item = new ExtensionItem
                {
                    Name = ext,
                    IsEnabled = _settings.EnabledExtensions.Contains(ext)
                };

                if (ext == ".pdf")
                {
                    _extensionsDocuments.Add(item);
                }
                else if (ArchiveManager.ArchiveExtensions.Contains(ext))
                {
                    _extensionsArchives.Add(item);
                }
                else if (videoExts.Contains(ext))
                {
                    _extensionsVideos.Add(item);
                }
                else
                {
                    _extensionsImages.Add(item);
                }
            }

            ItemsExtensionsImages.ItemsSource = _extensionsImages;
            ItemsExtensionsVideos.ItemsSource = _extensionsVideos;
            ItemsExtensionsArchives.ItemsSource = _extensionsArchives;
            ItemsExtensionsDocuments.ItemsSource = _extensionsDocuments;
            ItemsExtensionsDocuments.ItemsSource = null;
            ItemsExtensionsDocuments.ItemsSource = _extensionsDocuments;
        }

        private void InitializeKeyBindingsList()
        {
            BindingsStack.Children.Clear();
            string? currentCategory = null;

            foreach (var def in KeyBindingDefinitions)
            {
                if (def.CategoryResourceKey != currentCategory)
                {
                    currentCategory = def.CategoryResourceKey;
                    BindingsStack.Children.Add(CreateKeyHeader(GetString(currentCategory)));
                }

                if (_tempBindings.TryGetValue(def.PropertyName, out var binding))
                {
                    BindingsStack.Children.Add(CreateKeyRow(GetString(def.ResourceKey), binding));
                }
            }
        }

        private UIElement CreateKeyHeader(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 170, 170, 170)), // #AAAAAA
                Margin = new Thickness(0, 12, 0, 8)
            };
        }

        private UIElement CreateKeyRow(string label, KeyBindingData binding)
        {
            var grid = new Grid
            {
                Padding = new Thickness(12, 8, 12, 8),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(15, 255, 255, 255)),
                CornerRadius = new CornerRadius(8)
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var txtLabel = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 200, 200, 200))
            };
            Grid.SetColumn(txtLabel, 0);

            var btn = new Button
            {
                Content = GetBindingString(binding),
                MinWidth = 140,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(btn, 1);

            btn.Click += (s, e) =>
            {
                btn.Content = GetString("KeyBinding_PressShortcut");
                btn.Background = (SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            };

            btn.PreviewKeyDown += (s, e) =>
            {
                if (btn.Content.ToString() == GetString("KeyBinding_PressShortcut"))
                {
                    e.Handled = true;
                    var key = e.Key;
                    if (key == Windows.System.VirtualKey.Control || key == Windows.System.VirtualKey.Shift ||
                        key == Windows.System.VirtualKey.Menu || key == Windows.System.VirtualKey.LeftWindows ||
                        key == Windows.System.VirtualKey.RightWindows)
                        return;

                    binding.Key = key;
                    binding.Ctrl = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
                    binding.Shift = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
                    binding.Alt = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

                    btn.Content = GetBindingString(binding);
                    btn.ClearValue(Button.BackgroundProperty);
                }
            };

            grid.Children.Add(txtLabel);
            grid.Children.Add(btn);
            return grid;
        }

        private string GetBindingString(KeyBindingData b)
        {
            var parts = new List<string>();
            if (b.Ctrl) parts.Add("Ctrl");
            if (b.Shift) parts.Add("Shift");
            if (b.Alt) parts.Add("Alt");
            parts.Add(b.Key.ToString());
            return string.Join(" + ", parts);
        }

        private string GetString(string key) => _window.MenuStateManager.GetString(key);

        private void BtnResetKeyBindings_Click(object sender, RoutedEventArgs e)
        {
            var defaults = new SettingsData();
            var defaultsType = typeof(SettingsData);
            foreach (var def in KeyBindingDefinitions)
            {
                var prop = defaultsType.GetProperty(def.PropertyName);
                if (prop != null && prop.GetValue(defaults) is KeyBindingData defaultBinding)
                {
                    if (_tempBindings.TryGetValue(def.PropertyName, out var tempBinding))
                    {
                        CopyKeyBinding(defaultBinding, tempBinding);
                    }
                }
            }
            InitializeKeyBindingsList();
        }

        private void CopyKeyBinding(KeyBindingData src, KeyBindingData dest)
        {
            dest.Key = src.Key;
            dest.Ctrl = src.Ctrl;
            dest.Shift = src.Shift;
            dest.Alt = src.Alt;
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
                _isInitializing = true;
                SliderQuality.Value = _settings.JpegQuality;
                ComboBackground.SelectedIndex = _settings.BackgroundColorMode >= 0 ? _settings.BackgroundColorMode : 0;
                CheckHighQuality.IsChecked = _settings.UseHighQualityScaling;
                CheckShowPageIndicator.IsChecked = _settings.ShowPageIndicator;
                CheckPanAnimation.IsChecked = _settings.EnablePanAnimation;
                SliderPanSpeed.IsEnabled = _settings.EnablePanAnimation;
                SliderPanSpeed.Value = _settings.PanAnimationSpeed;
                TxtPanSpeed.Text = $"{_settings.PanAnimationSpeed:F1}x";
                SliderVideoVolume.Value = (int)(_settings.VideoVolume * 100);
                ComboBoundary.SelectedIndex = _settings.BoundaryAction >= 0 ? _settings.BoundaryAction : 1;

                InitializeKeyBindingData();
                InitializeExtensionsList();
                InitializeKeyBindingsList();
                _isInitializing = false;

                _window.AppWindowManager.ApplyBackgroundSettings();
                _window.UpdatePageIndicator();
                _window.ViewerManager.UpdateStretch();
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

            if (ComboBackground.SelectedIndex >= 0)
                _settings.BackgroundColorMode = ComboBackground.SelectedIndex;

            _settings.UseHighQualityScaling = CheckHighQuality.IsChecked ?? true;
            _settings.ShowPageIndicator = CheckShowPageIndicator.IsChecked ?? true;
            _settings.EnablePanAnimation = CheckPanAnimation.IsChecked ?? true;
            _settings.PanAnimationSpeed = SliderPanSpeed.Value;

            if (ComboBoundary.SelectedIndex >= 0)
                _settings.BoundaryAction = ComboBoundary.SelectedIndex;

            _settings.VideoVolume = SliderVideoVolume.Value / 100.0;

            var type = typeof(ISettingsManager);
            foreach (var def in KeyBindingDefinitions)
            {
                var prop = type.GetProperty(def.PropertyName);
                if (prop != null && prop.CanWrite && _tempBindings.TryGetValue(def.PropertyName, out var tempBinding))
                {
                    prop.SetValue(_settings, tempBinding);
                }
            }

            var newExts = _extensionsImages.Concat(_extensionsVideos).Concat(_extensionsArchives)
                .Where(i => i.IsEnabled)
                .Select(i => i.Name)
                .ToList();
            bool extChanged = !_settings.EnabledExtensions.SequenceEqual(newExts);
            _settings.EnabledExtensions = newExts;

            _settings.SaveSettings();

            _window.ViewModel.BoundaryAction = _settings.BoundaryAction;
            _window.ViewModel.ShowPageIndicator = _settings.ShowPageIndicator;

            _window.AppWindowManager.ApplyBackgroundSettings();
            _window.UpdatePageIndicator();
            _window.ViewerManager.UpdateStretch();

            if (extChanged)
            {
                // Refresh playlist if extensions changed
                if (!string.IsNullOrEmpty(_window.CurrentDirectory))
                {
                    WeakReferenceMessenger.Default.Send(new quick_image_viewer.ViewModels.LoadDirectoryMessage(_window.CurrentDirectory, _window.CurrentImagePath));
                }
            }

            var parent = this.Parent as Panel;
            parent?.Children.Remove(this);
            _window.IsDialogOpen = false;
        }
    }
}
