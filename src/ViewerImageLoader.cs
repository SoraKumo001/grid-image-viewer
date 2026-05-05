using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
            Image imageCtrl,
            SkiaSharp.Views.Windows.SKXamlCanvas canvasCtrl,
            ProgressRing loadingRing,
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
            _window.DispatcherQueue.TryEnqueue(() => { loadingRing.IsActive = !_window.SlideshowManager.IsSlideshowRunning; });

            try
            {
                var session = _window.ImageEditService.GetSession(filePath);
                if (session != null)
                {
                    renderer.Reset();
                    renderer.CurrentFilePath = filePath;
                    renderer.EditedBitmap = session.Current;
                    renderer.FrameCount = 1;

                    imageCtrl.Visibility = Visibility.Collapsed;
                    canvasCtrl.Visibility = Visibility.Visible;
                    canvasCtrl.Invalidate();
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
                    imageCtrl.Visibility = Visibility.Collapsed;
                    canvasCtrl.Visibility = Visibility.Visible;
                    canvasCtrl.Invalidate();
                }
                else
                {
                    byte[]? cachedBytes = cacheManager.GetCachedBytes(filePath);
                    Microsoft.UI.Xaml.Media.Imaging.BitmapImage? bitmapImage = null;

                    if (cachedBytes != null)
                    {
                        using var ms = new MemoryStream(cachedBytes);
                        bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                        await bitmapImage.SetSourceAsync(ms.AsRandomAccessStream());
                    }
                    else
                    {
                        try
                        {
                            using var stream = File.OpenRead(filePath);
                            bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                            await bitmapImage.SetSourceAsync(stream.AsRandomAccessStream());
                        }
                        catch (Exception)
                        {
                            var bmpBytes = await Task.Run(() => ImageProcessor.DecodeToBmpBytes(filePath));
                            if (bmpBytes != null)
                            {
                                bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                                using var ms = new MemoryStream(bmpBytes);
                                await bitmapImage.SetSourceAsync(ms.AsRandomAccessStream());
                            }
                        }
                    }

                    if (token.IsCancellationRequested) return;

                    renderer.Reset();
                    renderer.CurrentFilePath = filePath;
                    canvasCtrl.Visibility = Visibility.Collapsed;
                    imageCtrl.Visibility = Visibility.Visible;
                    imageCtrl.Source = bitmapImage;
                }
            }
            catch { }
            finally { loadingRing.IsActive = false; }
        }
    }
}
