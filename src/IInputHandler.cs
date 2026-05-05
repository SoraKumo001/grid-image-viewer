using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace grid_image_viewer
{
    public interface IInputHandler
    {
        void HandlePointerMoved(object sender, PointerRoutedEventArgs e);
        void HandleKeyDown(object sender, KeyRoutedEventArgs e);
        void HandlePointerWheelChanged(object sender, PointerRoutedEventArgs e);
        void HandleDoubleTapped(object sender, DoubleTappedRoutedEventArgs e);
        void HandleDrop(object sender, DragEventArgs e);
        void HandleDragOver(object sender, DragEventArgs e);
    }
}
