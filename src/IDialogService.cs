namespace grid_image_viewer
{
    public interface IDialogService
    {
        void Show(Microsoft.UI.Xaml.FrameworkElement overlay, int rowSpan = 2);
        void Close(string name);
        void Close(Microsoft.UI.Xaml.FrameworkElement overlay);
        void UpdateDialogOpenState();
    }
}
