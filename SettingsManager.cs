using Microsoft.UI.Windowing;
using System;
using Windows.Storage;
using Windows.System;

namespace grid_image_viewer
{
    public class SettingsManager
    {
        public VirtualKey KeyNextImage { get; set; } = VirtualKey.Space;
        public VirtualKey KeyPrevImage { get; set; } = VirtualKey.Back;
        public VirtualKey KeyNextFolder { get; set; } = VirtualKey.Down;
        public VirtualKey KeyPrevFolder { get; set; } = VirtualKey.Up;
        public VirtualKey KeyToggleManga { get; set; } = VirtualKey.G;
        public VirtualKey KeyExit { get; set; } = VirtualKey.Escape;
        public VirtualKey KeyToggleGrid { get; set; } = VirtualKey.Enter;
        public VirtualKey KeySlideshow { get; set; } = VirtualKey.A;

        public bool IsMangaMode { get; set; } = false;
        
        public bool SlideshowFullscreen { get; set; } = false;
        public bool SlideshowRandom { get; set; } = true;
        public bool SlideshowLoop { get; set; } = true;
        public bool SlideshowNextFolder { get; set; } = false;
        public bool SlideshowCurrentFolderOnly { get; set; } = false;
        public double SlideshowInterval { get; set; } = 2.0;

        public SettingsManager()
        {
            LoadSettings();
        }

        public void LoadSettings()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            
            if (settings.TryGetValue("IsMangaMode", out object? obj) && obj is bool isMangaMode)
                IsMangaMode = isMangaMode;

            if (settings.TryGetValue("Key_NextImage", out object? nextImg)) KeyNextImage = (VirtualKey)(int)nextImg;
            if (settings.TryGetValue("Key_PrevImage", out object? prevImg)) KeyPrevImage = (VirtualKey)(int)prevImg;
            if (settings.TryGetValue("Key_NextFolder", out object? nextFld)) KeyNextFolder = (VirtualKey)(int)nextFld;
            if (settings.TryGetValue("Key_PrevFolder", out object? prevFld)) KeyPrevFolder = (VirtualKey)(int)prevFld;
            if (settings.TryGetValue("Key_ToggleManga", out object? tglManga)) KeyToggleManga = (VirtualKey)(int)tglManga;
            if (settings.TryGetValue("Key_Exit", out object? exitApp)) KeyExit = (VirtualKey)(int)exitApp;
            if (settings.TryGetValue("Key_ToggleGrid", out object? tglGrid)) KeyToggleGrid = (VirtualKey)(int)tglGrid;
            if (settings.TryGetValue("Key_Slideshow", out object? slideshowKey)) KeySlideshow = (VirtualKey)(int)slideshowKey;

            if (settings.TryGetValue("SlideshowFullscreen", out object? ssFs)) SlideshowFullscreen = (bool)ssFs;
            if (settings.TryGetValue("SlideshowRandom", out object? ssRnd)) SlideshowRandom = (bool)ssRnd;
            if (settings.TryGetValue("SlideshowLoop", out object? ssLoop)) SlideshowLoop = (bool)ssLoop;
            if (settings.TryGetValue("SlideshowNextFolder", out object? ssNext)) SlideshowNextFolder = (bool)ssNext;
            if (settings.TryGetValue("SlideshowCurrentFolderOnly", out object? ssCurr)) SlideshowCurrentFolderOnly = (bool)ssCurr;
            if (settings.TryGetValue("SlideshowInterval", out object? ssInt)) SlideshowInterval = (double)ssInt;
        }

        public void SaveKeyBindings()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            settings["Key_NextImage"] = (int)KeyNextImage;
            settings["Key_PrevImage"] = (int)KeyPrevImage;
            settings["Key_NextFolder"] = (int)KeyNextFolder;
            settings["Key_PrevFolder"] = (int)KeyPrevFolder;
            settings["Key_ToggleManga"] = (int)KeyToggleManga;
            settings["Key_Exit"] = (int)KeyExit;
            settings["Key_ToggleGrid"] = (int)KeyToggleGrid;
            settings["Key_Slideshow"] = (int)KeySlideshow;
        }

        public void SaveMangaMode()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            settings["IsMangaMode"] = IsMangaMode;
        }

        public void SaveSlideshowSettings()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            settings["SlideshowFullscreen"] = SlideshowFullscreen;
            settings["SlideshowRandom"] = SlideshowRandom;
            settings["SlideshowLoop"] = SlideshowLoop;
            settings["SlideshowNextFolder"] = SlideshowNextFolder;
            settings["SlideshowCurrentFolderOnly"] = SlideshowCurrentFolderOnly;
            settings["SlideshowInterval"] = SlideshowInterval;
        }

        public void LoadWindowState(AppWindow appWindow)
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            if (settings.TryGetValue("WindowWidth", out object? widthObj) && widthObj is int width &&
                settings.TryGetValue("WindowHeight", out object? heightObj) && heightObj is int height)
            {
                if (width > 0 && height > 0)
                {
                    appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
                }
            }
            
            if (settings.TryGetValue("WindowX", out object? xObj) && xObj is int x &&
                settings.TryGetValue("WindowY", out object? yObj) && yObj is int y)
            {
                appWindow.Move(new Windows.Graphics.PointInt32(x, y));
            }
        }

        public void SaveWindowState(AppWindow appWindow, string currentImagePath)
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            
            if (appWindow.Presenter.Kind == AppWindowPresenterKind.Default)
            {
                settings["WindowWidth"] = appWindow.Size.Width;
                settings["WindowHeight"] = appWindow.Size.Height;
                settings["WindowX"] = appWindow.Position.X;
                settings["WindowY"] = appWindow.Position.Y;
            }

            if (!string.IsNullOrEmpty(currentImagePath))
            {
                settings["LastImagePath"] = currentImagePath;
            }
        }
    }
}
