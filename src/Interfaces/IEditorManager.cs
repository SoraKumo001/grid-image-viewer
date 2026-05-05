using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
namespace quick_image_viewer.Interfaces
{
    public interface IEditorManager
    {
        string ContextTargetPath { get; set; }
        void UpdateMenuStates();
        void PagesGrid_PointerMoved(object sender, PointerRoutedEventArgs e);
        void EditMenuFlyout_Opening(object sender, object e);
        void UpdateTargetIndexAtPoint(Point p);
        void MenuSaveAs_Click(object sender, RoutedEventArgs e);
        void MenuOverwrite_Click(object sender, RoutedEventArgs e);
        void MenuCrop_Click(object sender, RoutedEventArgs e);
        void ExecuteCropWithRect(Rect selectionRect);
        void MenuSettings_Click(object sender, RoutedEventArgs e);
        void MenuSupport_Click(object sender, RoutedEventArgs e);
        void MenuUndo_Click(object sender, RoutedEventArgs e);
        void MenuRedo_Click(object sender, RoutedEventArgs e);
        void MenuMetadata_Click(object sender, RoutedEventArgs e);
        void MenuPageIndicatorToggle_Click(object sender, RoutedEventArgs e);
        void MenuResize_Click(object sender, RoutedEventArgs e);
        void MenuTone_Click(object sender, RoutedEventArgs e);
        void MenuFilter_Click(object sender, RoutedEventArgs e);
        void MenuRotate_Click(object sender, RoutedEventArgs e);
        void MenuFlip_Click(object sender, RoutedEventArgs e);
        void MenuPrint_Click(object sender, RoutedEventArgs e);
        void MenuOpenExplorer_Click(object sender, RoutedEventArgs e);
        void MenuViewMode_Click(object sender, RoutedEventArgs e);
        void MenuLayoutMode_Click(object sender, RoutedEventArgs e);
        void MenuStretchMode_Click(object sender, RoutedEventArgs e);
        void MenuKeyBindings_Click(object sender, RoutedEventArgs e);
        string GetString(string key);
    }
}
