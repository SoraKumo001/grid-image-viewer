using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using quick_image_viewer.Interfaces;
using quick_image_viewer.ViewModels;

namespace quick_image_viewer.Managers
{
    internal class BookmarkManager : IBookmarkManager, IRecipient<ToggleBookmarkMessage>, IRecipient<ToggleBookmarkPanelMessage>
    {
        private readonly IMainView _window;
        private readonly ISettingsManager _settings;

        public BookmarkManager(IMainView window, ISettingsManager settings)
        {
            _window = window;
            _settings = settings;
            WeakReferenceMessenger.Default.Register<ToggleBookmarkMessage>(this);
            WeakReferenceMessenger.Default.Register<ToggleBookmarkPanelMessage>(this);
        }

        public void Receive(ToggleBookmarkMessage message) => MenuBookmark_Click(null!, null!);
        public void Receive(ToggleBookmarkPanelMessage message) => MenuBookmarksToggle_Click(null!, null!);

        public void UpdateBookmarkList()
        {
            _window.BookmarkListView.ItemsSource = null;
            _window.BookmarkListView.ItemsSource = _settings.Bookmarks;
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
            _window.EditorManager.UpdateMenuStates();
        }

        public void MenuBookmark_Click(object sender, RoutedEventArgs e)
        {
            string dir = _window.CurrentDirectory;
            if (string.IsNullOrEmpty(dir)) return;

            bool added = _settings.ToggleBookmark(dir, true);
            _window.EditorManager.UpdateMenuStates();
            UpdateBookmarkList();
            var loader = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader();
            _window.ShowNotification(loader.GetString(added ? "Notification_AddedToBookmarks" : "Notification_RemovedFromBookmarks"));
        }

        public void MenuBookmarksToggle_Click(object sender, RoutedEventArgs e)
        {
            bool show = !_window.ViewModel.IsBookmarkPanelVisible;
            _window.ViewModel.IsBookmarkPanelVisible = show;
            if (show) UpdateBookmarkList();
            _window.EditorManager.UpdateMenuStates();
        }

        public void BookmarkListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is BookmarkItem item)
            {
                _window.LoadDirectory(item.Path);
                _window.ViewModel.IsBookmarkPanelVisible = false;
            }
        }

        public void MenuBookmarkRemove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path)
            {
                _settings.ToggleBookmark(path, true);
                UpdateBookmarkList();
                _window.EditorManager.UpdateMenuStates();
            }
        }

        public void BookmarkListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            _settings.SaveSettings();
            _window.EditorManager.UpdateMenuStates();
        }
    }
}