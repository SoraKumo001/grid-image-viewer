using CommunityToolkit.Mvvm.Messaging;
using FFmpegInteropX;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Managers;
using quick_image_viewer.ViewModels;
using quick_image_viewer.Views.Controls;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace quick_image_viewer.Services
{
    internal class ViewerImageLoader(IMainView window, ISettingsManager settings) : IViewerImageLoader
    {
        private readonly IMainView _window = window;
        private readonly ISettingsManager _settings = settings;

        public async Task LoadPageIntoBufferAsync(
            string filePath,
            ViewerPageControl pageControl,
            PageRenderer renderer,
            int pageIndex,
            CancellationToken token,
            IViewerCacheManager cacheManager)
        {
            filePath = EnsureValidFilePath(filePath);
            if (string.IsNullOrEmpty(filePath)) return;

            renderer.CurrentFilePath = filePath;
            bool isVideo = MediaHelper.IsVideo(filePath);

            // 排他制御：前の読み込みや破棄が完了するのを待つ
            await pageControl.ResetPlaybackAsync();

            PrepareUIForLoading(filePath, pageControl, isVideo, token, cacheManager);

            try
            {
                if (TryLoadFromEditSession(filePath, pageControl, renderer)) return;

                if (isVideo)
                {
                    await LoadVideoAsync(filePath, pageControl, renderer, token);
                }
                else if (IsSkiaSupported(filePath))
                {
                    await LoadSkiaImageAsync(filePath, pageControl, renderer, token);
                }
                else
                {
                    await LoadNormalImageAsync(filePath, pageControl, renderer, token, cacheManager);
                }
            }
            catch (Exception)
            {
                // エラーハンドリングは各メソッド内で行う
            }
            finally
            {
                if (!isVideo)
                {
                    _window.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        pageControl.LoadingRing.IsActive = false;
                    });
                }
            }
        }

        private string EnsureValidFilePath(string filePath)
        {
            if (ArchiveManager.IsArchive(filePath) && !ArchiveManager.IsArchivePath(filePath))
            {
                var archiveImages = ArchiveManager.GetArchiveImages(filePath, _settings.EnabledExtensions);
                return archiveImages.Count > 0 ? archiveImages[0] : string.Empty;
            }
            return filePath;
        }

        private void PrepareUIForLoading(string filePath, ViewerPageControl pageControl, bool isVideo, CancellationToken token, IViewerCacheManager cacheManager)
        {
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                if (token.IsCancellationRequested) return;
                if (isVideo)
                {
                    pageControl.IsVideoContent = true;
                    pageControl.LoadingRing.IsActive = true;
                    pageControl.PageCanvas.Visibility = Visibility.Collapsed;
                    pageControl.PagePlayer.Opacity = 0;

                    ShowVideoThumbnailIfAvailable(filePath, pageControl, token, cacheManager);
                    pageControl.GetOrCreateMediaPlayer();
                }
                else
                {
                    pageControl.LoadingRing.IsActive = !_window.SlideshowManager.IsSlideshowRunning;
                }
            });
        }

        private void ShowVideoThumbnailIfAvailable(string filePath, ViewerPageControl pageControl, CancellationToken token, IViewerCacheManager cacheManager)
        {
            var cachedThumb = cacheManager.GetCachedSoftwareBitmap(filePath);
            if (cachedThumb != null)
            {
                var softwareSource = new SoftwareBitmapSource();
                bool enqueued = _window.DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        if (token.IsCancellationRequested) { softwareSource.Dispose(); return; }
                        await softwareSource.SetBitmapAsync(cachedThumb);
                        if (token.IsCancellationRequested || pageControl.IsMediaReady) { softwareSource.Dispose(); return; }

                        pageControl.UpdateSoftwareSource(softwareSource);
                        pageControl.PageImage.Visibility = Visibility.Visible;
                        pageControl.PageImage.Opacity = 0.5;
                    }
                    catch { softwareSource.Dispose(); }
                });
                if (!enqueued) softwareSource.Dispose();
            }
            else
            {
                pageControl.PageImage.Visibility = Visibility.Collapsed;
            }
        }

        private bool TryLoadFromEditSession(string filePath, ViewerPageControl pageControl, PageRenderer renderer)
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
                return true;
            }
            return false;
        }

        private bool IsSkiaSupported(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext == ".webp" || ext == ".gif" || ext == ".avis";
        }

        private async Task LoadVideoAsync(string filePath, ViewerPageControl pageControl, PageRenderer renderer, CancellationToken token)
        {
            await Task.Run(async () =>
            {
                try
                {
                    if (token.IsCancellationRequested) return;

                    pageControl.DispatcherQueue.TryEnqueue(async () =>
                    {
                        var mp = pageControl.PagePlayer.MediaPlayer;
                        if (mp == null || token.IsCancellationRequested) return;

                        try
                        {
                            await ViewerPageControl.GetGlobalInitSemaphore().WaitAsync(token);
                            try
                            {
                                if (token.IsCancellationRequested) return;

                                var config = new MediaSourceConfig();
                                config.Video.VideoDecoderMode = VideoDecoderMode.Automatic;
                                config.General.FastSeek = true;
                                config.General.ReadAheadBufferDuration = TimeSpan.FromSeconds(1);

                                FFmpegMediaSource? ffmpegSource = await CreateFFmpegMediaSourceAsync(filePath, config);

                                if (ffmpegSource == null || token.IsCancellationRequested)
                                {
                                    ffmpegSource?.Dispose();
                                    pageControl.LoadingRing.IsActive = false;
                                    return;
                                }

                                pageControl.FFmpegSource = ffmpegSource;
                            }
                            finally
                            {
                                ViewerPageControl.GetGlobalInitSemaphore().Release();
                            }

                            ConfigureMediaPlayer(mp, pageControl, token);

                            var fSource = pageControl.FFmpegSource;
                            if (fSource != null)
                            {
                                await fSource.OpenWithMediaPlayerAsync(mp);
                                fSource.PlaybackSession = mp.PlaybackSession;
                            }
                            mp.IsLoopingEnabled = true;
                        }
                        catch (Exception ex)
                        {
                            if (ex is OperationCanceledException || ex is TaskCanceledException) { }
                            else
                            {
                                pageControl.LoadingRing.IsActive = false;
                                _window.ShowNotification($"FFmpeg Error: {ex.Message}");
                            }
                        }
                    });
                }
                catch (Exception)
                {
                    pageControl.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        pageControl.LoadingRing.IsActive = false;
                    });
                }
            }, token);

            renderer.Reset();
            renderer.CurrentFilePath = filePath;
        }

        private async Task<FFmpegMediaSource?> CreateFFmpegMediaSourceAsync(string filePath, MediaSourceConfig config)
        {
            try
            {
                if (ArchiveManager.IsArchivePath(filePath))
                {
                    var (arc, entry) = ArchiveManager.SplitArchivePath(filePath);
                    var stream = ArchiveManager.GetEntryStream(arc, entry);
                    if (stream != null)
                    {
                        return await FFmpegMediaSource.CreateFromStreamAsync(stream.AsRandomAccessStream(), config);
                    }
                }
                else
                {
                    var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
                    var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                    return await FFmpegMediaSource.CreateFromStreamAsync(stream, config);
                }
            }
            catch { }
            return null;
        }

        private void ConfigureMediaPlayer(Windows.Media.Playback.MediaPlayer mp, ViewerPageControl pageControl, CancellationToken token)
        {
            void OnMediaOpened(Windows.Media.Playback.MediaPlayer sender, object args)
            {
                pageControl.DispatcherQueue.TryEnqueue(() =>
                {
                    if (token.IsCancellationRequested) return;
                    pageControl.IsMediaReady = true;

                    try
                    {
                        var size = new Windows.Foundation.Size(sender.PlaybackSession.NaturalVideoWidth, sender.PlaybackSession.NaturalVideoHeight);
                        pageControl.InvokeVideoSizeChanged(size);
                        pageControl.UpdateLayout();
                    }
                    catch { }

                    pageControl.LoadingRing.IsActive = false;
                    pageControl.PageImage.Source = null;
                    pageControl.PageImage.Visibility = Visibility.Collapsed;
                    pageControl.PageImage.Opacity = 1.0;

                    pageControl.PagePlayer.Opacity = 1.0;
                    pageControl.PagePlayer.Visibility = Visibility.Visible;

                    sender.Play();
                    WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
                });
            }

            void OnMediaFailed(Windows.Media.Playback.MediaPlayer sender, Windows.Media.Playback.MediaPlayerFailedEventArgs args)
            {
                pageControl.DispatcherQueue.TryEnqueue(() =>
                {
                    if (token.IsCancellationRequested) return;
                    pageControl.LoadingRing.IsActive = false;
                    pageControl.PageImage.Source = null;
                    pageControl.PageImage.Visibility = Visibility.Collapsed;
                    _window.ShowNotification($"Video Error: {args.Error}");
                    WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
                });
            }

            pageControl.SetMediaHandlers(OnMediaOpened, OnMediaFailed);

            double scale = 1.0;
            try { if (pageControl.IsLoaded && pageControl.XamlRoot != null) scale = pageControl.XamlRoot.RasterizationScale; } catch { }

            uint width = (uint)Math.Max(1, pageControl.ActualWidth * scale);
            uint height = (uint)Math.Max(1, pageControl.ActualHeight * scale);
            try { mp.SetSurfaceSize(new Windows.Foundation.Size(width, height)); } catch { }
            mp.Source = null;
        }

        private async Task LoadSkiaImageAsync(string filePath, ViewerPageControl pageControl, PageRenderer renderer, CancellationToken token)
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
            pageControl.PageImage.Visibility = Visibility.Collapsed;
            pageControl.PageCanvas.Visibility = Visibility.Visible;
            pageControl.PageCanvas.Invalidate();
        }

        private async Task LoadNormalImageAsync(string filePath, ViewerPageControl pageControl, PageRenderer renderer, CancellationToken token, IViewerCacheManager cacheManager)
        {
            SoftwareBitmap? cachedSoftwareBitmap = cacheManager.GetCachedSoftwareBitmap(filePath);

            if (cachedSoftwareBitmap != null)
            {
                var softwareSource = new SoftwareBitmapSource();
                try
                {
                    await softwareSource.SetBitmapAsync(cachedSoftwareBitmap);
                }
                catch
                {
                    softwareSource.Dispose();
                    return;
                }

                if (token.IsCancellationRequested)
                {
                    softwareSource.Dispose();
                    return;
                }

                renderer.Reset();
                renderer.CurrentFilePath = filePath;
                pageControl.PageCanvas.Visibility = Visibility.Collapsed;
                pageControl.PageImage.Visibility = Visibility.Visible;
                pageControl.UpdateSoftwareSource(softwareSource);
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
                    bitmapImage = await LoadBitmapImageFromFileAsync(filePath);
                }

                if (token.IsCancellationRequested) return;

                renderer.Reset();
                renderer.CurrentFilePath = filePath;
                pageControl.PageCanvas.Visibility = Visibility.Collapsed;
                pageControl.PageImage.Visibility = Visibility.Visible;
                pageControl.PageImage.Source = bitmapImage;
            }
        }

        private async Task<BitmapImage?> LoadBitmapImageFromFileAsync(string filePath)
        {
            try
            {
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
                using var stream = await file.OpenReadAsync();
                var bitmapImage = new BitmapImage();
                await bitmapImage.SetSourceAsync(stream).AsTask();
                return bitmapImage;
            }
            catch
            {
                var bmpBytes = await Task.Run(() => ImageProcessor.DecodeToBmpBytes(filePath));
                if (bmpBytes != null)
                {
                    var bitmapImage = new BitmapImage();
                    using var ms = new MemoryStream(bmpBytes);
                    await bitmapImage.SetSourceAsync(ms.AsRandomAccessStream()).AsTask();
                    return bitmapImage;
                }
            }
            return null;
        }
    }
}
