using FFmpegInteropX;
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

namespace quick_image_viewer.Services
{
    internal class ViewerImageLoader(IMainView window, ISettingsManager settings) : IViewerImageLoader
    {
        private readonly IMainView _window = window ?? throw new ArgumentNullException(nameof(window));
        private readonly ISettingsManager _settings = settings;

        public async Task LoadPageIntoBufferAsync(
            string filePath,
            ViewerPageControl pageControl,
            PageRenderer renderer,
            int pageIndex,
            CancellationToken token,
            IViewerCacheManager cacheManager)
        {
            System.Diagnostics.Debug.WriteLine($"[ViewerImageLoader] Loading: {filePath}");
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
                    System.Diagnostics.Debug.WriteLine("[ViewerImageLoader] Loading as Video");
                    await LoadVideoAsync(filePath, pageControl, renderer, token);
                }
                else if (IsSkiaSupported(filePath))
                {
                    System.Diagnostics.Debug.WriteLine("[ViewerImageLoader] Loading as Skia");
                    await LoadSkiaImageAsync(filePath, pageControl, renderer, token);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[ViewerImageLoader] Loading as Normal Image");
                    await LoadNormalImageAsync(filePath, pageControl, renderer, token, cacheManager);
                }
                System.Diagnostics.Debug.WriteLine($"[ViewerImageLoader] Load Completed: {filePath}");
            }
            catch (Exception)
            {
                // エラーハンドリングは各メソッド内で行う
            }
            finally
            {
                if (!isVideo)
                {
                    var dispatcher = pageControl.DispatcherQueue ?? _window?.DispatcherQueue;
                    dispatcher?.TryEnqueue(() =>
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
            var dispatcher = pageControl.DispatcherQueue ?? _window?.DispatcherQueue;
            if (dispatcher == null) return;

            dispatcher.TryEnqueue(() =>
            {
                if (token.IsCancellationRequested) return;

                bool isSlideshowRunning = false;
                try { isSlideshowRunning = _window?.SlideshowManager?.IsSlideshowRunning ?? false; } catch { }

                if (isVideo)
                {
                    pageControl.IsVideoContent = true;
                    pageControl.LoadingRing.IsActive = true;
                    pageControl.PageCanvas.Visibility = Visibility.Collapsed;
                    pageControl.PageImage.Visibility = Visibility.Collapsed;

                    try
                    {
                        pageControl.GetOrCreateMediaPlayer();
                        pageControl.PagePlayer.Opacity = 0;
                    }
                    catch { }
                }
                else
                {
                    pageControl.LoadingRing.IsActive = !isSlideshowRunning;
                }
            });
        }

        private bool TryLoadFromEditSession(string filePath, ViewerPageControl pageControl, PageRenderer renderer)
        {
            var session = _window?.ImageEditService?.GetSession(filePath);
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
            // 動画以外は原則 Skia バックエンドでの描画を優先し、GPUアクセラレーションを有効にする
            return !MediaHelper.IsVideo(filePath);
        }

        private async Task LoadVideoAsync(string filePath, ViewerPageControl pageControl, PageRenderer renderer, CancellationToken token)
        {
            var uiToken = pageControl.GetNewLoadToken();
            var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(token, uiToken);
            var combinedToken = combinedCts.Token;

            try
            {
                await EnqueueOnDispatcherAsync(pageControl.DispatcherQueue, async () =>
                {
                    if (combinedToken.IsCancellationRequested) return;

                    // 既存のプレイヤーを破棄し、新しい MediaPlayer インスタンスを作成
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
                            System.Diagnostics.Debug.WriteLine("[ViewerImageLoader] Creating MediaPlaybackItem");

                            // CreateMediaPlaybackItem() を使用することで、OpenWithMediaPlayerAsync 内部で発生する
                            // MediaPlayer との競合（COMException）を回避できる場合があります
                            var playbackItem = fSource.CreateMediaPlaybackItem();

                            if (!combinedToken.IsCancellationRequested)
                            {
                                try
                                {
                                    mp.IsLoopingEnabled = true;
                                    mp.Source = playbackItem;
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[ViewerImageLoader] Failed to set source: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is OperationCanceledException || ex is TaskCanceledException) { }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[ViewerImageLoader] Video Load Error: {ex.Message}");
                            pageControl.LoadingRing.IsActive = false;
                            _window?.ShowNotification($"FFmpeg Error: {ex.Message}");
                        }
                    }
                }, combinedToken);
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException || ex is TaskCanceledException) { }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ViewerImageLoader] Video dispatcher error: {ex.Message}");
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

        private static Task EnqueueOnDispatcherAsync(
            Microsoft.UI.Dispatching.DispatcherQueue dispatcher,
            Func<Task> action,
            CancellationToken token)
        {
            if (dispatcher.HasThreadAccess)
            {
                return action();
            }

            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    await action();
                    tcs.TrySetResult(null);
                }
                catch (OperationCanceledException ex)
                {
                    tcs.TrySetCanceled(ex.CancellationToken);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
            {
                tcs.TrySetCanceled(token);
            }

            return tcs.Task;
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ViewerImageLoader] CreateFFmpegMediaSourceAsync Error: {ex.Message}");
            }
            return null;
        }

        private void ConfigureMediaPlayer(Windows.Media.Playback.MediaPlayer mp, ViewerPageControl pageControl, CancellationToken token)
        {
            void OnMediaOpened(Windows.Media.Playback.MediaPlayer sender, object args)
            {
                if (token.IsCancellationRequested) return;

                pageControl.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        if (token.IsCancellationRequested) return;
                        System.Diagnostics.Debug.WriteLine("[ViewerImageLoader] Media Opened");
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
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ViewerImageLoader] Session error: {ex.Message}"); }

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

                        // WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ViewerImageLoader] OnMediaOpened Error: {ex.Message}");
                    }
                });
            }

            void OnMediaFailed(Windows.Media.Playback.MediaPlayer sender, Windows.Media.Playback.MediaPlayerFailedEventArgs args)
            {
                if (token.IsCancellationRequested) return;

                pageControl.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        if (token.IsCancellationRequested) return;
                        System.Diagnostics.Debug.WriteLine($"[ViewerImageLoader] Media Failed: {args.Error} - {args.ErrorMessage}");
                        pageControl.LoadingRing.IsActive = false;
                        pageControl.PageImage.Source = null;
                        pageControl.PageImage.Visibility = Visibility.Collapsed;
                        _window?.ShowNotification($"Video Error: {args.Error}");
                        // WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ViewerImageLoader] OnMediaFailed Error: {ex.Message}");
                    }
                });
            }

            pageControl.SetMediaHandlers(OnMediaOpened, OnMediaFailed);

            // 以前のソースをクリアし、MediaPlayerをクリーンな状態にする
            try { mp.Source = null; } catch { }
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
            try
            {
                SoftwareBitmap? cachedSoftwareBitmap = cacheManager.GetCachedSoftwareBitmap(filePath);

                if (cachedSoftwareBitmap != null)
                {
                    System.Diagnostics.Debug.WriteLine("[ViewerImageLoader] Using cached software bitmap");
                    var softwareSource = new SoftwareBitmapSource();
                    try
                    {
                        var copy = Windows.Graphics.Imaging.SoftwareBitmap.Copy(cachedSoftwareBitmap);
                        await softwareSource.SetBitmapAsync(copy);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ViewerImageLoader] SetBitmapAsync Error: {ex.Message}");
                        softwareSource.Dispose();
                        cachedSoftwareBitmap = null; // フォールバックさせる
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
                        pageControl.PageCanvas.Visibility = Visibility.Collapsed;
                        pageControl.PageImage.Visibility = Visibility.Visible;
                        pageControl.UpdateSoftwareSource(softwareSource);
                    }
                }

                if (cachedSoftwareBitmap == null)
                {
                    BitmapImage? bitmapImage = null;
                    byte[]? cachedBytes = cacheManager.GetCachedBytes(filePath);

                    if (cachedBytes != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[ViewerImageLoader] Using cached bytes");
                        using var ms = new MemoryStream(cachedBytes);
                        bitmapImage = new BitmapImage();
                        await bitmapImage.SetSourceAsync(ms.AsRandomAccessStream()).AsTask();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[ViewerImageLoader] Loading from file");
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ViewerImageLoader] LoadNormalImageAsync Error: {ex.Message}");
                throw; // Rethrow to let global handler catch it if it's critical
            }
        }

        private async Task<BitmapImage?> LoadBitmapImageFromFileAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || filePath.Length > 32767)
                {
                    System.Diagnostics.Debug.WriteLine($"[ViewerImageLoader] Invalid path or too long: {filePath?.Length}");
                    return null;
                }

                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var bitmapImage = new BitmapImage();
                await bitmapImage.SetSourceAsync(fs.AsRandomAccessStream()).AsTask();
                return bitmapImage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ViewerImageLoader] LoadBitmapImageFromFileAsync method failed: {ex.Message}");
                try
                {
                    System.Diagnostics.Debug.WriteLine("[ViewerImageLoader] Trying fallback ImageProcessor.DecodeToBmpBytes");
                    var bmpBytes = await Task.Run(() => ImageProcessor.DecodeToBmpBytes(filePath));
                    if (bmpBytes != null)
                    {
                        var bitmapImage = new BitmapImage();
                        using var ms = new MemoryStream(bmpBytes);
                        await bitmapImage.SetSourceAsync(ms.AsRandomAccessStream()).AsTask();
                        return bitmapImage;
                    }
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"[ViewerImageLoader] Fallback failed: {ex2.Message}");
                }
            }
            return null;
        }
    }
}
