using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Dispatching;
using quick_image_viewer.Common;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Services;
using quick_image_viewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
        private readonly IViewerCacheManager _cacheManager;
        private readonly DispatcherQueue _dispatcherQueue;
        private CancellationTokenSource? _loadCts;
        private CancellationTokenSource? _navigateCts;

        public PlaylistManager(IViewerStateService state, ISettingsManager settings, INotificationService notification, IViewerCacheManager cacheManager)
        {
            _state = state;
            _settings = settings;
            _notification = notification;
            _cacheManager = cacheManager;
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

            if (ArchiveManager.IsArchive(_state.Playlist[currentIndex]) && !ArchiveManager.IsArchivePath(_state.Playlist[currentIndex]))
            {
                return 1;
            }

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
            if (_state.Playlist.Count == 0 || _state.IsDisplayUpdating) return;

            int step = _settings.MangaSplitCount;
            int newIndex = _state.CurrentIndex;

            if (forceSingleStep || step <= 1)
            {
                newIndex = _state.CurrentIndex + offset;
            }
            else if (offset > 0)
            {
                for (int i = 0; i < offset; i++)
                {
                    newIndex += GetEffectiveSplitCountForIndex(newIndex);
                    if (newIndex >= _state.Playlist.Count) break;
                }
            }
            else
            {
                for (int i = 0; i < Math.Abs(offset); i++)
                {
                    if (newIndex <= 0) break;

                    int target = newIndex - 1;
                    if (ArchiveManager.IsArchive(_state.Playlist[target]) && !ArchiveManager.IsArchivePath(_state.Playlist[target]))
                    {
                        newIndex = target;
                    }
                    else
                    {
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

            if (newIndex < 0 || (offset < 0 && newIndex == _state.CurrentIndex && _state.CurrentIndex == 0))
            {
                int action = _settings.BoundaryAction;
                if (action == 1) { NavigateFolder(-1); return; }
                else if (action == 2)
                {
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
                    _notification.Show(_settings.GetString("Notification_LoopedEnd"));
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
                    _notification.Show(_settings.GetString("Notification_LoopedStart"));
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
            if (_state.IsDisplayUpdating) return;
            string currentDir = _state.CurrentDirectory;
            if (string.IsNullOrEmpty(currentDir)) return;

            currentDir = currentDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (currentDir.Length == 2 && currentDir[1] == ':') currentDir += Path.DirectorySeparatorChar;

            // Cancel previous navigation request to prevent race conditions
            _navigateCts?.Cancel();
            _navigateCts = new CancellationTokenSource();
            var token = _navigateCts.Token;

            var (preloadedPath, preloadedPlaylist) = _cacheManager.GetPreloadedFolderData(currentDir, offset);

            // Only use preloaded data if it points to a DIFFERENT folder than the current one
            if (!string.IsNullOrEmpty(preloadedPath) &&
                !string.Equals(preloadedPath, currentDir, StringComparison.OrdinalIgnoreCase) &&
                preloadedPlaylist != null && preloadedPlaylist.Count > 0)
            {
                LoadDirectory(preloadedPath, string.Empty, false, false, preloadedPlaylist);
                return;
            }

            var allowedExtensions = _settings.EnabledExtensions;
            _state.IsSearchingFolder = true;

            _ = Task.Run(() =>
            {
                try
                {
                    string? targetDir = FileNavigator.FindNextImageFolder(currentDir, offset, allowedExtensions, token);

                    if (token.IsCancellationRequested) return;

                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        if (token.IsCancellationRequested) return;

                        if (!string.IsNullOrEmpty(targetDir))
                        {
                            LoadDirectory(targetDir, string.Empty);
                        }
                        else
                        {
                            _state.IsSearchingFolder = false;
                            _notification.Show(_settings.GetString(offset > 0 ? "Notification_NoMoreFoldersEnd" : "Notification_NoMoreFoldersStart"));
                            WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
                        }
                    });
                }
                catch (Exception ex)
                {
                    AppLog.Error("PlaylistManager", "NavigateFolder failed", ex);
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        _state.IsSearchingFolder = false;
                        WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
                    });
                }
            });
        }

        public void LoadDirectory(string path, string initialFile = "", bool includeSiblings = false, bool includeSubfolders = false, List<string>? preloadedPlaylist = null)
        {
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            _state.CurrentDirectory = path;
            _state.IsSearchingFolder = true;
            _state.Playlist.Clear();

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
                catch (Exception ex)
                {
                    AppLog.Error("PlaylistManager", "LoadDirectory failed", ex);
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        _state.IsSearchingFolder = false;
                        WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
                    });
                }
            });
        }

        private void OnInitialFilesLoaded(string path, string initialFile, bool includeSiblings, bool includeSubfolders, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            if (_state.Playlist.Count > 0)
            {
                WeakReferenceMessenger.Default.Send(new PlaylistUpdatedMessage(false));

                if (!ArchiveManager.IsArchive(path, _settings.EnabledExtensions) && (includeSiblings || includeSubfolders))
                {
                    _ = Task.Run(() => DiscoverAdditionalFilesAsync(path, initialFile, includeSiblings, includeSubfolders, token));
                }
                else
                {
                    // For fast preloaded loads, give a tiny buffer for UI signals to propagate before hiding overlay
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(200);
                        _dispatcherQueue.TryEnqueue(() =>
                        {
                            if (token.IsCancellationRequested) return;
                            _state.IsSearchingFolder = false;
                            WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
                        });
                    });
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
            List<string> sortedFiles = alreadySorted ? files : files.Distinct().OrderBy(f => f, new NaturalStringComparer()).ToList();

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

            try
            {
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
            }
            catch (Exception ex)
            {
                AppLog.Error("PlaylistManager", "DiscoverAdditionalFilesAsync failed", ex);
            }
            finally
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    if (token.IsCancellationRequested) return;

                    UpdatePlaylist(accumulatedFiles, _state.CurrentImagePath, true);
                    WeakReferenceMessenger.Default.Send(new PlaylistUpdatedMessage(false));

                    _state.IsSearchingFolder = false;
                    WeakReferenceMessenger.Default.Send(new FocusRequestMessage());
                });
            }
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
                if (_state.CurrentIndex > index) _state.CurrentIndex--;
                if (_state.CurrentIndex >= _state.Playlist.Count) _state.CurrentIndex = _state.Playlist.Count - 1;

                WeakReferenceMessenger.Default.Send(new PlaylistUpdatedMessage(false));
            }
        }

        public void ReplaceInPlaylist(string oldPath, string newPath)
        {
            int index = _state.Playlist.IndexOf(oldPath);
            if (index != -1)
            {
                _state.Playlist[index] = newPath;
                WeakReferenceMessenger.Default.Send(new PlaylistUpdatedMessage(false));
            }
        }
    }
}
