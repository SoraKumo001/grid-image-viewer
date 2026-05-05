using System;

namespace grid_image_viewer
{
    public interface ISlideshowManager : IDisposable
    {
        bool IsSlideshowRunning { get; }
        int[] SlideshowRandomIndices { get; }
        void OpenSlideshowDialogAsync();
        void StopSlideshow();
        void StartSlideshow();
        void SlideshowDialog_Opened(Microsoft.UI.Xaml.Controls.ContentDialog sender, Microsoft.UI.Xaml.Controls.ContentDialogOpenedEventArgs args);
        void SlideshowDialog_PrimaryButtonClick(Microsoft.UI.Xaml.Controls.ContentDialog sender, Microsoft.UI.Xaml.Controls.ContentDialogButtonClickEventArgs args);
    }
}
