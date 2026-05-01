using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace grid_image_viewer
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();

            bool fileLoaded = false;
            try
            {
                var appArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
                if (appArgs.Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.File)
                {
                    var fileArgs = appArgs.Data as Windows.ApplicationModel.Activation.IFileActivatedEventArgs;
                    if (fileArgs != null && fileArgs.Files.Count > 0)
                    {
                        string filePath = fileArgs.Files[0].Path;
                        ((MainWindow)_window).LoadDirectory(System.IO.Path.GetDirectoryName(filePath) ?? "", filePath);
                        fileLoaded = true;
                    }
                }
            }
            catch
            {
                // Ignore activation errors to prevent app crash
            }

            if (!fileLoaded)
            {
                var settings = new SettingsManager();
                string lastImagePath = settings.LastImagePath;
                if (!string.IsNullOrEmpty(lastImagePath) && System.IO.File.Exists(lastImagePath))
                {
                    ((MainWindow)_window).LoadDirectory(System.IO.Path.GetDirectoryName(lastImagePath) ?? "", lastImagePath);
                }
            }
        }
    }
}
