using quick_image_viewer.Interfaces;
namespace quick_image_viewer.Managers
{
    internal class AppWindowManager : IAppWindowManager
    {
        private readonly IMainView _window;
        private readonly ISettingsManager _settings;

        public AppWindowManager(IMainView window, ISettingsManager settings)
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
            catch { }

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
            if (mode == 1) _window.RootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));
            else if (mode == 2) _window.RootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
            else _window.RootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
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