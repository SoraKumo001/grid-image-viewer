using System.Threading;
using System.Threading.Tasks;

namespace grid_image_viewer
{
    public interface IViewerImageLoader
    {
        Task LoadPageIntoBufferAsync(
            string filePath,
            Controls.ViewerPageControl pageControl,
            PageRenderer renderer,
            int pageIndex,
            CancellationToken token,
            IViewerCacheManager cacheManager);
    }
}
