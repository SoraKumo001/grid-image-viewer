using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
namespace quick_image_viewer.Interfaces
{
    public interface IViewerCacheManager
    {
        byte[]? GetCachedBytes(string filePath);
        SoftwareBitmap? GetCachedSoftwareBitmap(string filePath);
        Task PreloadPathsAsync(List<string> paths, CancellationToken token);
        Task PreloadAroundAsync(int currentIndex, List<string> playlist, int splitCount);
        Task PreloadFoldersAsync(string currentDir);
        void CancelPreloads();
        void ClearCache();
    }
}
