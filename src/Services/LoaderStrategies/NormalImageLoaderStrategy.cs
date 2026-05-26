using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Views.Controls;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace quick_image_viewer.Services.LoaderStrategies
{
    internal class NormalImageLoaderStrategy : BaseLoaderStrategy, ILoaderStrategy
    {
        public NormalImageLoaderStrategy(IViewerLoaderHost window) : base(window) { }

        public bool CanHandle(string filePath)
        {
            return true; // Final fallback
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
                SoftwareBitmap? cachedSoftwareBitmap = cacheManager.GetCachedSoftwareBitmap(filePath);

                if (cachedSoftwareBitmap != null)
                {
                    var softwareSource = new SoftwareBitmapSource();
                    try
                    {
                        var copy = SoftwareBitmap.Copy(cachedSoftwareBitmap);
                        await softwareSource.SetBitmapAsync(copy);
                    }
                    catch (Exception)
                    {
                        softwareSource.Dispose();
                        cachedSoftwareBitmap = null;
                    }

                    if (cachedSoftwareBitmap != null)
                    {
                        if (token.IsCancellationRequested)
                        {
                            softwareSource.Dispose();
                            return;
                        }

                        renderer.Reset();
                        renderer.CurrentFilePath = filePath;
                        pageControl.DispatcherQueue.TryEnqueue(() =>
                        {
                            pageControl.PageCanvas.Visibility = Visibility.Collapsed;
                            pageControl.PageImage.Visibility = Visibility.Visible;
                            pageControl.UpdateSoftwareSource(softwareSource);
                        });
                    }
                }

                if (cachedSoftwareBitmap == null)
                {
                    BitmapImage? bitmapImage = null;
                    byte[]? cachedBytes = cacheManager.GetCachedBytes(filePath);

                    if (cachedBytes != null)
                    {
                        var tcs = new TaskCompletionSource<BitmapImage?>();
                        pageControl.DispatcherQueue.TryEnqueue(async () =>
                        {
                            try
                            {
                                using var ms = new MemoryStream(cachedBytes);
                                bitmapImage = new BitmapImage();
                                await bitmapImage.SetSourceAsync(ms.AsRandomAccessStream()).AsTask();
                                tcs.SetResult(bitmapImage);
                            }
                            catch { tcs.SetResult(null); }
                        });
                        bitmapImage = await tcs.Task;
                    }
                    else
                    {
                        bitmapImage = await LoadBitmapImageFromFileAsync(filePath, pageControl.DispatcherQueue);
                    }

                    if (token.IsCancellationRequested) return;

                    renderer.Reset();
                    renderer.CurrentFilePath = filePath;
                    pageControl.DispatcherQueue.TryEnqueue(() =>
                    {
                        pageControl.PageCanvas.Visibility = Visibility.Collapsed;
                        pageControl.PageImage.Visibility = Visibility.Visible;
                        pageControl.PageImage.Source = bitmapImage;
                    });
                }
            }
            catch { }
        }

        private async Task<BitmapImage?> LoadBitmapImageFromFileAsync(string filePath, Microsoft.UI.Dispatching.DispatcherQueue dispatcher)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || filePath.Length > 32767) return null;

                var tcs = new TaskCompletionSource<BitmapImage?>();
                dispatcher.TryEnqueue(async () =>
                {
                    try
                    {
                        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        var bitmapImage = new BitmapImage();
                        await bitmapImage.SetSourceAsync(fs.AsRandomAccessStream()).AsTask();
                        tcs.SetResult(bitmapImage);
                    }
                    catch { tcs.SetResult(null); }
                });
                return await tcs.Task;
            }
            catch
            {
                try
                {
                    var bmpBytes = await Task.Run(() => ImageProcessor.DecodeToBmpBytes(filePath));
                    if (bmpBytes != null)
                    {
                        var tcs = new TaskCompletionSource<BitmapImage?>();
                        dispatcher.TryEnqueue(async () =>
                        {
                            using var ms = new MemoryStream(bmpBytes);
                            var bitmapImage = new BitmapImage();
                            await bitmapImage.SetSourceAsync(ms.AsRandomAccessStream()).AsTask();
                            tcs.SetResult(bitmapImage);
                        });
                        return await tcs.Task;
                    }
                }
                catch { }
            }
            return null;
        }
    }
}
