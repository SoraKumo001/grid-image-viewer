using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Managers;
using quick_image_viewer.Services;
using System.Collections.Generic;
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
        private ObservableCollection<ExtensionItem> _extensionsImages = new();
        private ObservableCollection<ExtensionItem> _extensionsVideos = new();
        private ObservableCollection<ExtensionItem> _extensionsArchives = new();
        private bool _isInitializing = true;

        private KeyBindingData _tempNextImage = null!;
        private KeyBindingData _tempPrevImage = null!;
        private KeyBindingData _tempNextFolder = null!;
        private KeyBindingData _tempPrevFolder = null!;
        private KeyBindingData _tempToggleManga = null!;
        private KeyBindingData _tempExit = null!;
        private KeyBindingData _tempToggleGrid = null!;
        private KeyBindingData _tempSlideshow = null!;
        private KeyBindingData _tempMetadata = null!;
        private KeyBindingData _tempToggleBookmarks = null!;
        private KeyBindingData _tempToggleFullscreen = null!;
        private KeyBindingData _tempToggleStretchMode = null!;
        private KeyBindingData _tempAddBookmark = null!;
        private KeyBindingData _tempRotateRight = null!;
        private KeyBindingData _tempRotateLeft = null!;
        private KeyBindingData _tempFlipHorizontal = null!;
        private KeyBindingData _tempCopyPath = null!;
        private KeyBindingData _tempDeleteFile = null!;
        private KeyBindingData _tempZoomIn = null!;
        private KeyBindingData _tempZoomOut = null!;
        private KeyBindingData _tempZoomReset = null!;
        private KeyBindingData _tempZoom100 = null!;

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
            _tempNextImage = _settings.KeyNextImage.Clone();
            _tempPrevImage = _settings.KeyPrevImage.Clone();
            _tempNextFolder = _settings.KeyNextFolder.Clone();
            _tempPrevFolder = _settings.KeyPrevFolder.Clone();
            _tempToggleManga = _settings.KeyToggleManga.Clone();
            _tempExit = _settings.KeyExit.Clone();
            _tempToggleGrid = _settings.KeyToggleGrid.Clone();
            _tempSlideshow = _settings.KeySlideshow.Clone();
            _tempMetadata = _settings.KeyMetadata.Clone();
            _tempToggleBookmarks = _settings.KeyToggleBookmarks.Clone();
            _tempToggleFullscreen = _settings.KeyToggleFullscreen.Clone();
            _tempToggleStretchMode = _settings.KeyToggleStretchMode.Clone();
            _tempAddBookmark = _settings.KeyAddBookmark.Clone();
            _tempRotateRight = _settings.KeyRotateRight.Clone();
            _tempRotateLeft = _settings.KeyRotateLeft.Clone();
            _tempFlipHorizontal = _settings.KeyFlipHorizontal.Clone();
            _tempCopyPath = _settings.KeyCopyPath.Clone();
            _tempDeleteFile = _settings.KeyDeleteFile.Clone();
            _tempZoomIn = _settings.KeyZoomIn.Clone();
            _tempZoomOut = _settings.KeyZoomOut.Clone();
            _tempZoomReset = _settings.KeyZoomReset.Clone();
            _tempZoom100 = _settings.KeyZoom100.Clone();
        }

        private void SettingControl_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settings == null || _window == null) return;
            ApplyTemporarySettings();
        }

        private void ApplyTemporarySettings()
        {
            if (ComboBackground.SelectedIndex >= 0)
                _settings.BackgroundColorMode = ComboBackground.SelectedIndex;

            _settings.UseHighQualityScaling = CheckHighQuality.IsChecked ?? true;
            _settings.ShowPageIndicator = CheckShowPageIndicator.IsChecked ?? true;

            if (ComboBoundary.SelectedIndex >= 0)
                _settings.BoundaryAction = ComboBoundary.SelectedIndex;

            _window.ViewModel.BoundaryAction = _settings.BoundaryAction;
            _window.ViewModel.ShowPageIndicator = _settings.ShowPageIndicator;
            _window.AppWindowManager.ApplyBackgroundSettings();
            _window.UpdatePageIndicator();
            _window.ViewerManager.UpdateStretch();
        }

        private void BtnExtFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                string tag = btn.Content.ToString() ?? "";
                var allLists = new[] { _extensionsImages, _extensionsVideos, _extensionsArchives };

                foreach (var list in allLists)
                {
                    foreach (var item in list)
                    {
                        if (tag == "All")
                        {
                            item.IsEnabled = true;
                        }
                        else if (tag == "None")
                        {
                            item.IsEnabled = false;
                        }
                        else if (tag == "Images" && list == _extensionsImages)
                        {
                            item.IsEnabled = true;
                        }
                        else if (tag == "Videos" && list == _extensionsVideos)
                        {
                            item.IsEnabled = true;
                        }
                        else if (tag == "Archives" && list == _extensionsArchives)
                        {
                            item.IsEnabled = true;
                        }
                    }
                }

                // Refresh UI
                ItemsExtensionsImages.ItemsSource = null;
                ItemsExtensionsImages.ItemsSource = _extensionsImages;
                ItemsExtensionsVideos.ItemsSource = null;
                ItemsExtensionsVideos.ItemsSource = _extensionsVideos;
                ItemsExtensionsArchives.ItemsSource = null;
                ItemsExtensionsArchives.ItemsSource = _extensionsArchives;
            }
        }

        private void InitializeExtensionsList()
        {
            string[] videoExts = { ".webm", ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".flv" };

            _extensionsImages.Clear();
            _extensionsVideos.Clear();
            _extensionsArchives.Clear();

            var all = FolderDiscoveryService.SupportedExtensions.Concat(ArchiveManager.ArchiveExtensions).Distinct().OrderBy(e => e);

            foreach (var ext in all)
            {
                var item = new ExtensionItem
                {
                    Name = ext,
                    IsEnabled = _settings.EnabledExtensions.Contains(ext)
                };

                if (ArchiveManager.ArchiveExtensions.Contains(ext))
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
        }

        private void InitializeKeyBindingsList()
        {
            BindingsStack.Children.Clear();

            // Navigation
            BindingsStack.Children.Add(CreateKeyHeader(GetString("KeyBinding_Category_Navigation")));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_NextImage"), _tempNextImage));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_PrevImage"), _tempPrevImage));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_NextFolder"), _tempNextFolder));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_PrevFolder"), _tempPrevFolder));

            // View Modes
            BindingsStack.Children.Add(CreateKeyHeader(GetString("KeyBinding_Category_View")));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_ToggleManga"), _tempToggleManga));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_ToggleGrid"), _tempToggleGrid));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_ToggleSlideshow"), _tempSlideshow));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_Metadata"), _tempMetadata));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_ToggleFullscreen"), _tempToggleFullscreen));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_ToggleStretchMode"), _tempToggleStretchMode));

            // Bookmarks
            BindingsStack.Children.Add(CreateKeyHeader(GetString("KeyBinding_Category_Bookmarks")));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_ToggleBookmarks"), _tempToggleBookmarks));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_AddBookmark"), _tempAddBookmark));

            // Zoom
            BindingsStack.Children.Add(CreateKeyHeader(GetString("KeyBinding_Category_Zoom")));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_ZoomIn"), _tempZoomIn));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_ZoomOut"), _tempZoomOut));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_ZoomReset"), _tempZoomReset));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_Zoom100"), _tempZoom100));

            // Actions
            BindingsStack.Children.Add(CreateKeyHeader(GetString("KeyBinding_Category_Action")));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_RotateRight"), _tempRotateRight));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_RotateLeft"), _tempRotateLeft));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_FlipHorizontal"), _tempFlipHorizontal));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_CopyPath"), _tempCopyPath));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_DeleteFile"), _tempDeleteFile));

            // Application
            BindingsStack.Children.Add(CreateKeyHeader(GetString("KeyBinding_Category_App")));
            BindingsStack.Children.Add(CreateKeyRow(GetString("KeyBinding_Exit"), _tempExit));
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
                btn.Content = "...";
                btn.Background = (SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            };

            btn.PreviewKeyDown += (s, e) =>
            {
                if (btn.Content.ToString() == "...")
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
            CopyKeyBinding(defaults.KeyNextImage, _tempNextImage);
            CopyKeyBinding(defaults.KeyPrevImage, _tempPrevImage);
            CopyKeyBinding(defaults.KeyNextFolder, _tempNextFolder);
            CopyKeyBinding(defaults.KeyPrevFolder, _tempPrevFolder);
            CopyKeyBinding(defaults.KeyToggleManga, _tempToggleManga);
            CopyKeyBinding(defaults.KeyExit, _tempExit);
            CopyKeyBinding(defaults.KeyToggleGrid, _tempToggleGrid);
            CopyKeyBinding(defaults.KeySlideshow, _tempSlideshow);
            CopyKeyBinding(defaults.KeyMetadata, _tempMetadata);
            CopyKeyBinding(defaults.KeyToggleBookmarks, _tempToggleBookmarks);
            CopyKeyBinding(defaults.KeyToggleFullscreen, _tempToggleFullscreen);
            CopyKeyBinding(defaults.KeyToggleStretchMode, _tempToggleStretchMode);
            CopyKeyBinding(defaults.KeyAddBookmark, _tempAddBookmark);
            CopyKeyBinding(defaults.KeyRotateRight, _tempRotateRight);
            CopyKeyBinding(defaults.KeyRotateLeft, _tempRotateLeft);
            CopyKeyBinding(defaults.KeyFlipHorizontal, _tempFlipHorizontal);
            CopyKeyBinding(defaults.KeyCopyPath, _tempCopyPath);
            CopyKeyBinding(defaults.KeyDeleteFile, _tempDeleteFile);
            CopyKeyBinding(defaults.KeyZoomIn, _tempZoomIn);
            CopyKeyBinding(defaults.KeyZoomOut, _tempZoomOut);
            CopyKeyBinding(defaults.KeyZoomReset, _tempZoomReset);
            CopyKeyBinding(defaults.KeyZoom100, _tempZoom100);
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

            if (ComboBoundary.SelectedIndex >= 0)
                _settings.BoundaryAction = ComboBoundary.SelectedIndex;

            _settings.KeyNextImage = _tempNextImage;
            _settings.KeyPrevImage = _tempPrevImage;
            _settings.KeyNextFolder = _tempNextFolder;
            _settings.KeyPrevFolder = _tempPrevFolder;
            _settings.KeyToggleManga = _tempToggleManga;
            _settings.KeyExit = _tempExit;
            _settings.KeyToggleGrid = _tempToggleGrid;
            _settings.KeySlideshow = _tempSlideshow;
            _settings.KeyMetadata = _tempMetadata;
            _settings.KeyToggleBookmarks = _tempToggleBookmarks;
            _settings.KeyToggleFullscreen = _tempToggleFullscreen;
            _settings.KeyToggleStretchMode = _tempToggleStretchMode;
            _settings.KeyAddBookmark = _tempAddBookmark;
            _settings.KeyRotateRight = _tempRotateRight;
            _settings.KeyRotateLeft = _tempRotateLeft;
            _settings.KeyFlipHorizontal = _tempFlipHorizontal;
            _settings.KeyCopyPath = _tempCopyPath;
            _settings.KeyDeleteFile = _tempDeleteFile;
            _settings.KeyZoomIn = _tempZoomIn;
            _settings.KeyZoomOut = _tempZoomOut;
            _settings.KeyZoomReset = _tempZoomReset;
            _settings.KeyZoom100 = _tempZoom100;

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