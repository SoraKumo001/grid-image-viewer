using SkiaSharp;

namespace quick_image_viewer.Interfaces
{
    public interface IDialogService
    {
        void Show(Microsoft.UI.Xaml.FrameworkElement overlay, int rowSpan = 2);
        void Close(string name);
        void Close(Microsoft.UI.Xaml.FrameworkElement overlay);
        void UpdateDialogOpenState();

        // Specialized dialog methods to decouple Managers from View
        void ShowCropOverlay();
        void ShowSettingsOverlay(ISettingsManager settings);
        void ShowResizeOverlay(string sourcePath, int origW, int origH);
        void ShowToneAdjustmentOverlay(string sourcePath, SKBitmap? baseBmp);
        void ShowKeyBindingsOverlay(ISettingsManager settings);
    }
}
