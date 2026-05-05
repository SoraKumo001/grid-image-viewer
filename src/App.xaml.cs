using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace grid_image_viewer
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
            Services = ConfigureServices();
            InitializeComponent();
        }

        public void SetMainView(IMainView view) => MainView = view;

        private static IServiceProvider ConfigureServices()
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
            services.AddSingleton<IEditorManager, EditorManager>();
            services.AddSingleton<IBookmarkManager, BookmarkManager>();
            services.AddSingleton<IPlaylistManager, PlaylistManager>();
            services.AddSingleton<IPrintService, grid_image_viewer.PrintService>();
            services.AddSingleton<IAnimationService, grid_image_viewer.AnimationService>();
            services.AddSingleton<IDialogService, grid_image_viewer.DialogService>();
            services.AddSingleton<INotificationService, grid_image_viewer.NotificationService>();
            services.AddSingleton<IMetadataDisplayService, grid_image_viewer.MetadataDisplayService>();
            services.AddSingleton<IImageEditService, ImageEditService>();
            services.AddSingleton<IInputHandler, InputHandler>();

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
                if (appArgs.Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.File)
                {
                    var fileArgs = appArgs.Data as Windows.ApplicationModel.Activation.IFileActivatedEventArgs;
                    if (fileArgs != null && fileArgs.Files.Count > 0)
                    {
                        string filePath = fileArgs.Files[0].Path;
                        _window.LoadDirectory(System.IO.Path.GetDirectoryName(filePath) ?? "", filePath);
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
    }
}
