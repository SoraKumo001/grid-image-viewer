using CommunityToolkit.Mvvm.Messaging;
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
            string[] videoExtensions = { ".webm", ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".flv" };
            bool isVideo = videoExtensions.Contains(ext);

            _window.DispatcherQueue.TryEnqueue(() =>
            {
                if (token.IsCancellationRequested) return;
                pageControl.ResetPlayback();
                if (isVideo)
                {
                    pageControl.LoadingRing.IsActive = true;
                    pageControl.PageCanvas.Visibility = Visibility.Collapsed;

                    // サムネイルがあれば表示
                    var cachedThumb = cacheManager.GetCachedSoftwareBitmap(filePath);
                    if (cachedThumb != null)
                    {
                        var softwareSource = new SoftwareBitmapSource();
                        // 同期的にセットできないため、UIスレッドで非同期にセット
                        _window.DispatcherQueue.TryEnqueue(async () =>
                        {
                            try
                            {
                                if (token.IsCancellationRequested) return;
                                await softwareSource.SetBitmapAsync(cachedThumb);
                                if (token.IsCancellationRequested) return;
                                pageControl.PageImage.Source = softwareSource;
                                pageControl.PageImage.Visibility = Visibility.Visible;
                                pageControl.PageImage.Opacity = 0.5; // 動画が重なるので少し薄くしておく
                            }
                            catch (Exception)
                            {
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
                                        double scale = 1.0;
                                        try
                                        {
                                            // XamlRoot access can throw if the element is not yet in the visual tree
                                            if (pageControl.IsLoaded && pageControl.XamlRoot != null)
                                            {
                                                scale = pageControl.XamlRoot.RasterizationScale;
                                            }
                                        }
                                        catch { }

                                        uint width = (uint)Math.Max(1, pageControl.ActualWidth * scale);
                                        uint height = (uint)Math.Max(1, pageControl.ActualHeight * scale);
                                        try { mp.SetSurfaceSize(new Windows.Foundation.Size(width, height)); } catch { }

                                        // ソースのセット
                                        var playbackItem = new Windows.Media.Playback.MediaPlaybackItem(source);
                                        var playbackList = new Windows.Media.Playback.MediaPlaybackList();
                                        playbackList.AutoRepeatEnabled = true;
                                        playbackList.Items.Add(playbackItem);
                                        mp.Source = playbackList;
                                        mp.IsLoopingEnabled = true; // Safety redundancy

                                        // 準備完了時にローディングを消す
                                        void OnMediaOpened(Windows.Media.Playback.MediaPlayer sender, object args)
                                        {
                                            sender.MediaOpened -= OnMediaOpened;

                                            pageControl.DispatcherQueue.TryEnqueue(() =>
                                            {
                                                if (token.IsCancellationRequested) return;
                                                pageControl.LoadingRing.IsActive = false;
                                                pageControl.PageImage.Visibility = Visibility.Collapsed;
                                                pageControl.PageImage.Opacity = 1.0;

                                                // Ensure playback starts
                                                sender.Play();

                                                // 動画読み込み完了後にフォーカスをRootGridに戻す
                                                WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
                                            });
                                        }
                                        mp.MediaOpened += OnMediaOpened;

                                        // エラー時も消す
                                        void OnMediaFailed(Windows.Media.Playback.MediaPlayer sender, Windows.Media.Playback.MediaPlayerFailedEventArgs args)
                                        {
                                            sender.MediaFailed -= OnMediaFailed;
                                            string errorMsg = args.Error switch
                                            {
                                                Windows.Media.Playback.MediaPlayerError.Aborted => "Playback aborted",
                                                Windows.Media.Playback.MediaPlayerError.NetworkError => "Network error",
                                                Windows.Media.Playback.MediaPlayerError.DecodingError => "Decoding error (Missing Codec?)",
                                                Windows.Media.Playback.MediaPlayerError.SourceNotSupported => "Source format not supported",
                                                _ => "Unknown playback error"
                                            };

                                            pageControl.DispatcherQueue.TryEnqueue(() =>
                                            {
                                                if (token.IsCancellationRequested) return;
                                                pageControl.LoadingRing.IsActive = false;
                                                _window.ShowNotification($"Video Error: {errorMsg}");
                                                WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
                                            });
                                        }
                                        mp.MediaFailed += OnMediaFailed;
                                    }
                                    catch (Exception)
                                    {
                                        pageControl.LoadingRing.IsActive = false;
                                    }
                                });
                            }
                            else
                            {
                                pageControl.DispatcherQueue.TryEnqueue(() =>
                                {
                                    if (token.IsCancellationRequested) return;
                                    pageControl.LoadingRing.IsActive = false;
                                });
                            }
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
