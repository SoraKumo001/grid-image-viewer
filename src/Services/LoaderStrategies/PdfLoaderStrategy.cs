using Microsoft.UI.Xaml;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Managers;
using quick_image_viewer.Views.Controls;
using SkiaSharp;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace quick_image_viewer.Services.LoaderStrategies
{
    internal class PdfLoaderStrategy : BaseLoaderStrategy, ILoaderStrategy
    {
        public PdfLoaderStrategy(IMainView window) : base(window) { }

        public bool CanHandle(string filePath)
        {
            return PdfManager.IsPdfPath(filePath);
        }

        public async Task LoadAsync(
            string filePath,
            ViewerPageControl pageControl,
            PageRenderer renderer,
            int pageIndex,
            CancellationToken token,
            IViewerCacheManager cacheManager)
        {
            try
            {
                var (actualPath, pdfPageIndex) = PdfManager.SplitVirtualPath(filePath);

                await Task.Run(async () =>
                {
                    using var stream = await PdfManager.RenderPageToStreamAsync(actualPath, pdfPageIndex);
                    if (stream == null || token.IsCancellationRequested) return;

                    using var netStream = stream.AsStreamForRead();
                    var skData = SKData.Create(netStream);
                    if (skData == null) return;

                    var bitmap = SKBitmap.Decode(skData);
                    if (bitmap == null)
                    {
                        skData.Dispose();
                        return;
                    }

                    if (!token.IsCancellationRequested)
                    {
                        renderer.Reset();
                        renderer.CurrentFilePath = filePath;
                        renderer.Data = skData;
                        renderer.Bitmap = bitmap;
                        renderer.FrameCount = 1;
                        renderer.CurrentFrame = 0;
                    }
                    else
                    {
                        bitmap.Dispose();
                        skData.Dispose();
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
            catch { }
        }
    }
}
