using quick_image_viewer.Helpers;
using quick_image_viewer.Views.Controls;
using System.Threading;
using System.Threading.Tasks;
namespace quick_image_viewer.Interfaces
{
    public interface IViewerImageLoader
    {
        Task LoadPageIntoBufferAsync(
            string filePath,
            ViewerPageControl pageControl,
            PageRenderer renderer,
            int pageIndex,
            CancellationToken token,
            IViewerCacheManager cacheManager);
    }
}
