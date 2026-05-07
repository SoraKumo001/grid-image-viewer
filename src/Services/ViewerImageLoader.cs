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
using System.Linq;
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
            if (ArchiveManager.IsArchive(filePath) && !ArchiveManager.IsArchivePath(filePath))
            {
                var archiveImages = ArchiveManager.GetArchiveImages(filePath, _settings.EnabledExtensions);
                if (archiveImages.Count > 0)
                {
                    filePath = archiveImages[0];
                }
                else
                {
                    return;
                }
            }

            renderer.CurrentFilePath = filePath;
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            string[] videoExtensions = [".webm", ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".flv"];
            bool isVideo = videoExtensions.Contains(ext);

            // 排他制御：前の読み込みや破棄が完了するのを待つ
            await pageControl.ResetPlaybackAsync();

            _window.DispatcherQueue.TryEnqueue(() =>
            {
                if (token.IsCancellationRequested) return;
                if (isVideo)
                {
                    pageControl.IsVideoContent = true;
                    pageControl.LoadingRing.IsActive = true;
                    pageControl.PageCanvas.Visibility = Visibility.Collapsed;
                    pageControl.PagePlayer.Opacity = 0; // ロード完了まで隠しておく

                    // サムネイルがあれば表示
                    var cachedThumb = cacheManager.GetCachedSoftwareBitmap(filePath);
                    if (cachedThumb != null)
                    {
                        var softwareSource = new SoftwareBitmapSource();
                        _window.DispatcherQueue.TryEnqueue(async () =>
                        {
                            try
                            {
                                if (token.IsCancellationRequested)
                                {
                                    softwareSource.Dispose();
                                    return;
                                }
                                await softwareSource.SetBitmapAsync(cachedThumb);
                                if (token.IsCancellationRequested || pageControl.IsMediaReady)
                                {
                                    softwareSource.Dispose();
                                    return;
                                }

                                pageControl.UpdateSoftwareSource(softwareSource);
                                pageControl.PageImage.Visibility = Visibility.Visible;
                                pageControl.PageImage.Opacity = 0.5;
                            }
                            catch
                            {
                                softwareSource.Dispose();
                            }
                        });
                    }
                    else
                    {
                        pageControl.PageImage.Visibility = Visibility.Collapsed;
                    }

                    pageControl.GetOrCreateMediaPlayer();
                }
                else
                {
                    pageControl.LoadingRing.IsActive = !_window.SlideshowManager.IsSlideshowRunning;
                }
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

                bool useSkia = ext == ".webp" || ext == ".gif" || ext == ".avis";

                if (isVideo)
                {
                    // メディアソースの生成をバックグラウンドで行う
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
                                    // グローバルロックの取得（デコーダエンジンの初期化競合を防ぐ）
                                    await ViewerPageControl.GetGlobalInitSemaphore().WaitAsync(token);
                                    try
                                    {
                                        if (token.IsCancellationRequested) return;

                                        // FFmpegInteropX Configuration
                                        var config = new MediaSourceConfig();
                                        config.Video.VideoDecoderMode = VideoDecoderMode.Automatic;
                                        config.Video.VideoOutputAllowBgra8 = false;
                                        config.General.FastSeek = true;
                                        config.General.ReadAheadBufferDuration = TimeSpan.FromSeconds(1);

                                        FFmpegMediaSource? ffmpegSource = null;

                                        try
                                        {
                                            if (ArchiveManager.IsArchivePath(filePath))
                                            {
                                                var (arc, entry) = ArchiveManager.SplitArchivePath(filePath);
                                                var stream = ArchiveManager.GetEntryStream(arc, entry);
                                                if (stream != null)
                                                {
                                                    ffmpegSource = await FFmpegMediaSource.CreateFromStreamAsync(stream.AsRandomAccessStream(), config);
                                                }
                                            }
                                            else
                                            {
                                                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
                                                var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                                                ffmpegSource = await FFmpegMediaSource.CreateFromStreamAsync(stream, config);
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            pageControl.LoadingRing.IsActive = false;
                                            throw;
                                        }

                                        if (ffmpegSource == null || token.IsCancellationRequested)
                                        {
                                            ffmpegSource?.Dispose();
                                            pageControl.LoadingRing.IsActive = false;
                                            return;
                                        }

                                        // ViewerPageControl にソースを保持させる (Dispose管理のため)
                                        pageControl.FFmpegSource = ffmpegSource;
                                    }
                                    finally
                                    {
                                        ViewerPageControl.GetGlobalInitSemaphore().Release();
                                    }

                                    // イベントハンドラの定義
                                    void OnMediaOpened(Windows.Media.Playback.MediaPlayer sender, object args)
                                    {
                                        pageControl.DispatcherQueue.TryEnqueue(() =>
                                        {
                                            if (token.IsCancellationRequested) return;

                                            pageControl.IsMediaReady = true;

                                            // 1. まずサイズを確定させ、レイアウトを強制更新する
                                            try
                                            {
                                                var size = new Windows.Foundation.Size(sender.PlaybackSession.NaturalVideoWidth, sender.PlaybackSession.NaturalVideoHeight);
                                                pageControl.InvokeVideoSizeChanged(size);
                                                pageControl.UpdateLayout(); // 同期的にレイアウトを確定させる
                                            }
                                            catch { }

                                            pageControl.LoadingRing.IsActive = false;
                                            pageControl.PageImage.Source = null; // サムネイルを明示的にクリア
                                            pageControl.PageImage.Visibility = Visibility.Collapsed;
                                            pageControl.PageImage.Opacity = 1.0;

                                            // 2. レイアウト確定後に不透明度を戻して表示を開始
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
                                            pageControl.PageImage.Source = null; // 失敗時もクリアして残像を防ぐ
                                            pageControl.PageImage.Visibility = Visibility.Collapsed;
                                            _window.ShowNotification($"Video Error: {args.Error}");
                                            WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
                                        });
                                    }

                                    // ViewerPageControlを通じてイベントを購読（管理と解除のため）
                                    pageControl.SetMediaHandlers(OnMediaOpened, OnMediaFailed);

                                    // デコード解像度の最適化 (FFmpegInteropXでもMediaPlayerのSurfaceSizeが有効)
                                    double scale = 1.0;
                                    try
                                    {
                                        if (pageControl.IsLoaded && pageControl.XamlRoot != null)
                                            scale = pageControl.XamlRoot.RasterizationScale;
                                    }
                                    catch { }

                                    uint width = (uint)Math.Max(1, pageControl.ActualWidth * scale);
                                    uint height = (uint)Math.Max(1, pageControl.ActualHeight * scale);
                                    try { mp.SetSurfaceSize(new Windows.Foundation.Size(width, height)); } catch { }

                                    // MediaPlayer にセット
                                    mp.Source = null;
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
                                    pageControl.LoadingRing.IsActive = false;
                                    _window.ShowNotification($"FFmpeg Error: {ex.Message}");
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
                        try
                        {
                            await softwareSource.SetBitmapAsync(cachedSoftwareBitmap);
                        }
                        catch (Exception)
                        {
                            if (token.IsCancellationRequested) return;
                        }

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
                // 動画の場合は MediaOpened イベントで終了制御するため、ここでは画像のみ終了させる
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
    }
}
