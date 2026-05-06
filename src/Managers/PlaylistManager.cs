using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Dispatching;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Services;
using quick_image_viewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace quick_image_viewer.Managers
{
    internal class PlaylistManager : IPlaylistManager
    {
        private readonly IViewerStateService _state;
        private readonly ISettingsManager _settings;
        private readonly INotificationService _notification;
        private readonly DispatcherQueue _dispatcherQueue;
        private CancellationTokenSource? _loadCts;

        public PlaylistManager(IViewerStateService state, ISettingsManager settings, INotificationService notification)
        {
            _state = state;
            _settings = settings;
            _notification = notification;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }

        public void Navigate(int offset, bool forceSingleStep)
        {
            if (_state.Playlist.Count == 0) return;

            int step = forceSingleStep ? 1 : _settings.MangaSplitCount;
            int actualOffset = offset * step;

            int newIndex = _state.CurrentIndex + actualOffset;

            if (newIndex < 0)
            {
                int action = _settings.BoundaryAction;
                if (action == 1) // NextFolder (Previous)
                {
                    NavigateFolder(-1);
                }
                else if (action == 2) // Loop
                {
                    _state.CurrentIndex = _state.Playlist.Count - 1;
                    _notification.Show(new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader().GetString("Notification_LoopedEnd"));
                    WeakReferenceMessenger.Default.Send(new RefreshDisplayMessage());
                }
                return;
            }
            if (newIndex >= _state.Playlist.Count)
            {
                int action = _settings.BoundaryAction;
                if (action == 1) // NextFolder
                {
                    NavigateFolder(1);
                }
                else if (action == 2) // Loop
                {
                    _state.CurrentIndex = 0;
                    _notification.Show(new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader().GetString("Notification_LoopedStart"));
                    WeakReferenceMessenger.Default.Send(new RefreshDisplayMessage());
                }
                return;
            }

            _state.CurrentIndex = newIndex;
            WeakReferenceMessenger.Default.Send(new RefreshDisplayMessage());
        }

        public void NavigateFolder(int offset)
        {
            string currentDir = _state.CurrentDirectory;
            if (string.IsNullOrEmpty(currentDir)) return;

            _ = Task.Run(() =>
            {
                string? targetDir = FileNavigator.FindNextImageFolder(currentDir, offset);
                _dispatcherQueue.TryEnqueue(() =>
                {
                    if (!string.IsNullOrEmpty(targetDir))
                    {
                        LoadDirectory(targetDir, string.Empty);
                    }
                    else
                    {
                        var loader = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader();
                        _notification.Show(loader.GetString(offset > 0 ? "Notification_NoMoreFoldersEnd" : "Notification_NoMoreFoldersStart"));
                    }
                });
            });
        }

        public void LoadDirectory(string path, string initialFile = "", bool includeSiblings = false, bool includeSubfolders = false, List<string>? preloadedPlaylist = null)
        {
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            _state.CurrentDirectory = path;
            _state.IsSearchingFolder = true;
            _state.Playlist.Clear();
            var token = _loadCts.Token;

            _ = Task.Run(() =>
            {
                try
                {
                    List<string> initialFiles = preloadedPlaylist ?? FolderDiscoveryService.GetInitialPlaylist(path);

                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        UpdatePlaylist(initialFiles, initialFile, true);
                        OnInitialFilesLoaded(path, initialFile, includeSiblings, includeSubfolders, token);
                    });
                }
                catch
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        _state.IsSearchingFolder = false;
                    });
                }
            });
        }

        private void OnInitialFilesLoaded(string path, string initialFile, bool includeSiblings, bool includeSubfolders, CancellationToken token)
        {
            if (_state.Playlist.Count > 0)
            {
                WeakReferenceMessenger.Default.Send(new PlaylistUpdatedMessage(false));

                if (!ArchiveManager.IsArchive(path) && (includeSiblings || includeSubfolders))
                {
                    _ = Task.Run(() => DiscoverAdditionalFilesAsync(path, initialFile, includeSiblings, includeSubfolders, token));
                }
                else
                {
                    _state.IsSearchingFolder = false;
                }
            }
            else if (includeSiblings || includeSubfolders)
            {
                _ = Task.Run(() => DiscoverAdditionalFilesAsync(path, initialFile, includeSiblings, includeSubfolders, token));
            }
            else
            {
                _state.IsSearchingFolder = false;
            }
        }

        private void UpdatePlaylist(List<string> files, string targetPath, bool alreadySorted)
        {
            string currentPath = !string.IsNullOrEmpty(targetPath) ? targetPath : _state.CurrentImagePath;
            List<string> sortedFiles;
            if (alreadySorted)
            {
                sortedFiles = files;
            }
            else
            {
                sortedFiles = files.Distinct().OrderBy(f => f, new NaturalStringComparer()).ToList();
            }

            _state.Playlist = new ObservableCollection<string>(sortedFiles);

            if (_state.Playlist.Count > 0)
            {
                int idx = _state.Playlist.IndexOf(currentPath);
                _state.CurrentIndex = idx >= 0 ? idx : 0;
            }
            else
            {
                _state.CurrentIndex = -1;
            }
        }

        private async Task DiscoverAdditionalFilesAsync(string path, string initialFile, bool includeSiblings, bool includeSubfolders, CancellationToken token)
        {
            List<string> accumulatedFiles = new List<string>(_state.Playlist);
            var comparer = new NaturalStringComparer();
            var lastUpdate = DateTime.Now;

            await FolderDiscoveryService.DiscoverFilesAsync(path, includeSiblings, includeSubfolders, (newFiles) =>
            {
                accumulatedFiles.AddRange(newFiles);
                var sorted = accumulatedFiles.Distinct().OrderBy(f => f, comparer).ToList();
                accumulatedFiles = sorted;

                if ((DateTime.Now - lastUpdate).TotalMilliseconds > 1000)
                {
                    lastUpdate = DateTime.Now;
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        if (token.IsCancellationRequested) return;

                        string currentPath = _state.CurrentImagePath;
                        UpdatePlaylist(sorted, currentPath, true);
                        WeakReferenceMessenger.Default.Send(new PlaylistUpdatedMessage(false));
                    });
                }
            }, token);

            _dispatcherQueue.TryEnqueue(() =>
            {
                if (!token.IsCancellationRequested)
                {
                    UpdatePlaylist(accumulatedFiles, _state.CurrentImagePath, true);
                    WeakReferenceMessenger.Default.Send(new PlaylistUpdatedMessage(false));
                }
                _state.IsSearchingFolder = false;
            });
        }

        public void RemoveFromPlaylist(string path)
        {
            int index = _state.Playlist.IndexOf(path);
            if (index != -1)
            {
                if (path == _state.CurrentImagePath)
                {
                    Navigate(1, true);
                }

                _state.Playlist.RemoveAt(index);
                // GridItems management moved to GridManager via message

                if (_state.CurrentIndex > index) _state.CurrentIndex--;
                if (_state.CurrentIndex >= _state.Playlist.Count) _state.CurrentIndex = _state.Playlist.Count - 1;

                WeakReferenceMessenger.Default.Send(new PlaylistUpdatedMessage(false));
            }
        }
    }
}