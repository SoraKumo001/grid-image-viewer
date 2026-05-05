using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace grid_image_viewer
{
    internal class ViewerImageLoader
    {
        private readonly MainWindow _window;
        private readonly SettingsManager _settings;

        public ViewerImageLoader(MainWindow window, SettingsManager settings)
        {
            _window = window;
            _settings = settings;
        }

        public async Task LoadPageIntoBufferAsync(
            string filePath,
            Controls.ViewerPageControl pageControl,
            PageRenderer renderer,
            int pageIndex,
            CancellationToken token,
            ViewerCacheManager cacheManager)
        {
            if (ArchiveManager.IsArchive(filePath) && !ArchiveManager.IsArchivePath(filePath))
            {
                _window.DispatcherQueue.TryEnqueue(() => _window.LoadDirectory(filePath));
                return;
            }

            renderer.CurrentFilePath = filePath;
            _window.DispatcherQueue.TryEnqueue(() => { pageControl.LoadingRing.IsActive = !_window.SlideshowManager.IsSlideshowRunning; });

            try
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
                    return;
                }

                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                bool useSkia = ext == ".webp" || ext == ".gif" || ext == ".avis";

                if (useSkia)
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
                    });
                    if (token.IsCancellationRequested) return;
                    pageControl.PageImage.Visibility = Visibility.Collapsed;
                    pageControl.PageCanvas.Visibility = Visibility.Visible;
                    pageControl.PageCanvas.Invalidate();
                }
                else
                {
                    SoftwareBitmap? cachedSoftwareBitmap = cacheManager.GetCachedSoftwareBitmap(filePath);

                    if (cachedSoftwareBitmap != null)
                    {
                        var softwareSource = new SoftwareBitmapSource();
                        await softwareSource.SetBitmapAsync(cachedSoftwareBitmap);

                        if (token.IsCancellationRequested) return;

                        renderer.Reset();
                        renderer.CurrentFilePath = filePath;
                        pageControl.PageCanvas.Visibility = Visibility.Collapsed;
                        pageControl.PageImage.Visibility = Visibility.Visible;
                        pageControl.PageImage.Source = softwareSource;
                    }
                    else
                    {
                        BitmapImage? bitmapImage = null;
                        byte[]? cachedBytes = cacheManager.GetCachedBytes(filePath);

                        if (cachedBytes != null)
                        {
                            using var ms = new MemoryStream(cachedBytes);
                            bitmapImage = new BitmapImage();
                            await bitmapImage.SetSourceAsync(ms.AsRandomAccessStream()).AsTask();
                        }
                        else
                        {
                            try
                            {
                                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
                                using var stream = await file.OpenReadAsync();
                                bitmapImage = new BitmapImage();
                                await bitmapImage.SetSourceAsync(stream).AsTask();
                            }
                            catch (Exception)
                            {
                                var bmpBytes = await Task.Run(() => ImageProcessor.DecodeToBmpBytes(filePath));
                                if (bmpBytes != null)
                                {
                                    bitmapImage = new BitmapImage();
                                    using var ms = new MemoryStream(bmpBytes);
                                    await bitmapImage.SetSourceAsync(ms.AsRandomAccessStream()).AsTask();
                                }
                            }
                        }

                        if (token.IsCancellationRequested) return;

                        renderer.Reset();
                        renderer.CurrentFilePath = filePath;
                        pageControl.PageCanvas.Visibility = Visibility.Collapsed;
                        pageControl.PageImage.Visibility = Visibility.Visible;
                        pageControl.PageImage.Source = bitmapImage;
                    }
                }
            }
            catch { }
            finally { pageControl.LoadingRing.IsActive = false; }
        }
    }
}
