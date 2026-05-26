using Microsoft.UI.Xaml;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Views.Controls;
using System.Threading;
using System.Threading.Tasks;

namespace quick_image_viewer.Services.LoaderStrategies
{
    internal class SkiaImageLoaderStrategy : BaseLoaderStrategy, ILoaderStrategy
    {
        public SkiaImageLoaderStrategy(IViewerLoaderHost window) : base(window) { }

        public bool CanHandle(string filePath)
        {
            return !MediaHelper.IsVideo(filePath);
        }

        public async Task LoadAsync(
            string filePath,
            ViewerPageControl pageControl,
            PageRenderer renderer,
            int pageIndex,
            CancellationToken token,
            IViewerCacheManager cacheManager)
        {
            await Task.Run(() =>
            {
                var tempRenderer = new PageRenderer();
                tempRenderer.LoadSkia(filePath, token);
                if (!token.IsCancellationRequested)
                {
                    renderer.Reset();
                    renderer.CurrentFilePath = filePath;
                    renderer.Data = tempRenderer.Data;
                    renderer.Codec = tempRenderer.Codec;
                    renderer.Bitmap = tempRenderer.Bitmap;
                    renderer.FrameCount = tempRenderer.FrameCount;
                    renderer.CurrentFrame = tempRenderer.CurrentFrame;
                    renderer.PriorFrame = tempRenderer.PriorFrame;
                    renderer.CurrentFrameDuration = tempRenderer.CurrentFrameDuration;
                }
            }, token);

            if (token.IsCancellationRequested) return;

            pageControl.DispatcherQueue.TryEnqueue(() =>
            {
                if (token.IsCancellationRequested) return;
                pageControl.PageImage.Visibility = Visibility.Collapsed;
                pageControl.PageCanvas.Visibility = Visibility.Visible;
                pageControl.PageCanvas.Invalidate();
            });
        }
    }
}
