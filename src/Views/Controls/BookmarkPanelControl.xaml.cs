using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using quick_image_viewer.ViewModels;
using System;

namespace quick_image_viewer.Views.Controls
{
    public sealed partial class BookmarkPanelControl : UserControl
    {
        public event EventHandler? PanelHoverStarted;
        public event EventHandler? PanelHoverEnded;

        public MainViewModel ViewModel
        {
            get => (MainViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(MainViewModel), typeof(BookmarkPanelControl), new PropertyMetadata(null));

        public event ItemClickEventHandler? BookmarkItemClick;
        public event Windows.Foundation.TypedEventHandler<ListViewBase, DragItemsCompletedEventArgs>? DragItemsCompleted;
        public event RoutedEventHandler? RemoveBookmarkClick;

        public BookmarkPanelControl()
        {
            this.InitializeComponent();
        }

        public ListView ListView => BookmarkListView;

        private void BookmarkListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            BookmarkItemClick?.Invoke(sender, e);
        }

        private void BookmarkListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            DragItemsCompleted?.Invoke(sender, args);
        }

        private void MenuBookmarkRemove_Click(object sender, RoutedEventArgs e)
        {
            RemoveBookmarkClick?.Invoke(sender, e);
        }

        private void Border_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            PanelHoverStarted?.Invoke(this, EventArgs.Empty);
        }

        private void Border_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            PanelHoverEnded?.Invoke(this, EventArgs.Empty);
        }
    }
}