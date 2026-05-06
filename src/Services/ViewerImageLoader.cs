using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Managers;
using quick_image_viewer.Views.Controls;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Storage.Streams;
namespace quick_image_viewer.Services
{
    internal class ViewerImageLoader : IViewerImageLoader
    {
        private readonly IMainView _window;
        private readonly ISettingsManager _settings;

        public ViewerImageLoader(IMainView window, ISettingsManager settings)
        {
            _window = window;
            _settings = settings;
        }

        public async Task LoadPageIntoBufferAsync(
            string filePath,
            ViewerPageControl pageControl,
            PageRenderer renderer,
            int pageIndex,
            CancellationToken token,
            IViewerCacheManager cacheManager)
        {
            if (ArchiveManager.IsArchive(filePath) && !ArchiveManager.IsArchivePath(filePath))
            {
                _window.DispatcherQueue.TryEnqueue(() => _window.LoadDirectory(filePath));
                return;
            }

            renderer.CurrentFilePath = filePath;
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                pageControl.ResetPlayback();
                pageControl.LoadingRing.IsActive = !_window.SlideshowManager.IsSlideshowRunning;
            });

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
                bool isWebM = ext == ".webm";
                bool useSkia = ext == ".webp" || ext == ".gif" || ext == ".avis";

                if (isWebM)
                {
                    IRandomAccessStream? stream = null;
                    if (ArchiveManager.IsArchivePath(filePath))
                    {
                        await Task.Run(() =>
                        {
                            var (arc, entry) = ArchiveManager.SplitArchivePath(filePath);
                            var s = ArchiveManager.GetEntryStream(arc, entry);
                            if (s != null) stream = s.AsRandomAccessStream();
                        });
                    }

                    _window.DispatcherQueue.TryEnqueue(() =>
                    {
                        pageControl.PageImage.Visibility = Visibility.Collapsed;
                        pageControl.PageCanvas.Visibility = Visibility.Collapsed;
                        pageControl.PagePlayer.Visibility = Visibility.Visible;

                        if (stream != null)
                        {
                            pageControl.PagePlayer.Source = MediaSource.CreateFromStream(stream, "video/webm");
                        }
                        else if (!ArchiveManager.IsArchivePath(filePath))
                        {
                            pageControl.PagePlayer.Source = MediaSource.CreateFromUri(new Uri(filePath));
                        }

                        if (pageControl.PagePlayer.MediaPlayer != null)
                        {
                            pageControl.PagePlayer.MediaPlayer.IsLoopingEnabled = true;
                            pageControl.PagePlayer.MediaPlayer.IsMuted = true;
                        }
                        pageControl.LoadingRing.IsActive = false;
                    });
                    renderer.Reset();
                    renderer.CurrentFilePath = filePath;
                    return;
                }

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
            finally
            {
                _window.DispatcherQueue.TryEnqueue(() => { pageControl.LoadingRing.IsActive = false; });
            }
        }
    }
}
