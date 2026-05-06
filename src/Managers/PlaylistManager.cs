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
    internal class PlaylistManager : IPlaylistManager,
        IRecipient<NavigationMessage>,
        IRecipient<FolderNavigationMessage>,
        IRecipient<LoadDirectoryMessage>
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

            WeakReferenceMessenger.Default.Register<NavigationMessage>(this);
            WeakReferenceMessenger.Default.Register<FolderNavigationMessage>(this);
            WeakReferenceMessenger.Default.Register<LoadDirectoryMessage>(this);
        }

        public void Receive(NavigationMessage message) => Navigate(message.Offset, message.ForceSingleStep);
        public void Receive(FolderNavigationMessage message) => NavigateFolder(message.Offset);
        public void Receive(LoadDirectoryMessage message) => LoadDirectory(message.Path, message.InitialFile, message.IncludeSiblings, message.IncludeSubfolders, message.PreloadedPlaylist);

        public int GetEffectiveSplitCount()
        {
            int splitCount = _settings.MangaSplitCount;
            if (splitCount <= 1 || _state.Playlist.Count == 0) return 1;

            int currentIndex = _state.CurrentIndex;
            if (currentIndex < 0 || currentIndex >= _state.Playlist.Count) return 1;

            int remaining = _state.Playlist.Count - currentIndex;
            int effective = Math.Min(splitCount, remaining);

            // Archives (Covers) are always single page in folder view
            if (ArchiveManager.IsArchive(_state.Playlist[currentIndex]) && !ArchiveManager.IsArchivePath(_state.Playlist[currentIndex]))
            {
                return 1;
            }

            // Normal images: stop spread if we hit an archive
            for (int i = 1; i < effective; i++)
            {
                if (ArchiveManager.IsArchive(_state.Playlist[currentIndex + i]) && !ArchiveManager.IsArchivePath(_state.Playlist[currentIndex + i]))
                {
                    return i;
                }
            }
            return effective;
        }

        public void Navigate(int offset, bool forceSingleStep)
        {
            if (_state.Playlist.Count == 0) return;

            int step = _settings.MangaSplitCount;
            int newIndex = _state.CurrentIndex;

            if (forceSingleStep || step <= 1)
            {
                newIndex = _state.CurrentIndex + offset;
            }
            else if (offset > 0)
            {
                // Go forward by current visible spreads
                for (int i = 0; i < offset; i++)
                {
                    newIndex += GetEffectiveSplitCountForIndex(newIndex);
                    if (newIndex >= _state.Playlist.Count) break;
                }
            }
            else
            {
                // Go backward by previous spreads
                for (int i = 0; i < Math.Abs(offset); i++)
                {
                    if (newIndex <= 0) break;

                    int target = newIndex - 1;
                    if (ArchiveManager.IsArchive(_state.Playlist[target]) && !ArchiveManager.IsArchivePath(_state.Playlist[target]))
                    {
                        // The item before is an archive, it's a spread of its own
                        newIndex = target;
                    }
                    else
                    {
                        // The item before is a normal image, look back to find spread start
                        int moved = 0;
                        while (target > 0 && moved < step - 1)
                        {
                            if (ArchiveManager.IsArchive(_state.Playlist[target - 1]) && !ArchiveManager.IsArchivePath(_state.Playlist[target - 1]))
                                break;
                            target--;
                            moved++;
                        }
                        newIndex = target;
                    }
                }
            }

            // Boundary checks
            if (newIndex < 0 || (offset < 0 && newIndex == _state.CurrentIndex && _state.CurrentIndex == 0))
            {
                int action = _settings.BoundaryAction;
                if (action == 1) { NavigateFolder(-1); return; }
                else if (action == 2)
                {
                    // Loop to end: find the START of the LAST spread
                    int lastStart = _state.Playlist.Count - 1;
                    int moved = 0;
                    while (lastStart > 0 && moved < step - 1)
                    {
                        if (ArchiveManager.IsArchive(_state.Playlist[lastStart]) && !ArchiveManager.IsArchivePath(_state.Playlist[lastStart])) break;
                        if (ArchiveManager.IsArchive(_state.Playlist[lastStart - 1]) && !ArchiveManager.IsArchivePath(_state.Playlist[lastStart - 1])) break;
                        lastStart--;
                        moved++;
                    }
                    newIndex = lastStart;
                    _notification.Show(new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader().GetString("Notification_LoopedEnd"));
                }
                else { newIndex = 0; }
            }
            else if (newIndex >= _state.Playlist.Count)
            {
                int action = _settings.BoundaryAction;
                if (action == 1) { NavigateFolder(1); return; }
                else if (action == 2)
                {
                    newIndex = 0;
                    _notification.Show(new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader().GetString("Notification_LoopedStart"));
                }
                else { newIndex = _state.CurrentIndex; }
            }

            if (newIndex < 0) newIndex = 0;
            if (newIndex >= _state.Playlist.Count) newIndex = Math.Max(0, _state.Playlist.Count - 1);

            if (newIndex != _state.CurrentIndex)
            {
                _state.CurrentIndex = newIndex;
                WeakReferenceMessenger.Default.Send(new RefreshDisplayMessage());
            }
        }

        private int GetEffectiveSplitCountForIndex(int index)
        {
            int splitCount = _settings.MangaSplitCount;
            if (splitCount <= 1 || index < 0 || index >= _state.Playlist.Count) return 1;

            int remaining = _state.Playlist.Count - index;
            int effective = Math.Min(splitCount, remaining);

            if (ArchiveManager.IsArchive(_state.Playlist[index]) && !ArchiveManager.IsArchivePath(_state.Playlist[index])) return 1;

            for (int i = 1; i < effective; i++)
            {
                if (ArchiveManager.IsArchive(_state.Playlist[index + i]) && !ArchiveManager.IsArchivePath(_state.Playlist[index + i])) return i;
            }
            return effective;
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
                    List<string> initialFiles = preloadedPlaylist ?? FolderDiscoveryService.GetInitialPlaylist(path, _settings.EnabledExtensions);

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
                        WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
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
                    WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
                }
            }
            else if (includeSiblings || includeSubfolders)
            {
                _ = Task.Run(() => DiscoverAdditionalFilesAsync(path, initialFile, includeSiblings, includeSubfolders, token));
            }
            else
            {
                _state.IsSearchingFolder = false;
                WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
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
            }, token, _settings.EnabledExtensions);

            _dispatcherQueue.TryEnqueue(() =>
            {
                if (!token.IsCancellationRequested)
                {
                    UpdatePlaylist(accumulatedFiles, _state.CurrentImagePath, true);
                    WeakReferenceMessenger.Default.Send(new PlaylistUpdatedMessage(false));
                }
                _state.IsSearchingFolder = false;
                WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
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