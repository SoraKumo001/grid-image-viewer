using quick_image_viewer.Common;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace quick_image_viewer.Managers
{
    internal class ViewerCacheManager : IViewerCacheManager
    {
        private readonly IViewerStateService _state;
        private readonly ISettingsManager _settings;
        private readonly Dictionary<string, byte[]> _imageCache = new Dictionary<string, byte[]>();
        private readonly Dictionary<string, SoftwareBitmap> _softwareBitmapCache = new Dictionary<string, SoftwareBitmap>();

        private CancellationTokenSource? _folderPreloadCts;
        private FolderPreloadCache? _folderPreloadCache;
        private string? _lastPreloadedDirectory;
        private CancellationTokenSource? _preloadCts;

        private sealed record FolderPreloadCache(
            string SourceDirectory,
            string? NextFolder,
            List<string>? NextPlaylist,
            string? PrevFolder,
            List<string>? PrevPlaylist);

        public ViewerCacheManager(IViewerStateService state, ISettingsManager settings)
        {
            _state = state;
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

        public SoftwareBitmap? GetCachedSoftwareBitmap(string filePath)
        {
            lock (_softwareBitmapCache)
            {
                _softwareBitmapCache.TryGetValue(filePath, out var bitmap);
                return bitmap;
            }
        }

        public async Task PreloadPathsAsync(List<string> paths, CancellationToken token)
        {
            if (paths == null || paths.Count == 0) return;

            foreach (var path in paths)
            {
                if (token.IsCancellationRequested) break;
                if (string.IsNullOrEmpty(path)) continue;

                bool isVideo = MediaHelper.IsVideo(path);

                if (isVideo)
                {
                    // 動画のサムネイル表示機能を削除したため、ここではプリロードを行わない
                    continue;
                }

                // 1. バイナリキャッシュを確認・作成
                bool needsBinary = false;
                lock (_imageCache) { if (!_imageCache.ContainsKey(path)) needsBinary = true; }

                if (needsBinary)
                {
                    try
                    {
                        byte[] bytes;
                        if (ArchiveManager.IsArchivePath(path))
                        {
                            var (arc, ent) = ArchiveManager.SplitArchivePath(path);
                            bytes = ArchiveManager.GetEntryBytes(arc, ent) ?? Array.Empty<byte>();
                        }
                        else
                        {
                            using var stream = System.IO.File.OpenRead(path);
                            using var ms = new MemoryStream();
                            await stream.CopyToAsync(ms, token);
                            bytes = ms.ToArray();
                        }

                        if (bytes.Length > 0)
                        {
                            lock (_imageCache)
                            {
                                if (_imageCache.Count >= Constants.MAX_CACHE_SIZE) _imageCache.Remove(_imageCache.Keys.First());
                                _imageCache[path] = bytes;
                            }
                        }
                    }
                    catch { continue; }
                }

                // 2. ソフトウェアビットマップキャッシュを確認・作成
                bool needsBitmap = false;
                lock (_softwareBitmapCache) { if (!_softwareBitmapCache.ContainsKey(path)) needsBitmap = true; }

                if (needsBitmap)
                {
                    // バックグラウンドでデコード
                    _ = Task.Run(async () =>
                    {
                        if (token.IsCancellationRequested) return;
                        try
                        {
                            byte[]? bytes = GetCachedBytes(path);
                            if (bytes == null) return;

                            using var ms = new MemoryStream(bytes);
                            using var stream = ms.AsRandomAccessStream();
                            var decoder = await BitmapDecoder.CreateAsync(stream);
                            var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                                BitmapPixelFormat.Bgra8,
                                BitmapAlphaMode.Premultiplied);

                            lock (_softwareBitmapCache)
                            {
                                AddSoftwareBitmapToCache(path, softwareBitmap);
                            }
                        }
                        catch { }
                    }, token);
                }
            }
        }

        private void AddSoftwareBitmapToCache(string path, SoftwareBitmap bitmap)
        {
            if (_softwareBitmapCache.TryGetValue(path, out var existing))
            {
                existing.Dispose();
            }
            else if (_softwareBitmapCache.Count >= Constants.MAX_BITMAP_CACHE_SIZE)
            {
                var firstKey = _softwareBitmapCache.Keys.First();
                _softwareBitmapCache[firstKey].Dispose();
                _softwareBitmapCache.Remove(firstKey);
            }
            _softwareBitmapCache[path] = bitmap;
        }

        public async Task PreloadAroundAsync(int currentIndex, List<string> playlist, int splitCount)
        {
            if (playlist.Count == 0) return;

            _preloadCts?.Cancel();
            _preloadCts?.Dispose();
            _preloadCts = new CancellationTokenSource();
            var token = _preloadCts.Token;

            var indicesToPreload = new List<int>();
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
                bool isVideo = MediaHelper.IsVideo(path);

                if (isVideo)
                {
                    // 動画のサムネイル表示機能を削除したため、ここではプリロードを行わない
                    continue;
                }

                bool needsBinary = false;
                lock (_imageCache) { if (!_imageCache.ContainsKey(path)) needsBinary = true; }

                if (needsBinary)
                {
                    try
                    {
                        byte[] bytes;
                        if (ArchiveManager.IsArchivePath(path))
                        {
                            var (arc, ent) = ArchiveManager.SplitArchivePath(path);
                            bytes = ArchiveManager.GetEntryBytes(arc, ent) ?? Array.Empty<byte>();
                        }
                        else
                        {
                            using var stream = System.IO.File.OpenRead(path);
                            using var ms = new MemoryStream();
                            await stream.CopyToAsync(ms, token);
                            bytes = ms.ToArray();
                        }

                        if (bytes.Length > 0)
                        {
                            lock (_imageCache)
                            {
                                if (_imageCache.Count >= Constants.MAX_CACHE_SIZE) _imageCache.Remove(_imageCache.Keys.First());
                                _imageCache[path] = bytes;
                            }
                        }
                    }
                    catch { continue; }
                }

                bool isVeryNear = (idx - currentIndex + playlist.Count) % playlist.Count < 2 * splitCount;
                if (isVeryNear)
                {
                    bool needsBitmap = false;
                    lock (_softwareBitmapCache) { if (!_softwareBitmapCache.ContainsKey(path)) needsBitmap = true; }

                    if (needsBitmap)
                    {
                        _ = Task.Run(async () =>
                        {
                            if (token.IsCancellationRequested) return;
                            try
                            {
                                byte[]? bytes = GetCachedBytes(path);
                                if (bytes == null) return;

                                using var ms = new MemoryStream(bytes);
                                using var stream = ms.AsRandomAccessStream();
                                var decoder = await BitmapDecoder.CreateAsync(stream);
                                var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                                    BitmapPixelFormat.Bgra8,
                                    BitmapAlphaMode.Premultiplied);

                                lock (_softwareBitmapCache) { AddSoftwareBitmapToCache(path, softwareBitmap); }
                            }
                            catch { }
                        }, token);
                    }
                }
            }
        }

        public async Task PreloadFoldersAsync(string currentDir)
        {
            currentDir = NormalizeDirectoryPath(currentDir);
            if (string.IsNullOrEmpty(currentDir) ||
                (currentDir == _lastPreloadedDirectory && _folderPreloadCache?.SourceDirectory == currentDir))
            {
                return;
            }

            _folderPreloadCts?.Cancel();
            _folderPreloadCts?.Dispose();
            _folderPreloadCts = new CancellationTokenSource();
            var token = _folderPreloadCts.Token;

            var allowedExtensions = _settings.EnabledExtensions;

            try
            {
                string? nextFolder = await Task.Run(() => FileNavigator.FindNextImageFolder(currentDir, 1, allowedExtensions, token), token);
                if (token.IsCancellationRequested) return;
                List<string>? nextPlaylist = !string.IsNullOrEmpty(nextFolder)
                    ? await Task.Run(() => FolderDiscoveryService.GetInitialPlaylist(nextFolder, allowedExtensions), token)
                    : null;
                if (token.IsCancellationRequested) return;
                string? prevFolder = await Task.Run(() => FileNavigator.FindNextImageFolder(currentDir, -1, allowedExtensions, token), token);
                if (token.IsCancellationRequested) return;
                List<string>? prevPlaylist = !string.IsNullOrEmpty(prevFolder)
                    ? await Task.Run(() => FolderDiscoveryService.GetInitialPlaylist(prevFolder, allowedExtensions), token)
                    : null;
                if (token.IsCancellationRequested) return;

                _folderPreloadCache = new FolderPreloadCache(currentDir, nextFolder, nextPlaylist, prevFolder, prevPlaylist);
                _lastPreloadedDirectory = currentDir;
            }
            catch { }
        }

        public (string? Path, List<string>? Playlist) GetPreloadedFolderData(string currentDir, int offset)
        {
            currentDir = NormalizeDirectoryPath(currentDir);
            var cache = _folderPreloadCache;
            if (cache == null ||
                !string.Equals(cache.SourceDirectory, currentDir, StringComparison.OrdinalIgnoreCase))
            {
                return (null, null);
            }

            if (offset > 0) return (cache.NextFolder, cache.NextPlaylist);
            if (offset < 0) return (cache.PrevFolder, cache.PrevPlaylist);
            return (null, null);
        }

        private static string NormalizeDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            string normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (normalized.Length == 2 && normalized[1] == ':') normalized += Path.DirectorySeparatorChar;
            return normalized;
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
            lock (_softwareBitmapCache)
            {
                foreach (var sb in _softwareBitmapCache.Values) sb.Dispose();
                _softwareBitmapCache.Clear();
            }
        }
    }
}
