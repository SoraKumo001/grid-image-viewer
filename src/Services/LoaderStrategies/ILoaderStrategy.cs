using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Views.Controls;
using System.Threading;
using System.Threading.Tasks;

namespace quick_image_viewer.Services.LoaderStrategies
{
    internal interface ILoaderStrategy
    {
        bool CanHandle(string filePath);
        Task LoadAsync(
            string filePath,
            ViewerPageControl pageControl,
            PageRenderer renderer,
            int pageIndex,
            CancellationToken token,
            IViewerCacheManager cacheManager);
    }
}
