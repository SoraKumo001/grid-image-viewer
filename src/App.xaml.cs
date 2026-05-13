using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Managers;
using quick_image_viewer.Services;
using quick_image_viewer.ViewModels;
using System;
using System.Threading.Tasks;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace quick_image_viewer
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private MainWindow? _window;
        public IServiceProvider Services { get; private set; }
        public IMainView? MainView { get; private set; }

        public App()
        {

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                e.SetObserved();
            };

            Services = ConfigureServices();
            InitializeComponent();
        }

        public void SetMainView(IMainView view) => MainView = view;

        private static ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Core
            services.AddSingleton<ISettingsManager, SettingsManager>();
            services.AddSingleton<IViewerStateService, ViewerStateService>();
            services.AddSingleton<IMainView>(s => ((App)Application.Current).MainView ?? throw new InvalidOperationException("MainView not initialized"));

            // ViewModel
            services.AddTransient<MainViewModel>();

            // Managers & Services
            services.AddSingleton<ISlideshowService, SlideshowService>();
            services.AddSingleton<ISlideshowManager, SlideshowManager>();
            services.AddSingleton<IViewerCacheManager, ViewerCacheManager>();
            services.AddSingleton<IViewerManager, ViewerManager>();
            services.AddSingleton<IGridManager, GridManager>();
            services.AddSingleton<IAppWindowManager, AppWindowManager>();
            services.AddSingleton<IMenuStateManager, MenuStateManager>();
            services.AddSingleton<IBookmarkManager, BookmarkManager>();
            services.AddSingleton<IPlaylistManager, PlaylistManager>();
            services.AddSingleton<IPrintService, PrintService>();
            services.AddSingleton<IAnimationService, AnimationService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<IMetadataDisplayService, MetadataDisplayService>();
            services.AddSingleton<IImageEditService, ImageEditService>();
            services.AddSingleton<IInputHandler, InputHandler>();
            services.AddSingleton<IFileOperationService, FileOperationService>();

            return services.BuildServiceProvider();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();

            bool fileLoaded = false;
            try
            {
                var appArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
                if (appArgs.Data is Windows.ApplicationModel.Activation.IFileActivatedEventArgs fileArgs && fileArgs.Files.Count > 0)
                {
                    string filePath = fileArgs.Files[0].Path;
                    LoadFile(filePath);
                    fileLoaded = true;
                }
                else if (appArgs.Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.Launch)
                {
                    // Fallback: Check command line arguments for file path
                    var args_list = Environment.GetCommandLineArgs();
                    if (args_list.Length > 1)
                    {
                        string filePath = args_list[1];
                        // If it's a file path and not just an option
                        if (System.IO.File.Exists(filePath) || System.IO.Directory.Exists(filePath))
                        {
                            LoadFile(filePath);
                            fileLoaded = true;
                        }
                    }
                }
            }
            catch
            {
                // Ignore activation errors to prevent app crash
            }

            if (!fileLoaded)
            {
                var settings = Services.GetRequiredService<ISettingsManager>();
                string lastImagePath = settings.LastImagePath;
                string lastDirectoryPath = settings.LastDirectoryPath;

                if (!string.IsNullOrEmpty(lastImagePath))
                {
                    if (ArchiveManager.IsArchivePath(lastImagePath))
                    {
                        var (archivePath, _) = ArchiveManager.SplitArchivePath(lastImagePath);
                        if (System.IO.File.Exists(archivePath))
                        {
                            _window.LoadDirectory(archivePath, lastImagePath);
                            fileLoaded = true;
                        }
                    }
                    else if (PdfManager.IsVirtualPath(lastImagePath))
                    {
                        var (actualPath, _) = PdfManager.SplitVirtualPath(lastImagePath);
                        if (System.IO.File.Exists(actualPath))
                        {
                            _window.LoadDirectory(actualPath, lastImagePath);
                            fileLoaded = true;
                        }
                    }
                    else if (System.IO.File.Exists(lastImagePath))
                    {
                        _window.LoadDirectory(System.IO.Path.GetDirectoryName(lastImagePath) ?? "", lastImagePath);
                        fileLoaded = true;
                    }
                }

                if (!fileLoaded && !string.IsNullOrEmpty(lastDirectoryPath))
                {
                    if (ArchiveManager.IsArchive(lastDirectoryPath))
                    {
                        if (System.IO.File.Exists(lastDirectoryPath))
                        {
                            _window.LoadDirectory(lastDirectoryPath);
                        }
                    }
                    else if (System.IO.Directory.Exists(lastDirectoryPath))
                    {
                        _window.LoadDirectory(lastDirectoryPath);
                    }
                }
            }
        }
        private void LoadFile(string filePath)
        {
            if (_window == null) return;

            if (ArchiveManager.IsArchive(filePath) || PdfManager.IsPdfPath(filePath))
            {
                _window.LoadDirectory(filePath);
            }
            else
            {
                _window.LoadDirectory(System.IO.Path.GetDirectoryName(filePath) ?? "", filePath);
            }
        }
    }
}

