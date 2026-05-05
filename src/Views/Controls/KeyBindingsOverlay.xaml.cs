using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Managers;
using System.Collections.Generic;
namespace quick_image_viewer.Views.Controls
{
    public sealed partial class KeyBindingsOverlay : UserControl
    {
        private readonly IMainView _window;
        private readonly ISettingsManager _settings;

        private KeyBindingData _tempNextImage;
        private KeyBindingData _tempPrevImage;
        private KeyBindingData _tempNextFolder;
        private KeyBindingData _tempPrevFolder;
        private KeyBindingData _tempToggleManga;
        private KeyBindingData _tempExit;
        private KeyBindingData _tempToggleGrid;
        private KeyBindingData _tempSlideshow;
        private KeyBindingData _tempMetadata;
        private KeyBindingData _tempToggleBookmarks;
        private KeyBindingData _tempToggleFullscreen;
        private KeyBindingData _tempToggleStretchMode;
        private KeyBindingData _tempAddBookmark;
        private KeyBindingData _tempRotateRight;
        private KeyBindingData _tempRotateLeft;
        private KeyBindingData _tempFlipHorizontal;
        private KeyBindingData _tempCopyPath;
        private KeyBindingData _tempDeleteFile;
        private KeyBindingData _tempZoomIn;
        private KeyBindingData _tempZoomOut;
        private KeyBindingData _tempZoomReset;
        private KeyBindingData _tempZoom100;

        public KeyBindingsOverlay(IMainView window, ISettingsManager settings)
        {
            this.InitializeComponent();
            _window = window;
            _settings = settings;

            // Clone current settings
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

            InitializeList();
        }

        private void InitializeList()
        {
            BindingsStack.Children.Clear();

            // Navigation
            BindingsStack.Children.Add(CreateHeader(GetString("KeyBinding_Category_Navigation")));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_NextImage"), _tempNextImage));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_PrevImage"), _tempPrevImage));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_NextFolder"), _tempNextFolder));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_PrevFolder"), _tempPrevFolder));

            // View Modes
            BindingsStack.Children.Add(CreateHeader(GetString("KeyBinding_Category_View")));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_ToggleManga"), _tempToggleManga));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_ToggleGrid"), _tempToggleGrid));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_ToggleSlideshow"), _tempSlideshow));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_Metadata"), _tempMetadata));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_ToggleFullscreen"), _tempToggleFullscreen));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_ToggleStretchMode"), _tempToggleStretchMode));

            // Bookmarks
            BindingsStack.Children.Add(CreateHeader(GetString("KeyBinding_Category_Bookmarks")));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_ToggleBookmarks"), _tempToggleBookmarks));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_AddBookmark"), _tempAddBookmark));

            // Zoom
            BindingsStack.Children.Add(CreateHeader(GetString("KeyBinding_Category_Zoom")));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_ZoomIn"), _tempZoomIn));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_ZoomOut"), _tempZoomOut));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_ZoomReset"), _tempZoomReset));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_Zoom100"), _tempZoom100));

            // Actions
            BindingsStack.Children.Add(CreateHeader(GetString("KeyBinding_Category_Action")));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_RotateRight"), _tempRotateRight));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_RotateLeft"), _tempRotateLeft));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_FlipHorizontal"), _tempFlipHorizontal));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_CopyPath"), _tempCopyPath));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_DeleteFile"), _tempDeleteFile));

            // Application
            BindingsStack.Children.Add(CreateHeader(GetString("KeyBinding_Category_App")));
            BindingsStack.Children.Add(CreateRow(GetString("KeyBinding_Exit"), _tempExit));
        }

        private UIElement CreateHeader(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = (SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                Margin = new Thickness(4, 16, 0, 4)
            };
        }

        private UIElement CreateRow(string label, KeyBindingData binding)
        {
            var grid = new Grid
            {
                Padding = new Thickness(12, 8, 12, 8),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(15, 255, 255, 255)),
                CornerRadius = new CornerRadius(8)
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var txtLabel = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 200, 200, 200))
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

        private string GetString(string key) => _window.EditorManager.GetString(key);

        private void BtnResetAll_Click(object sender, RoutedEventArgs e)
        {
            var defaults = new SettingsData();
            CopyBinding(defaults.KeyNextImage, _tempNextImage);
            CopyBinding(defaults.KeyPrevImage, _tempPrevImage);
            CopyBinding(defaults.KeyNextFolder, _tempNextFolder);
            CopyBinding(defaults.KeyPrevFolder, _tempPrevFolder);
            CopyBinding(defaults.KeyToggleManga, _tempToggleManga);
            CopyBinding(defaults.KeyExit, _tempExit);
            CopyBinding(defaults.KeyToggleGrid, _tempToggleGrid);
            CopyBinding(defaults.KeySlideshow, _tempSlideshow);
            CopyBinding(defaults.KeyMetadata, _tempMetadata);
            CopyBinding(defaults.KeyToggleBookmarks, _tempToggleBookmarks);
            CopyBinding(defaults.KeyToggleFullscreen, _tempToggleFullscreen);
            CopyBinding(defaults.KeyToggleStretchMode, _tempToggleStretchMode);
            CopyBinding(defaults.KeyAddBookmark, _tempAddBookmark);
            CopyBinding(defaults.KeyRotateRight, _tempRotateRight);
            CopyBinding(defaults.KeyRotateLeft, _tempRotateLeft);
            CopyBinding(defaults.KeyFlipHorizontal, _tempFlipHorizontal);
            CopyBinding(defaults.KeyCopyPath, _tempCopyPath);
            CopyBinding(defaults.KeyDeleteFile, _tempDeleteFile);
            CopyBinding(defaults.KeyZoomIn, _tempZoomIn);
            CopyBinding(defaults.KeyZoomOut, _tempZoomOut);
            CopyBinding(defaults.KeyZoomReset, _tempZoomReset);
            CopyBinding(defaults.KeyZoom100, _tempZoom100);
            InitializeList();
        }

        private void CopyBinding(KeyBindingData src, KeyBindingData dest)
        {
            dest.Key = src.Key;
            dest.Ctrl = src.Ctrl;
            dest.Shift = src.Shift;
            dest.Alt = src.Alt;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
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
            _settings.SaveKeyBindings();
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
        }

        private void Close()
        {
            var parent = this.Parent as Panel;
            parent?.Children.Remove(this);
            _window.IsDialogOpen = false;
        }
    }
}
