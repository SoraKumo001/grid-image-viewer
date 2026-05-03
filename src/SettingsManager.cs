using Microsoft.UI.Windowing;
using System;
using System.IO;
using System.Text.Json;
using Windows.System;

namespace grid_image_viewer
{
    public class KeyBindingData
    {
        public VirtualKey Key { get; set; }
        public bool Ctrl { get; set; }
        public bool Shift { get; set; }
        public bool Alt { get; set; }

        public KeyBindingData() { }
        public KeyBindingData(VirtualKey key, bool ctrl = false, bool shift = false, bool alt = false)
        {
            Key = key;
            Ctrl = ctrl;
            Shift = shift;
            Alt = alt;
        }

        public KeyBindingData Clone() => new KeyBindingData(Key, Ctrl, Shift, Alt);
    }

    public class BookmarkItem
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsFolder { get; set; } = true;
    }





    public class SettingsData
    {
        public KeyBindingData KeyNextImage { get; set; } = new KeyBindingData(VirtualKey.Space);
        public KeyBindingData KeyPrevImage { get; set; } = new KeyBindingData(VirtualKey.Back);
        public KeyBindingData KeyNextFolder { get; set; } = new KeyBindingData(VirtualKey.Down);
        public KeyBindingData KeyPrevFolder { get; set; } = new KeyBindingData(VirtualKey.Up);
        public KeyBindingData KeyToggleManga { get; set; } = new KeyBindingData(VirtualKey.G, ctrl: true);
        public KeyBindingData KeyExit { get; set; } = new KeyBindingData(VirtualKey.Escape);
        public KeyBindingData KeyToggleGrid { get; set; } = new KeyBindingData(VirtualKey.Enter);
        public KeyBindingData KeySlideshow { get; set; } = new KeyBindingData(VirtualKey.A);
        public KeyBindingData KeyMetadata { get; set; } = new KeyBindingData(VirtualKey.I);
        public KeyBindingData KeyToggleBookmarks { get; set; } = new KeyBindingData(VirtualKey.B);

        // Legacy properties for migration
        public VirtualKey? LegacyKeyNextImage { get; set; }
        public VirtualKey? LegacyKeyPrevImage { get; set; }
        public VirtualKey? LegacyKeyNextFolder { get; set; }
        public VirtualKey? LegacyKeyPrevFolder { get; set; }
        public VirtualKey? LegacyKeyToggleManga { get; set; }
        public VirtualKey? LegacyKeyExit { get; set; }
        public VirtualKey? LegacyKeyToggleGrid { get; set; }
        public VirtualKey? LegacyKeySlideshow { get; set; }

        public int MangaSplitCount { get; set; } = 1; // 1, 2, or 4
        public int QuadLayoutMode { get; set; } = 0; // 0: Auto, 1: Horizontal, 2: 2x2 Grid
        public bool SlideshowFullscreen { get; set; } = false;
        public bool SlideshowRandom { get; set; } = true;
        public bool SlideshowLoop { get; set; } = true;
        public bool SlideshowNextFolder { get; set; } = false;
        public bool SlideshowIncludeSiblings { get; set; } = false;
        public bool SlideshowCurrentFolderOnly { get; set; } = false;
        public bool SlideshowUniformToFill { get; set; } = false;
        public double SlideshowInterval { get; set; } = 2.0;
        public bool SlideshowCrossfade { get; set; } = true;
        public double SlideshowCrossfadeDuration { get; set; } = 0.4;
        public int ImageStretchMode { get; set; } = 2; // 0: None, 2: Uniform (Contain), 3: UniformToFill (Cover)

        public int JpegQuality { get; set; } = 90;

        public int WindowWidth { get; set; } = -1;
        public int WindowHeight { get; set; } = -1;
        public int WindowX { get; set; } = -1;
        public int WindowY { get; set; } = -1;

        public string LastImagePath { get; set; } = string.Empty;
        public System.Collections.Generic.List<BookmarkItem> Bookmarks { get; set; } = new System.Collections.Generic.List<BookmarkItem>();
    }

    public class SettingsManager
    {
        private readonly string _settingsFilePath;
        private SettingsData _data;

        public KeyBindingData KeyNextImage { get => _data.KeyNextImage; set => _data.KeyNextImage = value; }
        public KeyBindingData KeyPrevImage { get => _data.KeyPrevImage; set => _data.KeyPrevImage = value; }
        public KeyBindingData KeyNextFolder { get => _data.KeyNextFolder; set => _data.KeyNextFolder = value; }
        public KeyBindingData KeyPrevFolder { get => _data.KeyPrevFolder; set => _data.KeyPrevFolder = value; }
        public KeyBindingData KeyToggleManga { get => _data.KeyToggleManga; set => _data.KeyToggleManga = value; }
        public KeyBindingData KeyExit { get => _data.KeyExit; set => _data.KeyExit = value; }
        public KeyBindingData KeyToggleGrid { get => _data.KeyToggleGrid; set => _data.KeyToggleGrid = value; }
        public KeyBindingData KeySlideshow { get => _data.KeySlideshow; set => _data.KeySlideshow = value; }
        public KeyBindingData KeyMetadata { get => _data.KeyMetadata; set => _data.KeyMetadata = value; }
        public KeyBindingData KeyToggleBookmarks { get => _data.KeyToggleBookmarks; set => _data.KeyToggleBookmarks = value; }

        public int MangaSplitCount { get => _data.MangaSplitCount; set => _data.MangaSplitCount = value; }
        public int QuadLayoutMode { get => _data.QuadLayoutMode; set => _data.QuadLayoutMode = value; }

        public bool SlideshowFullscreen { get => _data.SlideshowFullscreen; set => _data.SlideshowFullscreen = value; }
        public bool SlideshowRandom { get => _data.SlideshowRandom; set => _data.SlideshowRandom = value; }
        public bool SlideshowLoop { get => _data.SlideshowLoop; set => _data.SlideshowLoop = value; }
        public bool SlideshowNextFolder { get => _data.SlideshowNextFolder; set => _data.SlideshowNextFolder = value; }
        public bool SlideshowIncludeSiblings { get => _data.SlideshowIncludeSiblings; set => _data.SlideshowIncludeSiblings = value; }
        public bool SlideshowCurrentFolderOnly { get => _data.SlideshowCurrentFolderOnly; set => _data.SlideshowCurrentFolderOnly = value; }
        public bool SlideshowUniformToFill { get => _data.SlideshowUniformToFill; set => _data.SlideshowUniformToFill = value; }
        public double SlideshowInterval { get => _data.SlideshowInterval; set => _data.SlideshowInterval = value; }
        public bool SlideshowCrossfade { get => _data.SlideshowCrossfade; set => _data.SlideshowCrossfade = value; }
        public double SlideshowCrossfadeDuration { get => _data.SlideshowCrossfadeDuration; set => _data.SlideshowCrossfadeDuration = value; }
        public int ImageStretchMode { get => _data.ImageStretchMode; set => _data.ImageStretchMode = value; }

        public int JpegQuality { get => _data.JpegQuality; set => _data.JpegQuality = value; }

        public string LastImagePath { get => _data.LastImagePath; }
        public System.Collections.Generic.List<BookmarkItem> Bookmarks { get => _data.Bookmarks; }

        public SettingsManager()
        {
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appDataFolder, "quick-image-viewer");
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

                    // Migrate legacy key bindings
                    if (_data.LegacyKeyNextImage.HasValue) { _data.KeyNextImage = new KeyBindingData(_data.LegacyKeyNextImage.Value); _data.LegacyKeyNextImage = null; }
                    if (_data.LegacyKeyPrevImage.HasValue) { _data.KeyPrevImage = new KeyBindingData(_data.LegacyKeyPrevImage.Value); _data.LegacyKeyPrevImage = null; }
                    if (_data.LegacyKeyNextFolder.HasValue) { _data.KeyNextFolder = new KeyBindingData(_data.LegacyKeyNextFolder.Value); _data.LegacyKeyNextFolder = null; }
                    if (_data.LegacyKeyPrevFolder.HasValue) { _data.KeyPrevFolder = new KeyBindingData(_data.LegacyKeyPrevFolder.Value); _data.LegacyKeyPrevFolder = null; }
                    if (_data.LegacyKeyToggleManga.HasValue) { _data.KeyToggleManga = new KeyBindingData(_data.LegacyKeyToggleManga.Value, ctrl: true); _data.LegacyKeyToggleManga = null; }
                    if (_data.LegacyKeyExit.HasValue) { _data.KeyExit = new KeyBindingData(_data.LegacyKeyExit.Value); _data.LegacyKeyExit = null; }
                    if (_data.LegacyKeyToggleGrid.HasValue) { _data.KeyToggleGrid = new KeyBindingData(_data.LegacyKeyToggleGrid.Value); _data.LegacyKeyToggleGrid = null; }
                    if (_data.LegacyKeySlideshow.HasValue) { _data.KeySlideshow = new KeyBindingData(_data.LegacyKeySlideshow.Value); _data.LegacyKeySlideshow = null; }
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
        public void SaveSettings() => Save();

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

        public bool ToggleBookmark(string path, bool isFolder)
        {
            var existing = _data.Bookmarks.Find(b => b.Path == path);
            if (existing != null)
            {
                _data.Bookmarks.Remove(existing);
                Save();
                return false; // Removed
            }
            else
            {
                _data.Bookmarks.Add(new BookmarkItem
                {
                    Path = path,
                    Name = isFolder ? Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) : Path.GetFileName(path),
                    IsFolder = isFolder
                });
                Save();
                return true; // Added
            }
        }
    }
}
