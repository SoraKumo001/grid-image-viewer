using quick_image_viewer.Common;
using quick_image_viewer.Interfaces;
using System;
namespace quick_image_viewer.Managers
{
    internal class AppWindowManager : IAppWindowManager
    {
        private readonly IAppWindowHost _window;
        private readonly ISettingsManager _settings;

        public AppWindowManager(IAppWindowHost window, ISettingsManager settings)
        {
            _window = window;
            _settings = settings;
        }

        public void InitializeWindow()
        {
            _window.ExtendsContentIntoTitleBar = true;
            _window.SetTitleBar(_window.AppTitleBar);

            ApplyBackgroundSettings();

            var hwnd = _window.WindowHandle;
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            _settings.LoadWindowState(appWindow);
            try
            {
                var iconPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "AppIcon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    appWindow.SetIcon(iconPath);
                }
                else
                {
                    var fallbackPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "..", "Assets", "AppIcon.ico");
                    if (!System.IO.File.Exists(fallbackPath))
                    {
                        fallbackPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Assets", "AppIcon.ico");
                    }
                    if (System.IO.File.Exists(fallbackPath)) appWindow.SetIcon(fallbackPath);
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("AppWindowManager", "SetIcon failed", ex);
            }

            if (Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = appWindow.TitleBar;
                titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 255, 255, 255);
                titleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(51, 255, 255, 255);
                titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 128, 128, 128);
                titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            }
        }

        public void ApplyBackgroundSettings()
        {
            int mode = _settings.BackgroundColorMode;
            var theme = mode == 2 ? Microsoft.UI.Xaml.ElementTheme.Light : Microsoft.UI.Xaml.ElementTheme.Dark;
            _window.RootGrid.RequestedTheme = theme;

            if (mode == 1) // Black
            {
                _window.RootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));
            }
            else if (mode == 2) // White
            {
                _window.RootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
            }
            else // System (Mica)
            {
                _window.RootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            }

            // Apply to TitleBar
            if (theme == Microsoft.UI.Xaml.ElementTheme.Light)
            {
                _window.AppTitleBar.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 243, 243));
                _window.AppTitleBar.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 215, 215, 215));
            }
            else
            {
                _window.AppTitleBar.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 36, 36, 36));
                _window.AppTitleBar.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 51, 51, 51));
            }
        }

        public void SaveWindowState()
        {
            var hwnd = _window.WindowHandle;
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            _settings.SaveWindowState(appWindow, _window.CurrentImagePath, _window.CurrentDirectory);
        }
    }
}
