using Microsoft.UI.Xaml;

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
                string lastDirectoryPath = settings.LastDirectoryPath;

                if (!string.IsNullOrEmpty(lastImagePath))
                {
                    if (ArchiveManager.IsArchivePath(lastImagePath))
                    {
                        var (archivePath, _) = ArchiveManager.SplitArchivePath(lastImagePath);
                        if (System.IO.File.Exists(archivePath))
                        {
                            ((MainWindow)_window).LoadDirectory(archivePath, lastImagePath);
                            fileLoaded = true;
                        }
                    }
                    else if (System.IO.File.Exists(lastImagePath))
                    {
                        ((MainWindow)_window).LoadDirectory(System.IO.Path.GetDirectoryName(lastImagePath) ?? "", lastImagePath);
                        fileLoaded = true;
                    }
                }

                if (!fileLoaded && !string.IsNullOrEmpty(lastDirectoryPath))
                {
                    if (ArchiveManager.IsArchive(lastDirectoryPath))
                    {
                        if (System.IO.File.Exists(lastDirectoryPath))
                        {
                            ((MainWindow)_window).LoadDirectory(lastDirectoryPath);
                        }
                    }
                    else if (System.IO.Directory.Exists(lastDirectoryPath))
                    {
                        ((MainWindow)_window).LoadDirectory(lastDirectoryPath);
                    }
                }
            }
        }
    }
}
