using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Managers;
using quick_image_viewer.Views.Controls;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
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
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            string[] videoExtensions = { ".webm", ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".flv" };
            bool isVideo = videoExtensions.Contains(ext);

            _window.DispatcherQueue.TryEnqueue(() =>
            {
                pageControl.ResetPlayback();
                if (isVideo)
                {
                    pageControl.LoadingRing.IsActive = true;
                    pageControl.PageImage.Visibility = Visibility.Collapsed;
                    pageControl.PageCanvas.Visibility = Visibility.Collapsed;

                    // プレイヤーを再利用（または新規作成）して即座にセットアップ
                    pageControl.GetOrCreateMediaPlayer();
                    System.Diagnostics.Debug.WriteLine($"[VideoLoader] Immediate UI reset and player pre-warm: {filePath}");
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
                    await Task.Run(() =>
                    {
                        try
                        {
                            if (token.IsCancellationRequested) return;

                            MediaSource? source = null;
                            if (ArchiveManager.IsArchivePath(filePath))
                            {
                                var (arc, entry) = ArchiveManager.SplitArchivePath(filePath);
                                var stream = ArchiveManager.GetEntryStream(arc, entry);
                                if (token.IsCancellationRequested) { stream?.Dispose(); return; }

                                string mimeType = ext switch
                                {
                                    ".mp4" => "video/mp4",
                                    ".mkv" => "video/x-matroska",
                                    ".mov" => "video/quicktime",
                                    ".avi" => "video/x-msvideo",
                                    ".wmv" => "video/x-ms-wmv",
                                    ".flv" => "video/x-flv",
                                    _ => "video/webm"
                                };
                                if (stream != null)
                                {
                                    source = MediaSource.CreateFromStream(stream.AsRandomAccessStream(), mimeType);
                                }
                            }
                            else
                            {
                                source = MediaSource.CreateFromUri(new Uri(filePath));
                            }

                            if (token.IsCancellationRequested) { source?.Dispose(); return; }

                            if (source != null)
                            {
                                pageControl.DispatcherQueue.TryEnqueue(() =>
                                {
                                    var mp = pageControl.PagePlayer.MediaPlayer;
                                    if (mp == null || token.IsCancellationRequested) { source.Dispose(); return; }

                                    try
                                    {
                                        // デコード解像度の最適化: 表示サイズに合わせてサーフェスサイズを制限
                                        double scale = pageControl.XamlRoot?.RasterizationScale ?? 1.0;
                                        uint width = (uint)Math.Max(1, pageControl.ActualWidth * scale);
                                        uint height = (uint)Math.Max(1, pageControl.ActualHeight * scale);
                                        mp.SetSurfaceSize(new Windows.Foundation.Size(width, height));

                                        // ソースのセット
                                        mp.Source = source;

                                        // 準備完了時にローディングを消す
                                        void OnMediaOpened(Windows.Media.Playback.MediaPlayer sender, object args)
                                        {
                                            sender.MediaOpened -= OnMediaOpened;
                                            System.Diagnostics.Debug.WriteLine($"[VideoLoader] Media Opened Successfully: {filePath}");
                                            pageControl.DispatcherQueue.TryEnqueue(() =>
                                            {
                                                pageControl.LoadingRing.IsActive = false;
                                            });
                                        }
                                        mp.MediaOpened += OnMediaOpened;

                                        // エラー時も消す
                                        void OnMediaFailed(Windows.Media.Playback.MediaPlayer sender, Windows.Media.Playback.MediaPlayerFailedEventArgs args)
                                        {
                                            sender.MediaFailed -= OnMediaFailed;
                                            System.Diagnostics.Debug.WriteLine($"[VideoLoader] Media Failed! Error: {args.Error}, Message: {args.ErrorMessage}");
                                            pageControl.DispatcherQueue.TryEnqueue(() =>
                                            {
                                                pageControl.LoadingRing.IsActive = false;
                                            });
                                        }
                                        mp.MediaFailed += OnMediaFailed;
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[VideoLoader] UI Thread Exception: {ex.Message}");
                                        pageControl.LoadingRing.IsActive = false;
                                    }
                                });
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[VideoLoader] Source creation failed (NULL)");
                                pageControl.DispatcherQueue.TryEnqueue(() =>
                                {
                                    pageControl.LoadingRing.IsActive = false;
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[VideoLoader] Background Exception: {ex.Message}");
                            pageControl.DispatcherQueue.TryEnqueue(() =>
                            {
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
                // 動画の場合は MediaOpened イベントで終了制御するため、ここでは画像のみ終了させる
                if (!isVideo)
                {
                    _window.DispatcherQueue.TryEnqueue(() => { pageControl.LoadingRing.IsActive = false; });
                }
            }
        }
    }
}
