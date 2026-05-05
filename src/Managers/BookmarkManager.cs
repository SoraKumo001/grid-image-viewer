using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using quick_image_viewer.Interfaces;
namespace quick_image_viewer.Managers
{
    internal class BookmarkManager : IBookmarkManager
    {
        private readonly IMainView _window;
        private readonly ISettingsManager _settings;

        public BookmarkManager(IMainView window, ISettingsManager settings)
        {
            _window = window;
            _settings = settings;
        }

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
            _window.ShowNotification(added ? "Added to bookmarks" : "Removed from bookmarks");
        }

        public void MenuBookmarksToggle_Click(object sender, RoutedEventArgs e)
        {
            bool show = _window.BookmarkPanel.Visibility != Visibility.Visible;
            _window.BookmarkPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show) UpdateBookmarkList();
            _window.EditorManager.UpdateMenuStates();
        }

        public void BookmarkListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is BookmarkItem item)
            {
                _window.LoadDirectory(item.Path);
                _window.BookmarkPanel.Visibility = Visibility.Collapsed;
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