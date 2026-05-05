namespace grid_image_viewer
{
    public interface IGridManager
    {
        void UpdateGridItems(bool forceFullUpdate);
        System.Collections.Generic.IList<ImageItem> GridItems { get; }
        void StartGridAnimation();
        void StopGridAnimation();
        void ImageGridView_ItemClick(object sender, Microsoft.UI.Xaml.Controls.ItemClickEventArgs e);
        void ImageGridView_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e);
        void RefreshThumbnails();
        void Dispose();
    }
}
