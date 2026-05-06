using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Services;
using quick_image_viewer.ViewModels;

namespace quick_image_viewer.Managers
{
    internal class BookmarkManager : IBookmarkManager, IRecipient<ToggleBookmarkMessage>, IRecipient<ToggleBookmarkPanelMessage>
    {
        private readonly IViewerStateService _state;
        private readonly ISettingsManager _settings;
        private readonly INotificationService _notification;
        private readonly IPlaylistManager _playlist;

        public BookmarkManager(IViewerStateService state, ISettingsManager settings, INotificationService notification, IPlaylistManager playlist)
        {
            _state = state;
            _settings = settings;
            _notification = notification;
            _playlist = playlist;

            WeakReferenceMessenger.Default.Register<ToggleBookmarkMessage>(this);
            WeakReferenceMessenger.Default.Register<ToggleBookmarkPanelMessage>(this);
        }

        public void Receive(ToggleBookmarkMessage message) => MenuBookmark_Click(null!, null!);
        public void Receive(ToggleBookmarkPanelMessage message) => MenuBookmarksToggle_Click(null!, null!);

        public void UpdateBookmarkList()
        {
            // Now handled by MainViewModel listening to BookmarksChangedMessage
            WeakReferenceMessenger.Default.Send(new BookmarksChangedMessage());
        }

        public void MoveBookmark(string path, int direction)
        {
            int index = _settings.Bookmarks.FindIndex(b => b.Path == path);
            if (index == -1) return;

            int newIndex = index + direction;
            if (newIndex < 0 || newIndex >= _settings.Bookmarks.Count) return;

            var item = _settings.Bookmarks[index];
            _settings.Bookmarks.RemoveAt(index);
            _settings.Bookmarks.Insert(newIndex, item);
            _settings.SaveSettings();

            UpdateBookmarkList();
        }

        public void MenuBookmark_Click(object sender, RoutedEventArgs e)
        {
            string dir = _state.CurrentDirectory;
            if (string.IsNullOrEmpty(dir)) return;

            bool added = _settings.ToggleBookmark(dir, true);
            UpdateBookmarkList();

            var loader = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader();
            _notification.Show(loader.GetString(added ? "Notification_AddedToBookmarks" : "Notification_RemovedFromBookmarks"));
        }

        public void MenuBookmarksToggle_Click(object sender, RoutedEventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new ToggleBookmarkPanelMessage());
        }

        public void BookmarkListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is BookmarkItem item)
            {
                _playlist.LoadDirectory(item.Path);
                // We might want a message to close the panel if needed, but for now we can rely on ViewModel
                // Or just send the toggle message again to close it
                WeakReferenceMessenger.Default.Send(new ToggleBookmarkPanelMessage());
            }
        }

        public void MenuBookmarkRemove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path)
            {
                _settings.ToggleBookmark(path, true);
                UpdateBookmarkList();
            }
        }

        public void BookmarkListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            _settings.SaveSettings();
            UpdateBookmarkList();
        }
    }
}