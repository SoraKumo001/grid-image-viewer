namespace quick_image_viewer.Interfaces
{
    public interface IMetadataDisplayService
    {
        void ToggleMetadataPanel(bool cycle = true);
        void UpdateMetadataPanel();
        void UpdateFocusBorders();
        void HandlePointerMoved(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e);
    }
}
