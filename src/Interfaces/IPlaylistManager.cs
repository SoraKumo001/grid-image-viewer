using System.Collections.Generic;
namespace quick_image_viewer.Interfaces
{
    public interface IPlaylistManager
    {
        void Navigate(int offset, bool forceSingleStep);
        void NavigateFolder(int offset);
        void LoadDirectory(string path, string initialFile = "", bool includeSiblings = false, bool includeSubfolders = false, List<string>? preloadedPlaylist = null);
        void RemoveFromPlaylist(string path);
        void ReplaceInPlaylist(string oldPath, string newPath);
        int GetEffectiveSplitCount();
    }
}
