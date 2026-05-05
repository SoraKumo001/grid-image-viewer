using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace grid_image_viewer
{
    internal class PlaylistManager
    {
        private readonly MainWindow _window;
        private readonly SettingsManager _settings;
        private CancellationTokenSource? _loadCts;

        public PlaylistManager(MainWindow window, SettingsManager settings)
        {
            _window = window;
            _settings = settings;
        }

        public void Navigate(int offset, bool forceSingleStep)
        {
            if (_window.Playlist.Count == 0) return;

            int step = forceSingleStep ? 1 : _settings.MangaSplitCount;
            int actualOffset = offset * step;

            int newIndex = _window.CurrentIndex + actualOffset;

            if (newIndex < 0)
            {
                int action = _settings.BoundaryAction;
                if (action == 1) // NextFolder (Previous)
                {
                    NavigateFolder(-1);
                }
                else if (action == 2) // Loop
                {
                    _window.CurrentIndex = _window.Playlist.Count - 1;
                    _window.ViewerManager.ShowNotification(new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader().GetString("Notification_LoopedEnd"));
                    _ = _window.UpdateDisplayAsync();
                }
                // action == 0 (None) -> do nothing
                return;
            }
            if (newIndex >= _window.Playlist.Count)
            {
                int action = _settings.BoundaryAction;
                if (action == 1) // NextFolder
                {
                    NavigateFolder(1);
                }
                else if (action == 2) // Loop
                {
                    _window.CurrentIndex = 0;
                    _window.ViewerManager.ShowNotification(new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader().GetString("Notification_LoopedStart"));
                    _ = _window.UpdateDisplayAsync();
                }
                // action == 0 (None) -> do nothing
                return;
            }

            _window.CurrentIndex = newIndex;
            _ = _window.UpdateDisplayAsync();
        }

        public void NavigateFolder(int offset)
        {
            string currentDir = _window.CurrentDirectory;
            if (string.IsNullOrEmpty(currentDir)) return;

            _ = Task.Run(() =>
            {
                string? targetDir = FileNavigator.FindNextImageFolder(currentDir, offset);
                _window.DispatcherQueue.TryEnqueue(() =>
                {
                    if (!string.IsNullOrEmpty(targetDir))
                    {
                        _window.LoadDirectory(targetDir, string.Empty);
                    }
                    else
                    {
                        _window.ViewerManager.ShowNotification(offset > 0 ? "No more folders (End)" : "No more folders (Start)");
                    }
                });
            });
        }

        public void LoadDirectory(string path, string initialFile = "", bool includeSiblings = false, bool includeSubfolders = false, List<string>? preloadedPlaylist = null)
        {
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            _window.CurrentDirectory = path;
            _window.IsSearchingFolder = true;
            _window.Playlist.Clear();
            var token = _loadCts.Token;

            _ = Task.Run(() =>
            {
                try
                {
                    List<string> initialFiles = preloadedPlaylist ?? FolderDiscoveryService.GetInitialPlaylist(path);

                    _window.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        UpdatePlaylist(initialFiles, initialFile, true);
                        OnInitialFilesLoaded(path, initialFile, includeSiblings, includeSubfolders, token);
                    });
                }
                catch
                {
                    _window.DispatcherQueue.TryEnqueue(() =>
                    {
                        _window.IsSearchingFolder = false;
                    });
                }
            });
        }

        private void OnInitialFilesLoaded(string path, string initialFile, bool includeSiblings, bool includeSubfolders, CancellationToken token)
        {
            if (_window.Playlist.Count > 0)
            {
                WeakReferenceMessenger.Default.Send(new PlaylistUpdatedMessage(false));

                if (!ArchiveManager.IsArchive(path) && (includeSiblings || includeSubfolders))
                {
                    _ = Task.Run(() => DiscoverAdditionalFilesAsync(path, initialFile, includeSiblings, includeSubfolders, token));
                }
                else
                {
                    _window.IsSearchingFolder = false;
                }
            }
            else if (includeSiblings || includeSubfolders)
            {
                _ = Task.Run(() => DiscoverAdditionalFilesAsync(path, initialFile, includeSiblings, includeSubfolders, token));
            }
            else
            {
                _window.IsSearchingFolder = false;
            }
        }

        private void UpdatePlaylist(List<string> files, string targetPath, bool alreadySorted)
        {
            string currentPath = !string.IsNullOrEmpty(targetPath) ? targetPath : _window.CurrentImagePath;
            List<string> sortedFiles;
            if (alreadySorted)
            {
                sortedFiles = files;
            }
            else
            {
                sortedFiles = files.Distinct().OrderBy(f => f, new NaturalStringComparer()).ToList();
            }

            _window.ViewModel.Playlist = new ObservableCollection<string>(sortedFiles);

            if (_window.Playlist.Count > 0)
            {
                int idx = _window.Playlist.IndexOf(currentPath);
                _window.CurrentIndex = idx >= 0 ? idx : 0;
            }
            else
            {
                _window.CurrentIndex = -1;
            }
        }

        private async Task DiscoverAdditionalFilesAsync(string path, string initialFile, bool includeSiblings, bool includeSubfolders, CancellationToken token)
        {
            List<string> accumulatedFiles = new List<string>(_window.Playlist);
            var comparer = new NaturalStringComparer();
            var lastUpdate = DateTime.Now;

            await FolderDiscoveryService.DiscoverFilesAsync(path, includeSiblings, includeSubfolders, (newFiles) =>
            {
                // 修正: 重い並び替え処理（Distinct/OrderBy）をバックグラウンドスレッドで実行する
                accumulatedFiles.AddRange(newFiles);
                var sorted = accumulatedFiles.Distinct().OrderBy(f => f, comparer).ToList();
                accumulatedFiles = sorted;

                // 修正: UI スレッドへの通知頻度を制限する（1秒以上経過した場合のみ更新）
                if ((DateTime.Now - lastUpdate).TotalMilliseconds > 1000)
                {
                    lastUpdate = DateTime.Now;
                    _window.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (token.IsCancellationRequested) return;

                        string currentPath = _window.CurrentImagePath;
                        UpdatePlaylist(sorted, currentPath, true);
                        WeakReferenceMessenger.Default.Send(new PlaylistUpdatedMessage(false));
                    });
                }
            }, token);

            // 最終更新とステータス変更
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                if (!token.IsCancellationRequested)
                {
                    UpdatePlaylist(accumulatedFiles, _window.CurrentImagePath, true);
                    WeakReferenceMessenger.Default.Send(new PlaylistUpdatedMessage(false));
                }
                _window.IsSearchingFolder = false;
            });
        }
    }
}