using Microsoft.UI.Xaml;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Managers;
using quick_image_viewer.Services.LoaderStrategies;
using quick_image_viewer.Views.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace quick_image_viewer.Services
{
    internal class ViewerImageLoader : IViewerImageLoader
    {
        private readonly IViewerLoaderHost _window;
        private readonly ISettingsManager _settings;
        private readonly List<ILoaderStrategy> _strategies;

        public ViewerImageLoader(IViewerLoaderHost window, ISettingsManager settings)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _settings = settings;

            // Register strategies in priority order
            _strategies = new List<ILoaderStrategy>
            {
                new EditSessionLoaderStrategy(_window),
                new VideoLoaderStrategy(_window),
                new PdfLoaderStrategy(_window),
                new SkiaImageLoaderStrategy(_window),
                new NormalImageLoaderStrategy(_window)
            };
        }

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

            await pageControl.ResetPlaybackAsync();

            PrepareUIForLoading(filePath, pageControl, isVideo, token, cacheManager);

            try
            {
                var strategy = _strategies.FirstOrDefault(s => s.CanHandle(filePath));
                if (strategy != null)
                {
                    await strategy.LoadAsync(filePath, pageControl, renderer, pageIndex, token, cacheManager);
                }
            }
            catch (Exception)
            {
                // Error handling is managed within strategies or reported via notification
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
    }
}
