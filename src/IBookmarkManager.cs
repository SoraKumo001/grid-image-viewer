using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace grid_image_viewer
{
    public interface IBookmarkManager
    {
        void UpdateBookmarkList();
        void MoveBookmark(string path, int direction);
        void MenuBookmark_Click(object sender, RoutedEventArgs e);
        void MenuBookmarksToggle_Click(object sender, RoutedEventArgs e);
        void BookmarkListView_ItemClick(object sender, ItemClickEventArgs e);
        void MenuBookmarkRemove_Click(object sender, RoutedEventArgs e);
        void BookmarkListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args);
    }
}
