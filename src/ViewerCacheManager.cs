using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace grid_image_viewer
{
    internal class ViewerCacheManager
    {
        private readonly MainWindow _window;
        private readonly SettingsManager _settings;
        private readonly Dictionary<string, byte[]> _imageCache = new Dictionary<string, byte[]>();
        private readonly Dictionary<string, Microsoft.UI.Xaml.Media.Imaging.BitmapImage> _bitmapCache = new Dictionary<string, Microsoft.UI.Xaml.Media.Imaging.BitmapImage>();
        private const int MAX_CACHE_SIZE = 20;
        private const int MAX_BITMAP_CACHE_SIZE = 5;

        private CancellationTokenSource? _folderPreloadCts;
        private string? _cachedNextFolder;
        private string? _cachedPrevFolder;
        private List<string>? _cachedNextPlaylist;
        private List<string>? _cachedPrevPlaylist;
        private string? _lastPreloadedDirectory;
        private CancellationTokenSource? _preloadCts;

        public ViewerCacheManager(MainWindow window, SettingsManager settings)
        {
            _window = window;
            _settings = settings;
        }

        public byte[]? GetCachedBytes(string filePath)
        {
            lock (_imageCache)
            {
                _imageCache.TryGetValue(filePath, out var bytes);
                return bytes;
            }
        }

        public Microsoft.UI.Xaml.Media.Imaging.BitmapImage? GetCachedBitmap(string filePath)
        {
            lock (_bitmapCache)
            {
                _bitmapCache.TryGetValue(filePath, out var bitmap);
                return bitmap;
            }
        }

        public async Task PreloadAroundAsync(int currentIndex, List<string> playlist, int splitCount)
        {
            if (playlist.Count == 0) return;

            _preloadCts?.Cancel();
            _preloadCts?.Dispose();
            _preloadCts = new CancellationTokenSource();
            var token = _preloadCts.Token;

            var indicesToPreload = new List<int>();
            // 優先度の高い順（次の画像 -> そのさらに次 -> 前の画像）
            for (int i = 0; i < 2 * splitCount; i++)
            {
                int nextIdx = (currentIndex + splitCount + i) % playlist.Count;
                if (nextIdx < 0) nextIdx += playlist.Count;
                indicesToPreload.Add(nextIdx);
            }
            for (int i = 1; i <= splitCount; i++)
            {
                int prevIdx = (currentIndex - i) % playlist.Count;
                if (prevIdx < 0) prevIdx += playlist.Count;
                indicesToPreload.Add(prevIdx);
            }

            foreach (var idx in indicesToPreload)
            {
                if (token.IsCancellationRequested) break;
                var path = playlist[idx];

                bool needsBinary = false;
                lock (_imageCache) { if (!_imageCache.ContainsKey(path)) needsBinary = true; }

                if (needsBinary)
                {
                    try
                    {
                        var bytes = await File.ReadAllBytesAsync(path, token);
                        lock (_imageCache)
                        {
                            if (_imageCache.Count >= MAX_CACHE_SIZE) _imageCache.Remove(_imageCache.Keys.First());
                            _imageCache[path] = bytes;
                        }
                    }
                    catch { continue; }
                }

                // 次の1画面分（splitCount分）はデコードまで行う
                bool isVeryNear = false;
                int distance = (idx - currentIndex + playlist.Count) % playlist.Count;
                if (distance >= 0 && distance < 2 * splitCount) isVeryNear = true;

                if (isVeryNear)
                {
                    bool needsBitmap = false;
                    lock (_bitmapCache) { if (!_bitmapCache.ContainsKey(path)) needsBitmap = true; }

                    if (needsBitmap)
                    {
                        _window.DispatcherQueue.TryEnqueue(async () =>
                        {
                            if (token.IsCancellationRequested) return;
                            try
                            {
                                byte[]? bytes = GetCachedBytes(path);
                                if (bytes == null) return;

                                using var ms = new MemoryStream(bytes);
                                var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                                await bitmap.SetSourceAsync(ms.AsRandomAccessStream()).AsTask();

                                lock (_bitmapCache)
                                {
                                    if (_bitmapCache.Count >= MAX_BITMAP_CACHE_SIZE) _bitmapCache.Remove(_bitmapCache.Keys.First());
                                    _bitmapCache[path] = bitmap;
                                }
                            }
                            catch { }
                        });
                    }
                }
            }
        }

        public async Task PreloadFoldersAsync(string currentDir)
        {
            if (string.IsNullOrEmpty(currentDir) || currentDir == _lastPreloadedDirectory) return;
            _folderPreloadCts?.Cancel();
            _folderPreloadCts?.Dispose();
            _folderPreloadCts = new CancellationTokenSource();
            var token = _folderPreloadCts.Token;
            _lastPreloadedDirectory = currentDir;
            try
            {
                _cachedNextFolder = await Task.Run(() => FileNavigator.FindNextImageFolder(currentDir, 1, token), token);
                if (token.IsCancellationRequested) return;
                if (!string.IsNullOrEmpty(_cachedNextFolder)) _cachedNextPlaylist = await Task.Run(() => FolderDiscoveryService.GetInitialPlaylist(_cachedNextFolder), token);
                if (token.IsCancellationRequested) return;
                _cachedPrevFolder = await Task.Run(() => FileNavigator.FindNextImageFolder(currentDir, -1, token), token);
                if (token.IsCancellationRequested) return;
                if (!string.IsNullOrEmpty(_cachedPrevFolder)) _cachedPrevPlaylist = await Task.Run(() => FolderDiscoveryService.GetInitialPlaylist(_cachedPrevFolder), token);
            }
            catch { }
        }

        public void CancelPreloads()
        {
            _folderPreloadCts?.Cancel();
            _preloadCts?.Cancel();
        }

        public void ClearCache()
        {
            lock (_imageCache)
            {
                _imageCache.Clear();
            }
            lock (_bitmapCache)
            {
                _bitmapCache.Clear();
            }
        }
    }
}
