using Microsoft.UI.Xaml;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Views.Controls;
using System.Threading;
using System.Threading.Tasks;

namespace quick_image_viewer.Services.LoaderStrategies
{
    internal class EditSessionLoaderStrategy : BaseLoaderStrategy, ILoaderStrategy
    {
        public EditSessionLoaderStrategy(IViewerLoaderHost window) : base(window) { }

        public bool CanHandle(string filePath)
        {
            return _window.ImageEditService?.GetSession(filePath) != null;
        }

        public Task LoadAsync(
            string filePath,
            ViewerPageControl pageControl,
            PageRenderer renderer,
            int pageIndex,
            CancellationToken token,
            IViewerCacheManager cacheManager)
        {
            var session = _window.ImageEditService.GetSession(filePath);
            if (session != null)
            {
                renderer.Reset();
                renderer.CurrentFilePath = filePath;
                renderer.EditedBitmap = session.Current;
                renderer.FrameCount = 1;

                pageControl.PageImage.Visibility = Visibility.Collapsed;
                pageControl.PageCanvas.Visibility = Visibility.Visible;
                pageControl.PageCanvas.Invalidate();
            }
            return Task.CompletedTask;
        }
    }
}
