using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using quick_image_viewer.ViewModels;
using quick_image_viewer.Views.Controls;
using System.Collections.ObjectModel;

namespace quick_image_viewer.Interfaces
{
    public interface IMetadataHost
    {
        MainViewModel ViewModel { get; }
        ObservableCollection<string> Playlist { get; }
        int CurrentIndex { get; set; }
        Grid PagesGrid { get; }
        ViewerPanel ViewerControl { get; }

        TextBlock TxtMetaTitle { get; }
        TextBlock TxtMetaFileName { get; }
        TextBlock TxtMetaDimensions { get; }
        TextBlock TxtMetaFileSize { get; }
        UIElement ExifDivider { get; }
        UIElement ExifGrid { get; }
        TextBlock TxtMetaCamera { get; }
        TextBlock TxtMetaLens { get; }
        TextBlock TxtMetaSettings { get; }
        TextBlock TxtMetaDate { get; }
    }
}
