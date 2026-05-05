namespace quick_image_viewer.Interfaces
{
    public interface IDialogService
    {
        void Show(Microsoft.UI.Xaml.FrameworkElement overlay, int rowSpan = 2);
        void Close(string name);
        void Close(Microsoft.UI.Xaml.FrameworkElement overlay);
        void UpdateDialogOpenState();
    }
}
