using quick_image_viewer.Managers;
using System.Collections.Generic;
namespace quick_image_viewer.Interfaces
{
    public interface ISettingsManager
    {
        int MangaSplitCount { get; set; }
        int QuadLayoutMode { get; set; }
        bool SlideshowFullscreen { get; set; }
        bool SlideshowRandom { get; set; }
        bool SlideshowLoop { get; set; }
        bool SlideshowNextFolder { get; set; }
        bool SlideshowIncludeSiblings { get; set; }
        bool SlideshowCurrentFolderOnly { get; set; }
        bool SlideshowUniformToFill { get; set; }
        double SlideshowInterval { get; set; }
        bool SlideshowCrossfade { get; set; }
        double SlideshowCrossfadeDuration { get; set; }
        int ImageStretchMode { get; set; }
        int BackgroundColorMode { get; set; }
        bool UseHighQualityScaling { get; set; }
        bool ShowPageIndicator { get; set; }
        double VideoVolume { get; set; }
        int BoundaryAction { get; set; }
        int JpegQuality { get; set; }
        List<string> EnabledExtensions { get; set; }

        string LastImagePath { get; }
        string LastDirectoryPath { get; }
        List<BookmarkItem> Bookmarks { get; }

        KeyBindingData KeyNextImage { get; set; }
        KeyBindingData KeyPrevImage { get; set; }
        KeyBindingData KeyNextFolder { get; set; }
        KeyBindingData KeyPrevFolder { get; set; }
        KeyBindingData KeyToggleManga { get; set; }
        KeyBindingData KeyExit { get; set; }
        KeyBindingData KeyToggleGrid { get; set; }
        KeyBindingData KeySlideshow { get; set; }
        KeyBindingData KeyMetadata { get; set; }
        KeyBindingData KeyToggleBookmarks { get; set; }
        KeyBindingData KeyToggleFullscreen { get; set; }
        KeyBindingData KeyToggleStretchMode { get; set; }
        KeyBindingData KeyAddBookmark { get; set; }
        KeyBindingData KeyRotateRight { get; set; }
        KeyBindingData KeyRotateLeft { get; set; }
        KeyBindingData KeyFlipHorizontal { get; set; }
        KeyBindingData KeyCopyPath { get; set; }
        KeyBindingData KeyDeleteFile { get; set; }
        KeyBindingData KeyZoomIn { get; set; }
        KeyBindingData KeyZoomOut { get; set; }
        KeyBindingData KeyZoomReset { get; set; }
        KeyBindingData KeyZoom100 { get; set; }

        void SaveSettings();
        void LoadSettings();
        void SaveMangaMode();
        void SaveSlideshowSettings();
        void SaveKeyBindings();
        bool ToggleBookmark(string path, bool isFolder);
        void LoadWindowState(Microsoft.UI.Windowing.AppWindow appWindow);
        void SaveWindowState(Microsoft.UI.Windowing.AppWindow appWindow, string currentImagePath, string currentDirectory);
        System.Threading.Tasks.Task ExportSettingsAsync(System.IntPtr windowHandle);
        System.Threading.Tasks.Task<bool> ImportSettingsAsync(System.IntPtr windowHandle);
        string GetString(string key);
    }
}
