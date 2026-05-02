using Microsoft.UI.Windowing;
using System;
using System.IO;
using System.Text.Json;
using Windows.System;

namespace grid_image_viewer
{
    public class SettingsData
    {
        public VirtualKey KeyNextImage { get; set; } = VirtualKey.Space;
        public VirtualKey KeyPrevImage { get; set; } = VirtualKey.Back;
        public VirtualKey KeyNextFolder { get; set; } = VirtualKey.Down;
        public VirtualKey KeyPrevFolder { get; set; } = VirtualKey.Up;
        public VirtualKey KeyToggleManga { get; set; } = VirtualKey.G;
        public VirtualKey KeyExit { get; set; } = VirtualKey.Escape;
        public VirtualKey KeyToggleGrid { get; set; } = VirtualKey.Enter;
        public VirtualKey KeySlideshow { get; set; } = VirtualKey.A;

        public int MangaSplitCount { get; set; } = 1; // 1, 2, or 4
        public int QuadLayoutMode { get; set; } = 0; // 0: Auto, 1: Horizontal, 2: 2x2 Grid
        public bool SlideshowFullscreen { get; set; } = false;
        public bool SlideshowRandom { get; set; } = true;
        public bool SlideshowLoop { get; set; } = true;
        public bool SlideshowNextFolder { get; set; } = false;
        public bool SlideshowCurrentFolderOnly { get; set; } = false;
        public double SlideshowInterval { get; set; } = 2.0;

        public int WindowWidth { get; set; } = -1;
        public int WindowHeight { get; set; } = -1;
        public int WindowX { get; set; } = -1;
        public int WindowY { get; set; } = -1;
        
        public string LastImagePath { get; set; } = string.Empty;
    }

    public class SettingsManager
    {
        private readonly string _settingsFilePath;
        private SettingsData _data;

        public VirtualKey KeyNextImage { get => _data.KeyNextImage; set => _data.KeyNextImage = value; }
        public VirtualKey KeyPrevImage { get => _data.KeyPrevImage; set => _data.KeyPrevImage = value; }
        public VirtualKey KeyNextFolder { get => _data.KeyNextFolder; set => _data.KeyNextFolder = value; }
        public VirtualKey KeyPrevFolder { get => _data.KeyPrevFolder; set => _data.KeyPrevFolder = value; }
        public VirtualKey KeyToggleManga { get => _data.KeyToggleManga; set => _data.KeyToggleManga = value; }
        public VirtualKey KeyExit { get => _data.KeyExit; set => _data.KeyExit = value; }
        public VirtualKey KeyToggleGrid { get => _data.KeyToggleGrid; set => _data.KeyToggleGrid = value; }
        public VirtualKey KeySlideshow { get => _data.KeySlideshow; set => _data.KeySlideshow = value; }

        public int MangaSplitCount { get => _data.MangaSplitCount; set => _data.MangaSplitCount = value; }
        public int QuadLayoutMode { get => _data.QuadLayoutMode; set => _data.QuadLayoutMode = value; }
        
        public bool SlideshowFullscreen { get => _data.SlideshowFullscreen; set => _data.SlideshowFullscreen = value; }
        public bool SlideshowRandom { get => _data.SlideshowRandom; set => _data.SlideshowRandom = value; }
        public bool SlideshowLoop { get => _data.SlideshowLoop; set => _data.SlideshowLoop = value; }
        public bool SlideshowNextFolder { get => _data.SlideshowNextFolder; set => _data.SlideshowNextFolder = value; }
        public bool SlideshowCurrentFolderOnly { get => _data.SlideshowCurrentFolderOnly; set => _data.SlideshowCurrentFolderOnly = value; }
        public double SlideshowInterval { get => _data.SlideshowInterval; set => _data.SlideshowInterval = value; }

        public string LastImagePath { get => _data.LastImagePath; }

        public SettingsManager()
        {
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appDataFolder, "grid-image-viewer");
            Directory.CreateDirectory(appFolder);
            _settingsFilePath = Path.Combine(appFolder, "settings.json");
            
            _data = new SettingsData();
            LoadSettings();
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    _data = JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
                }
            }
            catch
            {
                _data = new SettingsData();
            }
        }

        private void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch { }
        }

        public void SaveKeyBindings() => Save();
        public void SaveMangaMode() => Save();
        public void SaveSlideshowSettings() => Save();

        public void LoadWindowState(AppWindow appWindow)
        {
            if (_data.WindowWidth > 0 && _data.WindowHeight > 0)
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32(_data.WindowWidth, _data.WindowHeight));
            }
            
            if (_data.WindowX != -1 && _data.WindowY != -1)
            {
                appWindow.Move(new Windows.Graphics.PointInt32(_data.WindowX, _data.WindowY));
            }
        }

        public void SaveWindowState(AppWindow appWindow, string currentImagePath)
        {
            if (appWindow.Presenter.Kind == AppWindowPresenterKind.Default)
            {
                _data.WindowWidth = appWindow.Size.Width;
                _data.WindowHeight = appWindow.Size.Height;
                _data.WindowX = appWindow.Position.X;
                _data.WindowY = appWindow.Position.Y;
            }

            if (!string.IsNullOrEmpty(currentImagePath))
            {
                _data.LastImagePath = currentImagePath;
            }

            Save();
        }
    }
}
