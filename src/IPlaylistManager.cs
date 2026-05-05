using System.Collections.Generic;

namespace grid_image_viewer
{
    public interface IPlaylistManager
    {
        void Navigate(int offset, bool forceSingleStep);
        void NavigateFolder(int offset);
        void LoadDirectory(string path, string initialFile = "", bool includeSiblings = false, bool includeSubfolders = false, List<string>? preloadedPlaylist = null);
    }
}
