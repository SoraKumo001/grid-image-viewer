using FFmpegInteropX;
using Microsoft.UI.Xaml;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Managers;
using quick_image_viewer.Views.Controls;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Playback;

namespace quick_image_viewer.Services.LoaderStrategies
{
    internal class VideoLoaderStrategy : BaseLoaderStrategy, ILoaderStrategy
    {
        public VideoLoaderStrategy(IViewerLoaderHost window) : base(window) { }

        public bool CanHandle(string filePath)
        {
            return MediaHelper.IsVideo(filePath);
        }

        public async Task LoadAsync(
            string filePath,
            ViewerPageControl pageControl,
            PageRenderer renderer,
            int pageIndex,
            CancellationToken token,
            IViewerCacheManager cacheManager)
        {
            var uiToken = pageControl.GetNewLoadToken();
            var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(token, uiToken);
            var combinedToken = combinedCts.Token;

            try
            {
                await EnqueueOnDispatcherAsync(pageControl.DispatcherQueue, async () =>
                {
                    if (combinedToken.IsCancellationRequested) return;

                    var mp = pageControl.CreateNewMediaPlayer();

                    try
                    {
                        await ViewerPageControl.GetGlobalInitSemaphore().WaitAsync(combinedToken);
                        try
                        {
                            if (combinedToken.IsCancellationRequested) return;

                            var config = new MediaSourceConfig();
                            config.Video.VideoDecoderMode = VideoDecoderMode.Automatic;
                            config.General.FastSeek = true;
                            config.General.ReadAheadBufferDuration = TimeSpan.FromSeconds(1);

                            FFmpegMediaSource? ffmpegSource = await CreateFFmpegMediaSourceAsync(filePath, config);

                            if (ffmpegSource == null || combinedToken.IsCancellationRequested)
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

                        if (combinedToken.IsCancellationRequested) return;

                        ConfigureMediaPlayer(mp, pageControl, combinedToken);

                        var fSource = pageControl.FFmpegSource;
                        if (fSource != null && !combinedToken.IsCancellationRequested)
                        {
                            var playbackItem = fSource.CreateMediaPlaybackItem();
                            if (!combinedToken.IsCancellationRequested)
                            {
                                try
                                {
                                    mp.IsLoopingEnabled = true;
                                    mp.Source = playbackItem;
                                }
                                catch (Exception) { }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!(ex is OperationCanceledException || ex is TaskCanceledException))
                        {
                            pageControl.LoadingRing.IsActive = false;
                            _window?.ShowNotification($"FFmpeg Error: {ex.Message}");
                        }
                    }
                }, combinedToken);
            }
            catch (Exception ex)
            {
                if (!(ex is OperationCanceledException || ex is TaskCanceledException))
                {
                    pageControl.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (combinedToken.IsCancellationRequested) return;
                        pageControl.LoadingRing.IsActive = false;
                    });
                }
            }

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
                        var ras = stream.AsRandomAccessStream();
                        try
                        {
                            return await FFmpegMediaSource.CreateFromStreamAsync(ras, config);
                        }
                        catch
                        {
                            ras.Dispose();
                            throw;
                        }
                    }
                }
                else
                {
                    var stream = File.OpenRead(filePath);
                    try
                    {
                        return await FFmpegMediaSource.CreateFromStreamAsync(stream.AsRandomAccessStream(), config);
                    }
                    catch
                    {
                        stream.Dispose();
                        throw;
                    }
                }
            }
            catch (Exception) { }
            return null;
        }

        private void ConfigureMediaPlayer(MediaPlayer mp, ViewerPageControl pageControl, CancellationToken token)
        {
            void OnMediaOpened(MediaPlayer sender, object args)
            {
                if (token.IsCancellationRequested) return;

                pageControl.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        if (token.IsCancellationRequested) return;
                        pageControl.IsMediaReady = true;

                        try
                        {
                            var session = sender.PlaybackSession;
                            if (session != null)
                            {
                                uint w = 0, h = 0;
                                try { w = session.NaturalVideoWidth; } catch { }
                                try { h = session.NaturalVideoHeight; } catch { }
                                if (w > 0 && h > 0)
                                {
                                    var size = new Windows.Foundation.Size(w, h);
                                    pageControl.InvokeVideoSizeChanged(size);
                                    pageControl.UpdateLayout();
                                }
                            }
                        }
                        catch { }

                        pageControl.LoadingRing.IsActive = false;
                        pageControl.PageImage.Source = null;
                        pageControl.PageImage.Visibility = Visibility.Collapsed;
                        pageControl.PageImage.Opacity = 1.0;

                        pageControl.PagePlayer.Opacity = 1.0;
                        pageControl.PagePlayer.Visibility = Visibility.Visible;

                        if (!token.IsCancellationRequested)
                        {
                            try { sender.Play(); } catch { }
                        }
                    }
                    catch (Exception) { }
                });
            }

            void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
            {
                if (token.IsCancellationRequested) return;

                pageControl.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        if (token.IsCancellationRequested) return;
                        pageControl.LoadingRing.IsActive = false;
                        pageControl.PageImage.Source = null;
                        pageControl.PageImage.Visibility = Visibility.Collapsed;
                        _window?.ShowNotification($"Video Error: {args.Error}");
                    }
                    catch (Exception) { }
                });
            }

            pageControl.SetMediaHandlers(OnMediaOpened, OnMediaFailed);

            try { mp.Source = null; } catch { }
        }
    }
}
