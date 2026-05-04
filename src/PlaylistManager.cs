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

            await FolderDiscoveryService.DiscoverFilesAsync(path, includeSiblings, includeSubfolders, (newFiles) =>
            {
                _window.DispatcherQueue.TryEnqueue(() =>
                {
                    if (token.IsCancellationRequested) return;

                    accumulatedFiles.AddRange(newFiles);
                    var sorted = accumulatedFiles.Distinct().OrderBy(f => f, comparer).ToList();
                    accumulatedFiles = sorted;

                    string currentPath = _window.CurrentImagePath;
                    UpdatePlaylist(sorted, currentPath, true);
                    WeakReferenceMessenger.Default.Send(new PlaylistUpdatedMessage(false));
                });
            }, token);

            _window.DispatcherQueue.TryEnqueue(() =>
            {
                _window.IsSearchingFolder = false;
            });
        }
    }
}