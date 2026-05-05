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
        private const int MAX_CACHE_SIZE = 20;

        private CancellationTokenSource? _folderPreloadCts;
        private string? _cachedNextFolder;
        private string? _cachedPrevFolder;
        private List<string>? _cachedNextPlaylist;
        private List<string>? _cachedPrevPlaylist;
        private string? _lastPreloadedDirectory;

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

        public async Task PreloadAroundAsync(int currentIndex, List<string> playlist, int splitCount)
        {
            if (playlist.Count == 0) return;
            var indicesToPreload = new List<int>();
            for (int i = 1; i <= splitCount; i++)
            {
                int prevIdx = (currentIndex - i) % playlist.Count;
                if (prevIdx < 0) prevIdx += playlist.Count;
                indicesToPreload.Add(prevIdx);
            }
            for (int i = 0; i < 2 * splitCount; i++)
            {
                int nextIdx = (currentIndex + splitCount + i) % playlist.Count;
                if (nextIdx < 0) nextIdx += playlist.Count;
                indicesToPreload.Add(nextIdx);
            }

            foreach (var idx in indicesToPreload)
            {
                var path = playlist[idx];
                lock (_imageCache) { if (_imageCache.ContainsKey(path)) continue; }
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var bytes = await File.ReadAllBytesAsync(path);
                        lock (_imageCache)
                        {
                            if (_imageCache.Count >= MAX_CACHE_SIZE) _imageCache.Remove(_imageCache.Keys.First());
                            _imageCache[path] = bytes;
                        }
                    }
                    catch { }
                });
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
        }

        public void ClearCache()
        {
            lock (_imageCache)
            {
                _imageCache.Clear();
            }
        }
    }
}
