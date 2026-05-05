using quick_image_viewer.Helpers;
using quick_image_viewer.Views.Controls;
using SkiaSharp.Views.Windows;
using System;
using System.Threading.Tasks;
namespace quick_image_viewer.Interfaces
{
    public interface IViewerManager : IDisposable
    {
        Task UpdateDisplayAsync();
        void UpdateStretch();
        void HandleWindowSizeChanged(double width, double height);
        void PaintCanvas(int bufferIndex, int pageIndex, SKPaintSurfaceEventArgs e);
        string? GetPathForPage(int index);
        void InvalidatePage(int index);
        void ToggleMetadataPanel(bool cycle = false);
        void ShowNotification(string message);
        void Navigate(int offset, bool forceSingleStep);
        void NavigateFolder(int offset);
        PageRenderer[] Pages { get; }
        ViewerPageControl[] PageControls { get; }
        void StopAnimation();
        void HandlePointerMoved(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e);
    }
}
